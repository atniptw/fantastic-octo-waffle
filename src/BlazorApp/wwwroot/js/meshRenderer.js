/**
 * Three.js mesh renderer interop module.
 * Provides functions for rendering 3D meshes from parsed Unity assets.
 * 
 * Contract (from docs/BlazorUI.md and docs/DataModels.md):
 * - positions: Float32Array length 3 * vertexCount (XYZ)
 * - indices: Uint16Array | Uint32Array length 3 * triangleCount
 * - normals (optional): Float32Array length 3 * vertexCount
 * - uvs (optional): Float32Array length 2 * vertexCount
 * - groups (optional): Array<{ start: number, count: number, materialIndex: number }>
 */

import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

// Lighting constants
const AMBIENT_LIGHT_INTENSITY = 0.6;
const DIRECTIONAL_LIGHT_INTENSITY = 0.8;

// Camera positioning constant
const CAMERA_MARGIN_FACTOR = 1.5;

// Module state
let scene = null;
let camera = null;
let renderer = null;
let controls = null;
let meshes = new Map(); // meshId -> THREE.Mesh
let animationId = null;
let nextMeshId = 1;

/**
 * Initialize Three.js viewer with the specified canvas element.
 * @param {string} canvasId - ID of the canvas element
 * @param {object} options - Configuration options
 * @param {number} options.fov - Camera field of view (default: 60)
 * @param {number} options.near - Camera near plane (default: 0.1)
 * @param {number} options.far - Camera far plane (default: 1000)
 * @param {string|number} options.background - Background color (default: 0x1a1a1a)
 * @returns {Promise<void>}
 * @throws {Error} If canvas not found or already initialized
 */
export async function init(canvasId, options = {}) {
    if (renderer) {
        throw new Error('Renderer already initialized');
    }

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        throw new Error(`Canvas element with id '${canvasId}' not found`);
    }

    // Setup renderer
    renderer = new THREE.WebGLRenderer({
        canvas: canvas,
        antialias: true,
        alpha: true
    });
    renderer.setSize(canvas.clientWidth, canvas.clientHeight);
    renderer.setPixelRatio(window.devicePixelRatio);

    // Setup scene
    scene = new THREE.Scene();
    const bgColor = options.background ?? 0x1a1a1a;
    scene.background = new THREE.Color(bgColor);

    // Setup camera
    const fov = options.fov ?? 60;
    const aspect = canvas.clientWidth / canvas.clientHeight;
    const near = options.near ?? 0.1;
    const far = options.far ?? 1000;
    camera = new THREE.PerspectiveCamera(fov, aspect, near, far);
    camera.position.set(0, 0, 5);

    // Setup controls
    controls = new OrbitControls(camera, canvas);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.screenSpacePanning = false;
    controls.minDistance = 0.5;
    controls.maxDistance = 500;

    // Add lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, AMBIENT_LIGHT_INTENSITY);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, DIRECTIONAL_LIGHT_INTENSITY);
    directionalLight.position.set(1, 1, 1);
    scene.add(directionalLight);

    // Start animation loop
    startAnimationLoop();
}

/**
 * Animation loop for rendering and updating controls.
 */
function startAnimationLoop() {
    function animate() {
        animationId = requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
    }
    animate();
}

/**
 * Load mesh geometry into the viewer and display it.
 * @param {object} geometry - Geometry data
 * @param {Float32Array} geometry.positions - Vertex positions (XYZ)
 * @param {Uint16Array|Uint32Array} geometry.indices - Triangle indices
 * @param {Float32Array} [geometry.normals] - Vertex normals (XYZ)
 * @param {Float32Array} [geometry.uvs] - Texture coordinates (UV)
 * @param {Array<object>} [groups] - Submesh groups
 * @param {object} [materialOpts] - Material options
 * @param {string|number} [materialOpts.color] - Material color (default: 0x888888)
 * @param {boolean} [materialOpts.wireframe] - Wireframe mode (default: false)
 * @param {number} [materialOpts.metalness] - Metalness (default: 0.5)
 * @param {number} [materialOpts.roughness] - Roughness (default: 0.5)
 * @returns {Promise<string>} Mesh ID for future operations
 * @throws {Error} If renderer not initialized or geometry invalid
 */
export async function loadMesh(geometry, groups = null, materialOpts = {}) {
    validateRendererInitialized();
    validateGeometry(geometry);

    const bufferGeometry = createBufferGeometry(geometry, groups);
    const material = createMaterial(materialOpts);
    const mesh = new THREE.Mesh(bufferGeometry, material);
    
    scene.add(mesh);
    centerCameraOnMesh(mesh);

    const meshId = `mesh-${nextMeshId++}`;
    meshes.set(meshId, mesh);
    return meshId;
}

/**
 * Validate that the renderer is initialized.
 * @throws {Error} If renderer not initialized
 */
function validateRendererInitialized() {
    if (!renderer || !scene) {
        throw new Error('Renderer not initialized. Call init() first.');
    }
}

/**
 * Validate geometry data.
 * @param {object} geometry - Geometry to validate
 * @throws {Error} If geometry is invalid
 */
function validateGeometry(geometry) {
    if (!geometry?.positions || !geometry?.indices) {
        throw new Error('Invalid geometry: positions and indices are required');
    }

    const vertexCount = geometry.positions.length / 3;
    const triangleCount = geometry.indices.length / 3;

    if (vertexCount < 3 || triangleCount < 1) {
        throw new Error('Invalid geometry: insufficient vertices or triangles');
    }
}

/**
 * Create Three.js BufferGeometry from geometry data.
 * @param {object} geometry - Geometry data
 * @param {Array<object>} groups - Submesh groups
 * @returns {THREE.BufferGeometry} Created geometry
 */
function createBufferGeometry(geometry, groups) {
    const bufferGeometry = new THREE.BufferGeometry();
    
    bufferGeometry.setAttribute('position',
        new THREE.BufferAttribute(geometry.positions, 3));
    bufferGeometry.setIndex(
        new THREE.BufferAttribute(geometry.indices, 1));

    addNormalsToGeometry(bufferGeometry, geometry);
    addUVsToGeometry(bufferGeometry, geometry);
    addGroupsToGeometry(bufferGeometry, groups);

    return bufferGeometry;
}

/**
 * Add normals to geometry or compute them.
 * @param {THREE.BufferGeometry} bufferGeometry - Target geometry
 * @param {object} geometry - Source geometry data
 */
function addNormalsToGeometry(bufferGeometry, geometry) {
    if (geometry.normals && geometry.normals.length === geometry.positions.length) {
        bufferGeometry.setAttribute('normal',
            new THREE.BufferAttribute(geometry.normals, 3));
    } else {
        bufferGeometry.computeVertexNormals();
    }
}

/**
 * Add UVs to geometry if present.
 * @param {THREE.BufferGeometry} bufferGeometry - Target geometry
 * @param {object} geometry - Source geometry data
 */
function addUVsToGeometry(bufferGeometry, geometry) {
    const vertexCount = geometry.positions.length / 3;
    if (geometry.uvs && geometry.uvs.length === vertexCount * 2) {
        bufferGeometry.setAttribute('uv',
            new THREE.BufferAttribute(geometry.uvs, 2));
    }
}

/**
 * Add groups to geometry for submeshes.
 * @param {THREE.BufferGeometry} bufferGeometry - Target geometry
 * @param {Array<object>} groups - Submesh groups
 */
function addGroupsToGeometry(bufferGeometry, groups) {
    if (groups && Array.isArray(groups)) {
        for (const group of groups) {
            bufferGeometry.addGroup(group.start, group.count, group.materialIndex);
        }
    }
}

/**
 * Create material with specified options.
 * @param {object} opts - Material options
 * @returns {THREE.Material} Created material
 */
function createMaterial(opts) {
    return new THREE.MeshStandardMaterial({
        color: opts.color ?? 0x888888,
        wireframe: opts.wireframe ?? false,
        metalness: opts.metalness ?? 0.5,
        roughness: opts.roughness ?? 0.5,
        side: THREE.DoubleSide
    });
}

/**
 * Center camera on the given mesh using its bounding box.
 * @param {THREE.Mesh} mesh - Mesh to center on
 */
function centerCameraOnMesh(mesh) {
    const box = new THREE.Box3().setFromObject(mesh);
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());

    // Set controls target to mesh center
    controls.target.copy(center);

    // Position camera to view entire mesh
    const maxDim = Math.max(size.x, size.y, size.z);
    const fov = camera.fov * (Math.PI / 180);
    let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2));
    cameraZ *= CAMERA_MARGIN_FACTOR; // Add some margin

    camera.position.set(
        center.x + cameraZ * 0.5,
        center.y + cameraZ * 0.5,
        center.z + cameraZ
    );
    camera.lookAt(center);
    controls.update();
}

/**
 * Update material properties of a displayed mesh.
 * @param {string} meshId - Mesh ID returned from loadMesh
 * @param {object} opts - Material options to update
 * @param {string|number} [opts.color] - New color
 * @param {boolean} [opts.wireframe] - Wireframe mode
 * @param {number} [opts.metalness] - Metalness value
 * @param {number} [opts.roughness] - Roughness value
 * @returns {Promise<void>}
 * @throws {Error} If mesh not found
 */
export async function updateMaterial(meshId, opts = {}) {
    const mesh = meshes.get(meshId);
    if (!mesh) {
        throw new Error(`Mesh with ID '${meshId}' not found`);
    }

    const material = mesh.material;
    if (opts.color !== undefined) {
        material.color.set(opts.color);
    }
    if (opts.wireframe !== undefined) {
        material.wireframe = opts.wireframe;
    }
    if (opts.metalness !== undefined) {
        material.metalness = opts.metalness;
    }
    if (opts.roughness !== undefined) {
        material.roughness = opts.roughness;
    }
    material.needsUpdate = true;
}

/**
 * Clear all meshes from the scene or dispose a specific mesh.
 * @param {string} [meshId] - Optional specific mesh ID to dispose
 * @returns {Promise<void>}
 */
export async function clear(meshId = null) {
    if (meshId) {
        // Dispose specific mesh
        const mesh = meshes.get(meshId);
        if (mesh) {
            disposeMesh(mesh);
            meshes.delete(meshId);
        }
    } else {
        // Clear all meshes
        meshes.forEach(mesh => disposeMesh(mesh));
        meshes.clear();
    }
}

/**
 * Dispose a single mesh and its resources.
 * @param {THREE.Mesh} mesh - Mesh to dispose
 */
function disposeMesh(mesh) {
    if (mesh.geometry) {
        mesh.geometry.dispose();
    }
    if (mesh.material) {
        if (Array.isArray(mesh.material)) {
            mesh.material.forEach(m => m.dispose());
        } else {
            mesh.material.dispose();
        }
    }
    if (scene) {
        scene.remove(mesh);
    }
}

/**
 * Dispose all viewer resources and cleanup.
 * @returns {Promise<void>}
 */
export async function dispose() {
    // Stop animation
    if (animationId) {
        cancelAnimationFrame(animationId);
        animationId = null;
    }

    // Clear all meshes
    await clear();

    // Dispose controls
    if (controls) {
        controls.dispose();
        controls = null;
    }

    // Dispose renderer
    if (renderer) {
        renderer.dispose();
        renderer = null;
    }

    // Clear references
    scene = null;
    camera = null;
    nextMeshId = 1;
}

/**
 * Resize the viewport and update camera aspect ratio.
 * @param {number} width - New width in pixels
 * @param {number} height - New height in pixels
 * @returns {Promise<void>}
 */
export async function resize(width, height) {
    if (!camera || !renderer) {
        return;
    }

    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    renderer.setSize(width, height);
}

// Expose functions to global window object for Blazor JSInterop
window.meshRenderer = {
    init,
    loadMesh,
    updateMaterial,
    clear,
    dispose,
    resize
};
