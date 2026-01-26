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

## Three.js Interop API (Implemented)

### JavaScript Functions (wwwroot/js/meshRenderer.js)

All functions are exposed via `window.meshRenderer` for Blazor JSInterop.

#### `init(canvasId, options)`
Creates renderer, scene, camera, lighting, and OrbitControls.

**Parameters:**
- `canvasId` (string): ID of the canvas element
- `options` (object, optional):
  - `fov` (number): Camera field of view (default: 60)
  - `near` (number): Camera near plane (default: 0.1)
  - `far` (number): Camera far plane (default: 1000)
  - `background` (number): Background color hex (default: 0x1a1a1a)

**Returns:** `Promise<void>`

**Throws:** Error if canvas not found or already initialized

#### `loadMesh(geometry, groups, materialOpts)`
Builds `THREE.BufferGeometry` from typed arrays and adds mesh to scene.

**Parameters:**
- `geometry` (object):
  - `positions` (Float32Array): Vertex positions, length `3 * vertexCount` (XYZ)
  - `indices` (Uint16Array | Uint32Array): Triangle indices, length `3 * triangleCount`
  - `normals` (Float32Array, optional): Vertex normals, length `3 * vertexCount`
  - `uvs` (Float32Array, optional): Texture coordinates, length `2 * vertexCount` (UV)
- `groups` (Array, optional): Submesh groups for multi-material meshes
  - Each group: `{ start: number, count: number, materialIndex: number }`
- `materialOpts` (object, optional):
  - `color` (string | number): Material color (default: 0x888888)
  - `wireframe` (boolean): Wireframe mode (default: false)
  - `metalness` (number): Metalness 0-1 (default: 0.5)
  - `roughness` (number): Roughness 0-1 (default: 0.5)

**Returns:** `Promise<string>` - Mesh ID for future operations

**Throws:** Error if renderer not initialized or geometry invalid

**Notes:**
- If normals are missing, calls `geometry.computeVertexNormals()` automatically
- Centers camera on mesh bounding box after loading
- Material uses `THREE.MeshStandardMaterial` with double-sided rendering
- Validates that indices length is divisible by 3 (proper triangle list)

#### `clearMesh(meshId)`
Removes a specific mesh from the scene and frees its resources.

**Parameters:**
- `meshId` (string): Mesh ID to dispose

**Notes:**
- Disposes geometry and material resources
- Removes mesh from Three.js scene

#### `clear()`
Removes all meshes from the scene.

**Notes:**
- Disposes geometry and material resources for all meshes
- Removes all meshes from Three.js scene

#### `dispose()`
Cleans up all viewer resources.

**Notes:**
- Stops animation loop
- Clears all meshes
- Disposes controls and renderer
- Resets module state
- **Important:** After calling dispose, the service cannot be reinitialized. A new service instance or page reload is required.

#### `resize(width, height)`
Updates viewport and camera aspect ratio.

**Parameters:**
- `width` (number): New width in pixels
- `height` (number): New height in pixels

### C# Service (ViewerService)

The `ViewerService` class provides C# bindings to the JavaScript functions via `IJSRuntime`.

**Thread Safety:** This service is not thread-safe. Callers must ensure methods are not invoked concurrently from multiple threads.

**Lifecycle:** This service is designed for single-use. After calling `DisposeAsync`, the service cannot be reinitialized and a new instance must be created.

**Usage Example:**
```csharp
// Initialize viewer
await viewerService.InitializeAsync("viewer-canvas");

// Create geometry
var geometry = new ThreeJsGeometry
{
    Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f },
    Indices = new uint[] { 0, 1, 2 },
    VertexCount = 3,
    TriangleCount = 1,
    Normals = new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }
};

// Show mesh
string meshId = await viewerService.ShowAsync(geometry);

// Update material
await viewerService.UpdateMaterialAsync(meshId, "#FF0000", wireframe: true);

// Clear mesh
await viewerService.ClearAsync();

// Cleanup
await viewerService.DisposeAsync();
```

### Data Models

#### `ThreeJsGeometry`
```csharp
public sealed class ThreeJsGeometry
{
    public required float[] Positions { get; init; }
    public required uint[] Indices { get; init; }
    public float[]? Normals { get; init; }
    public float[]? Uvs { get; init; }
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
    public List<SubMeshGroup>? Groups { get; init; }
}
```

#### `SubMeshGroup`
```csharp
public sealed class SubMeshGroup
{
    public required int Start { get; init; }
    public required int Count { get; init; }
    public required int MaterialIndex { get; init; }
}
```

### Implementation Notes

- **Three.js Version:** v0.170.0 loaded via CDN with ES module imports
  - **CDN Dependency:** Production deployments should consider local hosting or fallback mechanisms if CDN is unavailable
- **Coordinate System:** Uses raw Unity values; no coordinate flipping applied
- **Array Marshaling:** Blazor automatically converts C# arrays to JavaScript typed arrays
- **Animation Loop:** Runs continuously via `requestAnimationFrame` for smooth controls
- **Lighting:** Scene includes ambient light (0.6 intensity) and directional light (0.8 intensity)
- **Controls:** OrbitControls with damping enabled, min distance 0.5, max distance 500
- **Function Signatures:** All JavaScript functions are synchronous (not async) as they perform only synchronous Three.js operations

### Testing

- **Unit Tests:** `ViewerServiceTests` verifies all JSInterop calls with mocked `IJSRuntime`
- **Lint:** JavaScript passes ESLint 9 with cognitive complexity limit of 10
- **Format:** Code follows Prettier formatting rules

### Future Enhancements

- Add support for multiple materials via groups array
- Implement UI controls for material property adjustments
- Add support for texture loading from Unity asset bundles
- Implement mesh selection and highlighting
- Add animation support for skinned meshes