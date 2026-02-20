import { getAssetById } from "./assetStore.js";

const previewUrlsByElement = new Map();

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

export async function previewAsset(elementId, assetId) {
  const element = getElement(elementId);
  if (!element) {
    return false;
  }

  const asset = await getAssetById(assetId);
  if (!asset?.glb) {
    clearPreview(elementId);
    return false;
  }

  const glbBytes = asset.glb instanceof ArrayBuffer
    ? asset.glb
    : asset.glb.buffer instanceof ArrayBuffer
      ? asset.glb.buffer.slice(asset.glb.byteOffset, asset.glb.byteOffset + asset.glb.byteLength)
      : null;

  if (!glbBytes) {
    clearPreview(elementId);
    return false;
  }

  const blob = new Blob([glbBytes], { type: "model/gltf-binary" });
  const nextUrl = URL.createObjectURL(blob);

  revokePreviewUrl(elementId);
  element.src = nextUrl;
  previewUrlsByElement.set(elementId, nextUrl);
  return true;
}

export function clearPreview(elementId) {
  const element = getElement(elementId);
  if (element) {
    element.removeAttribute("src");
  }

  revokePreviewUrl(elementId);
}
