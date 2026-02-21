import { getAssetById, getAvatarById } from "./assetStore.js";

const previewUrlsByElement = new Map();
const previewViewersInContainer = new Map();
let modelViewerLoadPromise;

function log(level, message, details = null) {
  const payload = details ? { details } : undefined;
  if (level === "error") {
    console.error(`[previewInterop] ${message}`, payload ?? "");
    return;
  }

  if (level === "warn") {
    console.warn(`[previewInterop] ${message}`, payload ?? "");
    return;
  }

  console.info(`[previewInterop] ${message}`, payload ?? "");
}

function getElement(elementId) {
  const element = document.getElementById(elementId);
  return element ?? null;
}

function revokePreviewUrl(elementId) {
  const existingUrl = previewUrlsByElement.get(elementId);
  if (!existingUrl) {
    return;
  }

  URL.revokeObjectURL(existingUrl);
  previewUrlsByElement.delete(elementId);
}

async function ensureModelViewerLoaded() {
  if (customElements.get("model-viewer")) {
    return true;
  }

  if (!modelViewerLoadPromise) {
    modelViewerLoadPromise = import("https://cdn.jsdelivr.net/npm/@google/model-viewer/dist/model-viewer.min.js")
      .then(() => true)
      .catch(() => false);
  }

  return modelViewerLoadPromise;
}

function parseGlbJson(glbBytes) {
  if (!(glbBytes instanceof ArrayBuffer) || glbBytes.byteLength < 20) {
    return null;
  }

  const view = new DataView(glbBytes);
  const magic = view.getUint32(0, true);
  const jsonChunkLength = view.getUint32(12, true);
  const jsonChunkType = view.getUint32(16, true);
  if (magic !== 0x46546c67 || jsonChunkType !== 0x4e4f534a) {
    return null;
  }

  const jsonStart = 20;
  const jsonEnd = jsonStart + jsonChunkLength;
  if (jsonEnd > glbBytes.byteLength) {
    return null;
  }

  const jsonBytes = new Uint8Array(glbBytes, jsonStart, jsonChunkLength);
  const jsonText = new TextDecoder("utf-8").decode(jsonBytes).trim();
  try {
    return JSON.parse(jsonText);
  } catch {
    return null;
  }
}

function hasRenderableMesh(glbJson) {
  if (!glbJson || !Array.isArray(glbJson.meshes) || glbJson.meshes.length === 0) {
    return false;
  }

  return glbJson.meshes.some((mesh) =>
    Array.isArray(mesh?.primitives) && mesh.primitives.length > 0
  );
}

function extractConversionWarning(glbJson) {
  const warnings = glbJson?.extras?.conversionWarnings;
  if (!Array.isArray(warnings) || warnings.length === 0) {
    return null;
  }

  const firstWarning = warnings[0];
  return typeof firstWarning === "string" ? firstWarning : null;
}

function buildResult(success, reason = null) {
  return { success, reason };
}

export async function previewAsset(elementId, assetId) {
  log("info", "previewAsset start", { elementId, assetId });

  const element = getElement(elementId);
  if (!element) {
    log("error", "preview element not found", { elementId });
    return buildResult(false, "Preview element not found.");
  }

  const modelViewerReady = await ensureModelViewerLoaded();
  if (!modelViewerReady) {
    clearPreview(elementId);
    log("warn", "model-viewer failed to load", { assetId });
    return buildResult(false, "Preview renderer failed to load.");
  }

  const asset = await getAssetById(assetId);
  if (!asset?.glb) {
    clearPreview(elementId);
    log("warn", "asset has no GLB payload", { assetId });
    return buildResult(false, "Selected item has no stored GLB data.");
  }

  const glbBytes = asset.glb instanceof ArrayBuffer
    ? asset.glb
    : asset.glb.buffer instanceof ArrayBuffer
      ? asset.glb.buffer.slice(asset.glb.byteOffset, asset.glb.byteOffset + asset.glb.byteLength)
      : null;

  if (!glbBytes) {
    clearPreview(elementId);
    log("warn", "asset GLB payload invalid", { assetId });
    return buildResult(false, "Stored GLB payload is invalid.");
  }

  const glbJson = parseGlbJson(glbBytes);
  if (!hasRenderableMesh(glbJson)) {
    clearPreview(elementId);
    const warning = extractConversionWarning(glbJson);
    log("warn", "GLB has no renderable mesh", { assetId, warning });
    return buildResult(
      false,
      warning ?? "GLB has no renderable mesh yet (parser decode incomplete for this item)."
    );
  }

  const blob = new Blob([glbBytes], { type: "model/gltf-binary" });
  const nextUrl = URL.createObjectURL(blob);

  revokePreviewUrl(elementId);
  element.src = nextUrl;
  previewUrlsByElement.set(elementId, nextUrl);
  log("info", "previewAsset success", { assetId });
  return buildResult(true);
}

export function clearPreview(elementId) {
  const element = getElement(elementId);
  if (element) {
    element.removeAttribute("src");
  }

  revokePreviewUrl(elementId);
  log("info", "preview cleared", { elementId });
}

export async function previewMultipleAssets(containerId, assetIds, avatarId = null) {
  log("info", "previewMultipleAssets start", { containerId, assetCount: assetIds?.length ?? 0, avatarId });

  const container = getElement(containerId);
  if (!container) {
    log("error", "preview container not found", { containerId });
    return buildResult(false, "Preview container not found.");
  }

  const modelViewerReady = await ensureModelViewerLoaded();
  if (!modelViewerReady) {
    clearAllPreviews(containerId);
    log("warn", "model-viewer failed to load");
    return buildResult(false, "Preview renderer failed to load.");
  }

  if ((!assetIds || assetIds.length === 0) && !avatarId) {
    clearAllPreviews(containerId);
    log("info", "no assets or avatar to preview");
    return buildResult(false, "No assets or avatar selected.");
  }

  clearAllPreviews(containerId);

  const loadedViewers = [];
  const failedAssets = [];

  // Load avatar as base layer if provided
  if (avatarId) {
    const avatar = await getAvatarById(avatarId);
    if (!avatar?.glb) {
      log("warn", "avatar has no GLB payload", { avatarId });
    } else {
      const glbBytes = avatar.glb instanceof ArrayBuffer
        ? avatar.glb
        : avatar.glb.buffer instanceof ArrayBuffer
          ? avatar.glb.buffer.slice(avatar.glb.byteOffset, avatar.glb.byteOffset + avatar.glb.byteLength)
          : null;

      if (glbBytes) {
        const glbJson = parseGlbJson(glbBytes);
        if (hasRenderableMesh(glbJson)) {
          const blob = new Blob([glbBytes], { type: "model/gltf-binary" });
          const url = URL.createObjectURL(blob);

          const viewer = document.createElement("model-viewer");
          viewer.setAttribute("camera-controls", "");
          viewer.setAttribute("auto-rotate", "");
          viewer.setAttribute("touch-action", "pan-y");
          viewer.style.position = "absolute";
          viewer.style.inset = "0";
          viewer.style.width = "100%";
          viewer.style.height = "100%";
          viewer.style.background = "transparent";
          viewer.className = "planner-model-viewer planner-model-viewer--avatar";
          viewer.src = url;

          container.appendChild(viewer);
          loadedViewers.push({ viewer, url, assetId: avatarId, isAvatar: true });
          previewUrlsByElement.set(avatarId, url);
          log("info", "avatar loaded", { avatarId });
        } else {
          log("warn", "avatar GLB has no renderable mesh", { avatarId });
        }
      } else {
        log("warn", "avatar GLB payload invalid", { avatarId });
      }
    }
  }

  // Load decoration assets on top
  if (assetIds && assetIds.length > 0) {
    for (const assetId of assetIds) {
      const asset = await getAssetById(assetId);
      if (!asset?.glb) {
        log("warn", "asset has no GLB payload", { assetId });
        failedAssets.push(assetId);
        continue;
      }

      const glbBytes = asset.glb instanceof ArrayBuffer
        ? asset.glb
        : asset.glb.buffer instanceof ArrayBuffer
          ? asset.glb.buffer.slice(asset.glb.byteOffset, asset.glb.byteOffset + asset.glb.byteLength)
          : null;

      if (!glbBytes) {
        log("warn", "asset GLB payload invalid", { assetId });
        failedAssets.push(assetId);
        continue;
      }

      const glbJson = parseGlbJson(glbBytes);
      if (!hasRenderableMesh(glbJson)) {
        const warning = extractConversionWarning(glbJson);
        log("warn", "GLB has no renderable mesh", { assetId, warning });
        failedAssets.push(assetId);
        continue;
      }

      const blob = new Blob([glbBytes], { type: "model/gltf-binary" });
      const url = URL.createObjectURL(blob);

      const viewer = document.createElement("model-viewer");
      viewer.setAttribute("camera-controls", "");
      viewer.setAttribute("auto-rotate", "");
      viewer.setAttribute("touch-action", "pan-y");
      viewer.style.position = "absolute";
      viewer.style.inset = "0";
      viewer.style.width = "100%";
      viewer.style.height = "100%";
      viewer.style.background = "transparent";
      viewer.className = "planner-model-viewer planner-model-viewer--multi";
      viewer.src = url;

      container.appendChild(viewer);
      loadedViewers.push({ viewer, url, assetId, isAvatar: false });
      previewUrlsByElement.set(assetId, url);
    }
  }

  previewViewersInContainer.set(containerId, loadedViewers);

  if (loadedViewers.length === 0) {
    log("warn", "no valid assets or avatar to preview", { assetIds, avatarId, failedAssets });
    return buildResult(false, "No valid assets or avatar could be loaded for preview.");
  }

  const avatarLoaded = loadedViewers.some(v => v.isAvatar);
  const assetsLoaded = loadedViewers.filter(v => !v.isAvatar).length;
  const successMessage = avatarLoaded
    ? (assetsLoaded > 0
      ? `Loaded avatar + ${assetsLoaded} decoration(s).`
      : `Loaded avatar.`)
    : `Loaded ${assetsLoaded} decoration(s).`;

  log("info", "previewMultipleAssets success", { loadedCount: loadedViewers.length, failedCount: failedAssets.length });
  return buildResult(true, successMessage);
}

export function clearAllPreviews(containerId) {
  const container = getElement(containerId);
  if (!container) {
    log("warn", "preview container not found for clear", { containerId });
    return;
  }

  const viewers = previewViewersInContainer.get(containerId);
  if (viewers) {
    for (const { viewer, url, assetId } of viewers) {
      viewer.remove();
      URL.revokeObjectURL(url);
      previewUrlsByElement.delete(assetId);
    }
    previewViewersInContainer.delete(containerId);
  }

  log("info", "all previews cleared", { containerId });
}
