// Three.js mesh renderer interop
// Provides functions to initialize Three.js scene and load/display meshes

window.meshRenderer = (() => {
    let scene = null;
    let camera = null;
    let renderer = null;
    let controls = null;
    let meshes = new Map(); // Track loaded meshes by ID
    let meshIdCounter = 0;
    let resizeHandler = null; // Track resize handler for proper cleanup
    let animationFrameId = null; // Track animation frame for cleanup

    /**
     * Initialize Three.js scene with canvas element
     * @param {string} canvasId - ID of the canvas element
     * @param {object} options - Optional configuration
     */
    function init(canvasId, options = {}) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            throw new Error(`Canvas element '${canvasId}' not found`);
        }

        // Scene
        scene = new THREE.Scene();
        scene.background = new THREE.Color(options.background || 0x222222);

        // Camera
        camera = new THREE.PerspectiveCamera(
            75,
            canvas.clientWidth / canvas.clientHeight,
            0.1,
            1000
        );
        camera.position.set(2, 2, 3);
        camera.lookAt(0, 0, 0);

        // Renderer
        renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        renderer.setSize(canvas.clientWidth, canvas.clientHeight);
        renderer.setPixelRatio(window.devicePixelRatio);

        // Lights
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambientLight);

        const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
        directionalLight.position.set(5, 10, 7.5);
        scene.add(directionalLight);

        // OrbitControls (if available)
        if (THREE.OrbitControls) {
            controls = new THREE.OrbitControls(camera, renderer.domElement);
            controls.enableDamping = true;
            controls.dampingFactor = 0.05;
        }

        // Animation loop with safe guards
        function animate() {
            if (!renderer || !scene || !camera) {
                // Stop animation if disposed
                return;
            }
            animationFrameId = requestAnimationFrame(animate);
            if (controls) controls.update();
            renderer.render(scene, camera);
        }
        animate();

        // Handle window resize - store handler for cleanup
        resizeHandler = () => {
            if (!camera || !renderer) return;
            camera.aspect = canvas.clientWidth / canvas.clientHeight;
            camera.updateProjectionMatrix();
            renderer.setSize(canvas.clientWidth, canvas.clientHeight);
        };
        window.addEventListener('resize', resizeHandler);
    }

    /**
     * Load mesh geometry into the scene
     * @param {object} geometry - Geometry data with positions, indices, etc.
     * @param {array} groups - Optional submesh groups
     * @param {object} materialOpts - Optional material options
     * @returns {string} Mesh ID for future reference
     */
    function loadMesh(geometry, groups = null, materialOpts = {}) {
        if (!scene) {
            throw new Error('Scene not initialized. Call init() first.');
        }

        // Create BufferGeometry
        const bufferGeometry = new THREE.BufferGeometry();

        // Set positions
        const positions = new Float32Array(geometry.positions);
        bufferGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

        // Set indices
        if (geometry.indices) {
            const indices = geometry.indices.length > 65535 
                ? new Uint32Array(geometry.indices)
                : new Uint16Array(geometry.indices);
            bufferGeometry.setIndex(new THREE.BufferAttribute(indices, 1));
        }

        // Set normals if available
        if (geometry.normals) {
            const normals = new Float32Array(geometry.normals);
            bufferGeometry.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
        } else {
            // Compute normals if not provided
            bufferGeometry.computeVertexNormals();
        }

        // Set UVs if available
        if (geometry.uvs) {
            const uvs = new Float32Array(geometry.uvs);
            bufferGeometry.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
        }

        // Create material
        const material = new THREE.MeshStandardMaterial({
            color: materialOpts.color || 0x4488ff,
            wireframe: materialOpts.wireframe || false,
            metalness: materialOpts.metalness || 0.3,
            roughness: materialOpts.roughness || 0.7,
            side: THREE.DoubleSide
        });

        // Create mesh
        const mesh = new THREE.Mesh(bufferGeometry, material);

        // Add to scene
        scene.add(mesh);

        // Generate and store mesh ID
        const meshId = `mesh_${++meshIdCounter}`;
        meshes.set(meshId, mesh);

        // Center and fit camera to mesh
        const box = new THREE.Box3().setFromObject(mesh);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());
        const maxDim = Math.max(size.x, size.y, size.z);
        const fov = camera.fov * (Math.PI / 180);
        let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2));
        cameraZ *= 1.5; // Add some padding
        
        camera.position.set(center.x, center.y, center.z + cameraZ);
        camera.lookAt(center);
        
        if (controls) {
            controls.target.copy(center);
            controls.update();
        }

        return meshId;
    }

    /**
     * Update material properties of a mesh
     * @param {string} meshId - Mesh identifier
     * @param {object} materialOpts - Material properties to update
     */
    function updateMaterial(meshId, materialOpts) {
        const mesh = meshes.get(meshId);
        if (!mesh) {
            throw new Error(`Mesh '${meshId}' not found`);
        }

        if (materialOpts.color !== undefined) {
            // Handle various color formats (hex string with/without #, or numeric)
            let colorValue = materialOpts.color;
            if (typeof colorValue === 'string') {
                colorValue = colorValue.startsWith('#') ? colorValue.substring(1) : colorValue;
                mesh.material.color.setHex(parseInt(colorValue, 16));
            } else if (typeof colorValue === 'number') {
                mesh.material.color.setHex(colorValue);
            }
        }
        if (materialOpts.wireframe !== undefined) {
            mesh.material.wireframe = materialOpts.wireframe;
        }
        if (materialOpts.metalness !== undefined) {
            mesh.material.metalness = materialOpts.metalness;
        }
        if (materialOpts.roughness !== undefined) {
            mesh.material.roughness = materialOpts.roughness;
        }
    }

    /**
     * Clear all meshes from scene
     */
    function clear() {
        if (!scene) return;

        meshes.forEach((mesh) => {
            scene.remove(mesh);
            mesh.geometry.dispose();
            mesh.material.dispose();
        });
        meshes.clear();
    }

    /**
     * Dispose specific mesh or all resources
     * @param {string} meshId - Optional mesh ID to dispose (if null, disposes all)
     */
    function dispose(meshId = null) {
        if (meshId) {
            const mesh = meshes.get(meshId);
            if (mesh) {
                scene.remove(mesh);
                mesh.geometry.dispose();
                mesh.material.dispose();
                meshes.delete(meshId);
            }
        } else {
            // Stop animation loop
            if (animationFrameId !== null) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }
            
            clear();
            if (renderer) {
                renderer.dispose();
                renderer = null;
            }
            if (resizeHandler) {
                window.removeEventListener('resize', resizeHandler);
                resizeHandler = null;
            }
            scene = null;
            camera = null;
            controls = null;
        }
    }

    return {
        init,
        loadMesh,
        updateMaterial,
        clear,
        dispose
    };
})();

