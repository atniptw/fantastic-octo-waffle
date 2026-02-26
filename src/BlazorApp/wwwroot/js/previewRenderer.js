/* global atob, document, window, ResizeObserver, cancelAnimationFrame, requestAnimationFrame */

const THREE_VERSION = "0.165.0";
const threeModuleUrl = `https://unpkg.com/three@${THREE_VERSION}/build/three.module.js`;
const gltfLoaderUrl = `https://unpkg.com/three@${THREE_VERSION}/examples/jsm/loaders/GLTFLoader.js`;

const states = new Map();
let cachedModulesPromise;

function loadModules() {
    if (!cachedModulesPromise) {
        cachedModulesPromise = Promise.all([
            import(threeModuleUrl),
            import(gltfLoaderUrl)
        ]);
    }

    return cachedModulesPromise;
}

function decodeBase64ToUint8Array(base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) {
        bytes[index] = binary.charCodeAt(index);
    }

    return bytes;
}

function clearHost(host, message) {
    host.innerHTML = "";
    const placeholder = document.createElement("div");
    placeholder.className = "preview-empty";
    placeholder.textContent = message;
    host.appendChild(placeholder);
}

function ensureState(hostId) {
    const existing = states.get(hostId);
    if (existing) {
        return existing;
    }

    const state = {
        hostId,
        renderer: null,
        scene: null,
        camera: null,
        modelRoot: null,
        frameRequestId: 0,
        resizeObserver: null,
        modelRotationStep: 0
    };

    states.set(hostId, state);
    return state;
}

function disposeState(state) {
    if (state.frameRequestId) {
        cancelAnimationFrame(state.frameRequestId);
        state.frameRequestId = 0;
    }

    if (state.resizeObserver) {
        state.resizeObserver.disconnect();
        state.resizeObserver = null;
    }

    if (state.renderer) {
        state.renderer.dispose();
        if (state.renderer.domElement?.parentElement) {
            state.renderer.domElement.parentElement.removeChild(state.renderer.domElement);
        }
    }

    state.renderer = null;
    state.scene = null;
    state.camera = null;
    state.modelRoot = null;
}

function startRenderLoop(state) {
    if (!state.renderer || !state.scene || !state.camera) {
        return;
    }

    const renderFrame = () => {
        if (!state.renderer || !state.scene || !state.camera) {
            return;
        }

        if (state.modelRoot) {
            state.modelRotationStep += 0.004;
            state.modelRoot.rotation.y = state.modelRotationStep;
        }

        state.renderer.render(state.scene, state.camera);
        state.frameRequestId = requestAnimationFrame(renderFrame);
    };

    if (state.frameRequestId) {
        cancelAnimationFrame(state.frameRequestId);
    }

    state.frameRequestId = requestAnimationFrame(renderFrame);
}

export async function clearPreview(hostId, message) {
    const host = document.getElementById(hostId);
    if (!host) {
        return;
    }

    const state = ensureState(hostId);
    disposeState(state);
    clearHost(host, message || "Preview is not available.");
}

export async function renderGlb(hostId, glbBase64) {
    const host = document.getElementById(hostId);
    if (!host) {
        return;
    }

    if (!glbBase64 || typeof glbBase64 !== "string") {
        await clearPreview(hostId, "No GLB output available.");
        return;
    }

    const [THREE, loaderModule] = await loadModules();
    const { GLTFLoader } = loaderModule;

    const state = ensureState(hostId);
    disposeState(state);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(window.devicePixelRatio || 1);

    const hostWidth = Math.max(host.clientWidth, 280);
    const hostHeight = Math.max(host.clientHeight, 220);
    renderer.setSize(hostWidth, hostHeight);

    host.innerHTML = "";
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(45, hostWidth / hostHeight, 0.01, 1000);

    scene.add(new THREE.AmbientLight(0xffffff, 1.1));
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.2);
    keyLight.position.set(2.5, 3.5, 4.5);
    scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight(0xffffff, 0.6);
    fillLight.position.set(-3, 2, -2.5);
    scene.add(fillLight);

    state.renderer = renderer;
    state.scene = scene;
    state.camera = camera;
    state.modelRotationStep = 0;

    state.resizeObserver = new ResizeObserver(() => {
        if (!state.renderer || !state.camera) {
            return;
        }

        const width = Math.max(host.clientWidth, 280);
        const height = Math.max(host.clientHeight, 220);
        state.renderer.setSize(width, height);
        state.camera.aspect = width / height;
        state.camera.updateProjectionMatrix();
    });
    state.resizeObserver.observe(host);

    const bytes = decodeBase64ToUint8Array(glbBase64);
    const loader = new GLTFLoader();

    await new Promise((resolve, reject) => {
        loader.parse(
            bytes.buffer,
            "",
            (gltf) => {
                state.modelRoot = gltf.scene;
                state.scene.add(gltf.scene);
                const box = new THREE.Box3().setFromObject(gltf.scene);
                const size = box.getSize(new THREE.Vector3());
                const center = box.getCenter(new THREE.Vector3());
                const maxSize = Math.max(size.x, size.y, size.z) || 1;
                const fovInRadians = (state.camera.fov * Math.PI) / 180;
                const distance = maxSize / Math.tan(fovInRadians / 2);

                state.camera.position.set(center.x + distance * 0.75, center.y + distance * 0.45, center.z + distance * 0.95);
                state.camera.near = Math.max(distance / 200, 0.01);
                state.camera.far = distance * 20;
                state.camera.lookAt(center);
                state.camera.updateProjectionMatrix();

                resolve();
            },
            (error) => reject(error)
        );
    });

    startRenderLoop(state);
}

export function disposePreview(hostId) {
    const state = states.get(hostId);
    if (!state) {
        return;
    }

    disposeState(state);
    states.delete(hostId);
}
