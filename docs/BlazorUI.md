# Blazor UI Architecture

 

## Overview
Blazor app structure for browsing packages, viewing details, and rendering 3D meshes via JS interop using a small Three.js wrapper.

## Components
Pages: Index (browser), ModDetail, Viewer3D
Shared: ModCard, ModFilter, LoadingSpinner

## Routing
/ → browse/search
/mod/{namespace}/{name} → detail
/mod/{namespace}/{name}/preview → 3D viewer

## State Management
Services for mod list, selection, parse progress, viewer state

## JS Interop
Three.js wrapper in wwwroot/js/meshRenderer.js

## File Index & Renderability
- After ZIP download via Worker, list entries with filename and type tags (UnityFS, SerializedFile, resS).
- Perform a shallow check per asset file to determine if it contains any `Mesh` objects; mark "Renderable: Yes/No".
- Do not render automatically. User selects a specific file, then clicks "Render" to parse and display that asset.
- Filters: "Only renderable files" and text search to quickly find items like "dragon".
- Progress: "Downloading → Indexing → Ready"; rendering starts only upon selection.

## Client Service APIs (Design)
- ZipDownloader
	- `Task<DownloadMeta?> GetMetaAsync(ModVersion v)`
	- `IAsyncEnumerable<byte[]> StreamZipAsync(ModVersion v, CancellationToken ct)`
- ZipIndexer
	- `IAsyncEnumerable<FileIndexItem> IndexAsync(IAsyncEnumerable<byte[]> zipStream, CancellationToken ct)`
- AssetScanner
	- `Task<bool> IsRenderableAsync(FileIndexItem file, CancellationToken ct)`
- AssetRenderer
	- `Task<ThreeJsGeometry> RenderAsync(FileIndexItem file, CancellationToken ct)`
- ViewerService
	- `Task ShowAsync(ThreeJsGeometry g)` and controls for submesh selection

## Three.js Interop API (Design)
- `init(canvasId, options)`
	- Creates renderer, scene, camera; sets background and controls.
- `loadMesh(geometry, groups?, materialOpts?)`
	- Builds `THREE.BufferGeometry` from typed arrays; applies optional submesh groups and material options; returns a mesh handle/id.
- `updateMaterial(meshId, materialOpts)`
	- Changes color, wireframe, metalness/roughness.
- `clear()` / `dispose(meshId?)`
	- Removes meshes and frees resources.
- `resize(width, height)`
	- Updates viewport and camera.

Geometry input contract for `loadMesh`:
- `positions`: `Float32Array` length `3 * vertexCount` (XYZ)
- `indices`: `Uint16Array | Uint32Array` length `3 * triangleCount` (triangle list)
- `normals` (optional): `Float32Array` length `3 * vertexCount`
- `uvs` (optional): `Float32Array` length `2 * vertexCount`
- `groups` (optional): array of `{ start: number, count: number, materialIndex: number }`

Notes:
- If normals are missing, wrapper can call `geometry.computeVertexNormals()`.
- Coordinate system: start with raw values; if mirrored, consider flipping Z or adjusting triangle winding.