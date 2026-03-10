import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

const state = {
  renderer: null,
  scene: null,
  camera: null,
  controls: null,
  loader: null,
  modelRoot: null,
  raf: 0,
  cache: { key: null, scene: null },
  inspectLight: null,
};

const DEBUG_PREFIX = "[viewer-debug]";
const GROUP_ORDER = ["head", "body", "neck", "other"];

function debugLog(event, details = {}) {
  try {
    console.info(`${DEBUG_PREFIX} ${event}`, details);
  } catch {
    console.info(`${DEBUG_PREFIX} ${event}`);
  }
}

function setStatus(message) {
  const status = document.getElementById("model-status");
  if (status) {
    status.textContent = message;
  }
}

function modNameFromSource(source) {
  if (!source || typeof source !== "string") {
    return "Unknown Mod";
  }
  const fileName = source.split("/").pop() ?? source;
  return fileName.replace(/\.[^/.]+$/, "") || "Unknown Mod";
}

function bodyPartFromEntry(entry) {
  const match = /_(head|body|neck)\.hhh$/i.exec(entry ?? "");
  return match ? match[1].toLowerCase() : "other";
}

function assetNameFromEntry(entry) {
  const fileName = (entry ?? "").split("/").pop() ?? entry ?? "Unknown Asset";
  const trimmed = fileName
    .replace(/\.hhh$/i, "")
    .replace(/_(head|body|neck)$/i, "")
    .trim();
  return trimmed || "Unknown Asset";
}

function disposeObject(object) {
  object.traverse((child) => {
    if (!child.isMesh) {
      return;
    }
    child.geometry?.dispose?.();
    const material = child.material;
    if (Array.isArray(material)) {
      material.forEach((m) => m?.dispose?.());
    } else {
      material?.dispose?.();
    }
  });
}

function ensureViewer() {
  if (state.renderer) {
    return;
  }

  const canvasHost = document.getElementById("model-canvas");
  const width = canvasHost.clientWidth;
  const height = canvasHost.clientHeight;

  state.scene = new THREE.Scene();
  state.scene.background = new THREE.Color(0xeef4ff);

  state.camera = new THREE.PerspectiveCamera(45, width / height, 0.01, 5000);
  state.camera.position.set(1.5, 1.2, 1.5);

  state.renderer = new THREE.WebGLRenderer({ antialias: true });
  state.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  state.renderer.toneMapping = THREE.ACESFilmicToneMapping;
  state.renderer.toneMappingExposure = 1.25;
  state.renderer.setSize(width, height);
  canvasHost.appendChild(state.renderer.domElement);

  state.controls = new OrbitControls(state.camera, state.renderer.domElement);
  state.controls.enableDamping = true;
  state.controls.target.set(0, 0, 0);

  // Three-point plus hemisphere fill keeps dark materials visible in previews.
  const ambient = new THREE.AmbientLight(0xffffff, 1.15);
  const hemi = new THREE.HemisphereLight(0xe8f3ff, 0x5a6a7a, 0.75);

  const key = new THREE.DirectionalLight(0xfff7ea, 0.95);
  key.position.set(3, 4, 5);

  const fill = new THREE.DirectionalLight(0xe7f1ff, 0.8);
  fill.position.set(-3, 2.5, -2);

  const rim = new THREE.DirectionalLight(0xffffff, 0.4);
  rim.position.set(0, 2, -5);

  // Keep a front-biased light aligned with the camera to reduce inspection-time dark faces.
  const inspectLight = new THREE.DirectionalLight(0xffffff, 0.95);
  inspectLight.position.copy(state.camera.position);
  inspectLight.target.position.copy(state.controls.target);
  state.inspectLight = inspectLight;

  state.scene.add(ambient, hemi, key, fill, rim, inspectLight, inspectLight.target);

  state.loader = new GLTFLoader();

  const tick = () => {
    if (state.inspectLight) {
      state.inspectLight.position.copy(state.camera.position);
      state.inspectLight.target.position.copy(state.controls.target);
      state.inspectLight.target.updateMatrixWorld();
    }
    state.controls.update();
    state.renderer.render(state.scene, state.camera);
    state.raf = requestAnimationFrame(tick);
  };
  tick();

  window.addEventListener("resize", () => {
    if (!state.renderer) {
      return;
    }
    const w = canvasHost.clientWidth;
    const h = canvasHost.clientHeight;
    state.camera.aspect = w / h;
    state.camera.updateProjectionMatrix();
    state.renderer.setSize(w, h);
  });
}

function fitCamera(bounds) {
  if (!bounds || bounds.length !== 2) {
    state.camera.position.set(1.5, 1.2, 1.5);
    state.controls.target.set(0, 0, 0);
    state.controls.update();
    debugLog("fitCamera:fallback", { reason: "missing-or-invalid-bounds" });
    return;
  }

  const min = new THREE.Vector3(bounds[0][0], bounds[0][1], bounds[0][2]);
  const max = new THREE.Vector3(bounds[1][0], bounds[1][1], bounds[1][2]);
  const center = new THREE.Vector3().addVectors(min, max).multiplyScalar(0.5);
  const size = new THREE.Vector3().subVectors(max, min);
  const radius = Math.max(size.x, size.y, size.z) * 0.8 || 1;

  state.controls.target.copy(center);
  state.camera.position.set(center.x + radius, center.y + radius * 0.7, center.z + radius);
  state.camera.near = Math.max(radius / 500, 0.01);
  state.camera.far = Math.max(radius * 40, 100);
  state.camera.updateProjectionMatrix();
  state.controls.update();

  debugLog("fitCamera:computed", {
    bounds,
    size: [size.x, size.y, size.z],
    radius,
    camera: {
      position: [state.camera.position.x, state.camera.position.y, state.camera.position.z],
      target: [state.controls.target.x, state.controls.target.y, state.controls.target.z],
      near: state.camera.near,
      far: state.camera.far,
    },
  });
}

function swapModel(sceneObject) {
  if (state.modelRoot) {
    state.scene.remove(state.modelRoot);
    disposeObject(state.modelRoot);
  }
  state.modelRoot = sceneObject;
  state.scene.add(sceneObject);
}

function cloneCachedScene(cached) {
  return cached.clone(true);
}

async function loadModel(item) {
  ensureViewer();
  if (!item.model) {
    setStatus("This asset has no exported model.");
    debugLog("loadModel:missing-model", { hhh_entry: item.hhh_entry });
    return;
  }

  const modelUrl = `../data/outputs/${item.model}`;
  setStatus(`Loading ${item.hhh_entry}...`);
  debugLog("loadModel:start", {
    hhh_entry: item.hhh_entry,
    model: item.model,
    model_url: modelUrl,
    mesh_bounds: item.mesh_bounds,
  });

  if (state.cache.key === modelUrl && state.cache.scene) {
    swapModel(cloneCachedScene(state.cache.scene));
    fitCamera(item.mesh_bounds);
    setStatus("Model loaded (cached)");
    debugLog("loadModel:cached", { hhh_entry: item.hhh_entry, model_url: modelUrl });
    return;
  }

  try {
    const gltf = await state.loader.loadAsync(modelUrl);
    swapModel(gltf.scene);
    fitCamera(item.mesh_bounds);
    state.cache = { key: modelUrl, scene: gltf.scene.clone(true) };
    setStatus("Model loaded");
    debugLog("loadModel:success", {
      hhh_entry: item.hhh_entry,
      model_url: modelUrl,
      child_count: gltf.scene.children.length,
    });
  } catch (error) {
    setStatus(`Failed to load ${item.hhh_entry}: ${error?.message ?? "unknown error"}`);
    debugLog("loadModel:error", {
      hhh_entry: item.hhh_entry,
      model_url: modelUrl,
      error: error?.message ?? "unknown error",
    });
  }
}

function buildCard(item, modName) {
  const card = document.createElement("article");
  card.className = "card";

  const body = document.createElement("div");
  body.className = "card-body";
  body.innerHTML = `
    <h2 class="title">${item.asset_name}</h2>
    <p class="meta">${modName}</p>
  `;

  card.appendChild(body);
  card.addEventListener("click", () => {
    const active = document.querySelector(".card.active");
    if (active) {
      active.classList.remove("active");
    }
    card.classList.add("active");
    loadModel(item);
  });
  return card;
}

function groupItemsByBodyPart(items) {
  const groups = new Map();
  for (const name of GROUP_ORDER) {
    groups.set(name, []);
  }

  for (const item of items) {
    const part = bodyPartFromEntry(item.hhh_entry);
    const normalized = {
      ...item,
      body_part: part,
      asset_name: assetNameFromEntry(item.hhh_entry),
    };

    if (!groups.has(part)) {
      groups.get("other").push(normalized);
      continue;
    }
    groups.get(part).push(normalized);
  }

  return groups;
}

function renderGroupedGallery(items, modName) {
  const tabs = document.getElementById("group-tabs");
  const panel = document.getElementById("group-panel");
  tabs.innerHTML = "";
  panel.innerHTML = "";

  const groups = groupItemsByBodyPart(items);
  const availableGroups = GROUP_ORDER.filter((name) => (groups.get(name) ?? []).length > 0);

  const renderGroup = (groupName) => {
    const entries = groups.get(groupName) ?? [];
    panel.innerHTML = "";

    const header = document.createElement("header");
    header.className = "group-header";

    const title = document.createElement("h2");
    title.className = "group-title";
    title.textContent = groupName;
    header.append(title);

    const grid = document.createElement("div");
    grid.className = "group-grid";
    for (const item of entries) {
      grid.appendChild(buildCard(item, modName));
    }

    panel.append(header, grid);
  };

  for (const groupName of availableGroups) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "group-tab";
    button.role = "tab";
    button.textContent = `${groupName} (${(groups.get(groupName) ?? []).length})`;
    button.setAttribute("aria-selected", "false");
    button.addEventListener("click", () => {
      for (const tab of tabs.querySelectorAll(".group-tab")) {
        tab.setAttribute("aria-selected", "false");
      }
      button.setAttribute("aria-selected", "true");
      renderGroup(groupName);
    });
    tabs.appendChild(button);
  }

  const firstTab = tabs.querySelector(".group-tab");
  if (firstTab) {
    firstTab.setAttribute("aria-selected", "true");
    renderGroup(availableGroups[0]);
  }

  debugLog("metadata:gallery-populated", {
    card_count: items.length,
    group_counts: Object.fromEntries(GROUP_ORDER.map((name) => [name, (groups.get(name) ?? []).length])),
  });
}

async function loadMetadata() {
  try {
    const response = await fetch("../data/outputs/metadata.json");
    if (!response.ok) {
      debugLog("metadata:missing", { status: response.status });
      return;
    }

    const data = await response.json();
    const modName = modNameFromSource(data.source);
    debugLog("metadata:loaded", {
      source: data.source,
      hhh_count: data.hhh_count,
      model_count: data.model_count,
      parse_errors: data.hhh_parse_errors,
    });

    renderGroupedGallery(data.images ?? [], modName);
  } catch {
    debugLog("metadata:error", { reason: "fetch-failed" });
  }
}

loadMetadata();
