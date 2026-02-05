# glTF Exporter Architecture Design

## Overview
Design the C# → glTF/GLB export pipeline for converting parsed Unity `Mesh` objects to GLB format for efficient Three.js rendering.

**Decision**: Use **SharpGLTF** (see [GltfExporterResearch.md](GltfExporterResearch.md))

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Blazor WASM Frontend                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Viewer3D Page / ModDetail Page                       │   │
│  └─────────────────────────────────────────────────────┐│   │
│                                 │                        ││   │
│                                 ▼                        ││   │
│  ┌────────────────────────────────────────────────────┘│   │
│  │ GltfExportService (BlazorApp/Services/)             │   │
│  │  - ExportMesheToGlb(List<Mesh>) → byte[]           │   │
│  │  - CacheGlb(mod, version, glbData)                  │   │
│  │  - LoadCachedGlb(mod, version) → byte[]?          │   │
│  └────────────────┬─────────────────────────────────────┘   │
│                   │                                         │   │
│                   ▼                                         │   │
│  ┌────────────────────────────────────────────────────────┐   │
│  │ GltfExporter (UnityAssetParser/Export/)               │   │
│  │  - MeshesToGltf(List<Mesh>) → ModelRoot             │   │
│  │    ├─ CreatePrimitive per submesh                   │   │
│  │    ├─ Map vertices/normals/uvs → accessors          │   │
│  │    └─ Set material/metadata                         │   │
│  │  - ModelRootToGlb(ModelRoot) → byte[]              │   │
│  └────────────────┬────────────────────────────────────┬─┐   │
│                   │                                    │ │   │
│                   ▼                                    │ │   │
│  ┌────────────────────────────────────────────────────┘ │   │
│  │ SharpGLTF.CommandLine                               │   │
│  │  - ModelRoot.CreateInstance()                       │   │
│  │  - mesh.CreatePrimitive()                           │   │
│  │  - model.SaveAsGLB(stream)                          │   │
│  └───────────────────────────────────────────────────────┘   │
│                                          ▲                   │
│                                          │                   │
│  ┌─────────────────────────────────────┴────────────────┐   │
│  │ Parsed Mesh Objects (from MeshExtractionService)    │   │
│  │  - Vertices: Vector3[]                               │   │
│  │  - Normals: Vector3[]                                │   │
│  │  - UVs: Vector2[]                                    │   │
│  │  - Indices: uint[]                                   │   │
│  │  - Submeshes: SubMesh[]                              │   │
│  └────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
               ▼ (GLB bytes)
         Three.js GLTFLoader
               ▼
         Render in Scene
```

## Class Structure

### 1. **GltfExporter.cs** (UnityAssetParser/Export/)

**Purpose**: Convert parsed Unity Mesh → glTF ModelRoot

```csharp
namespace UnityAssetParser.Export;

/// <summary>
/// Exports Unity Mesh objects to glTF 2.0 format.
/// </summary>
public class GltfExporter
{
    /// <summary>
    /// Converts a list of parsed Mesh objects to glTF ModelRoot.
    /// Each Mesh becomes a separate Mesh node with submeshes as primitives.
    /// </summary>
    public ModelRoot MeshesToGltf(List<Mesh> meshes)
    {
        var model = ModelRoot.CreateInstance();
        
        foreach (var mesh in meshes)
        {
            AddMeshToModel(model, mesh);
        }
        
        return model;
    }
    
    /// <summary>
    /// Adds a single Mesh (with submeshes) to the glTF model.
    /// Creates a glTF Mesh with Primitives for each submesh.
    /// </summary>
    private void AddMeshToModel(ModelRoot model, Mesh mesh)
    {
        // Create glTF Mesh
        var gltfMesh = model.CreateMesh(mesh.m_Name);
        
        // Add each submesh as a separate primitive
        for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
        {
            var submesh = mesh.m_SubMeshes[i];
            AddSubmeshPrimitive(gltfMesh, mesh, submesh, i);
        }
    }
    
    /// <summary>
    /// Adds a submesh as a glTF Primitive to a Mesh.
    /// </summary>
    private void AddSubmeshPrimitive(
        SharpGLTF.Schema2.Mesh gltfMesh, 
        Mesh unityMesh, 
        SubMesh submesh, 
        int submeshIndex)
    {
        // Create primitive
        var primitive = gltfMesh.CreatePrimitive();
        
        // Add vertex positions (required)
        if (unityMesh.m_Vertices != null && unityMesh.m_Vertices.Length > 0)
        {
            var positions = unityMesh.m_Vertices
                .Select(v => new System.Numerics.Vector3(v.X, v.Y, v.Z))
                .ToArray();
            primitive.WithVertexAccessor("POSITION", positions);
        }
        
        // Add normals (optional but recommended)
        if (unityMesh.m_Normals != null && unityMesh.m_Normals.Length > 0)
        {
            var normals = unityMesh.m_Normals
                .Select(n => new System.Numerics.Vector3(n.X, n.Y, n.Z))
                .ToArray();
            primitive.WithVertexAccessor("NORMAL", normals);
        }
        
        // Add UVs (optional)
        if (unityMesh.m_UV0 != null && unityMesh.m_UV0.Length > 0)
        {
            var uvs = unityMesh.m_UV0
                .Select(uv => new System.Numerics.Vector2(uv.X, uv.Y))
                .ToArray();
            primitive.WithVertexAccessor("TEXCOORD_0", uvs);
        }
        
        // Add indices
        if (submesh.indexBuffer != null)
        {
            primitive.WithIndexAccessor("INDICES", submesh.indexBuffer);
        }
        
        // Add material (basic for now)
        var material = model.UseDefaultMaterial();
        primitive.Material = material;
        
        // Optional: metadata for debugging
        // primitive.Name = $"{unityMesh.m_Name}_submesh_{submeshIndex}";
    }
    
    /// <summary>
    /// Exports glTF ModelRoot to GLB binary format (byte array).
    /// </summary>
    public byte[] ExportToGlb(ModelRoot model)
    {
        using var memoryStream = new MemoryStream();
        model.SaveAsGLB(memoryStream);
        return memoryStream.ToArray();
    }
    
    /// <summary>
    /// Convenience method: Meshes → glTF → GLB in one call.
    /// </summary>
    public byte[] MeshesToGlb(List<Mesh> meshes)
    {
        var model = MeshesToGltf(meshes);
        return ExportToGlb(model);
    }
}
```

### 2. **GltfExportService.cs** (BlazorApp/Services/)

**Purpose**: Service layer integrating exporter with caching & error handling

```csharp
namespace BlazorApp.Services;

/// <summary>
/// Service for exporting parsed Mesh objects to glTF/GLB format.
/// Integrates caching, error handling, and logging.
/// </summary>
public class GltfExportService : IGltfExportService
{
    private readonly ILogger<GltfExportService> _logger;
    private readonly GltfExporter _exporter;
    
    public GltfExportService(ILogger<GltfExportService> logger)
    {
        _logger = logger;
        _exporter = new GltfExporter();
    }
    
    /// <summary>
    /// Exports meshes to GLB, with IndexedDB caching.
    /// </summary>
    public async Task<byte[]> ExportMeshesToGlbAsync(
        List<Mesh> meshes, 
        string cacheKey)
    {
        try
        {
            // Check cache first
            var cached = await LoadCachedGlbAsync(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Loaded glTF from cache: {CacheKey}", cacheKey);
                return cached;
            }
            
            // Export
            _logger.LogInformation("Exporting {MeshCount} meshes to glTF for {CacheKey}", 
                meshes.Count, cacheKey);
            var glbData = _exporter.MeshesToGlb(meshes);
            
            // Cache result
            await CacheGlbAsync(cacheKey, glbData);
            _logger.LogInformation("Cached glTF export: {CacheKey}, size: {Size} bytes", 
                cacheKey, glbData.Length);
            
            return glbData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export meshes to glTF: {CacheKey}", cacheKey);
            throw;
        }
    }
    
    /// <summary>
    /// Load glTF from browser IndexedDB.
    /// </summary>
    public async Task<byte[]?> LoadCachedGlbAsync(string cacheKey)
    {
        // TODO: Implement IndexedDB lookup via JS interop
        // For now, return null (will be implemented in Task 6)
        return null;
    }
    
    /// <summary>
    /// Save glTF to browser IndexedDB.
    /// </summary>
    public async Task CacheGlbAsync(string cacheKey, byte[] glbData)
    {
        // TODO: Implement IndexedDB storage via JS interop
    }
}

/// <summary>
/// Service interface for glTF export & caching.
/// </summary>
public interface IGltfExportService
{
    Task<byte[]> ExportMeshesToGlbAsync(List<Mesh> meshes, string cacheKey);
    Task<byte[]?> LoadCachedGlbAsync(string cacheKey);
    Task CacheGlbAsync(string cacheKey, byte[] glbData);
}
```

### 3. **IGltfExportService.cs** (Interface)

Already included in GltfExportService.cs above.

## Data Mappings

### Unity Mesh → glTF Primitive

| Unity Field | glTF Semantic | Type | Notes |
|------------|-----------|------|-------|
| `m_Vertices` | `POSITION` | `Vec3` | Required; already normalized |
| `m_Normals` | `NORMAL` | `Vec3` | Optional but recommended for lighting |
| `m_UV0` | `TEXCOORD_0` | `Vec2` | Optional; UV channel 0 |
| `m_Colors` | `COLOR_0` | `Vec4` | Optional; not extracted yet |
| `submesh.indexBuffer` | `INDICES` | `Uint16/Uint32` | Depends on vertex count |
| `m_Tangents` | `TANGENT` | `Vec4` | Optional; not extracted yet |

### Material Mapping

**Phase 2** (now): Use default glTF material (white, no texture)  
**Future (Phase 3)**: Add basic PBR material:
- Base color: White (or extract from Mesh if available)
- Metallic: 0.0
- Roughness: 0.7 (default plastic-like)

## Service Integration Points

### 1. **AssetRenderer Integration**

Current flow:
```csharp
// In AssetRendererService.cs
var renderer = new MeshExtractionService();
var meshes = renderer.ExtractMeshes(deserializedNode);
// → Currently exports to JSON

// New flow:
var exportService = new GltfExportService(logger);
var glbData = await exportService.ExportMeshesToGlbAsync(
    meshes, 
    cacheKey: $"{modName}:{assetPath}"
);
// → Send GLB blob to client
```

### 2. **Viewer3D Page Integration**

Current:
```csharp
// IN Viewer3D.razor
var meshJson = await service.GetMeshJsonAsync(modKey);
// Pass to Three.js via JS interop

// New:
var glbBlob = await service.GetMeshGlbAsync(modKey);
// Pass to Three.js GLTFLoader
```

## Testing Strategy

### Unit Tests
**File**: `Tests/UnityAssetParser.Tests/Export/GltfExporterTests.cs`

```csharp
[Fact]
public void MeshesToGltf_SingleMesh_CreatesValidModel()
{
    // Arrange
    var mesh = CreateTestMesh(vertexCount: 12, indexCount: 36);
    var exporter = new GltfExporter();
    
    // Act
    var model = exporter.MeshesToGltf(new[] { mesh }.ToList());
    
    // Assert
    Assert.NotNull(model);
    Assert.Single(model.LogicalMeshes);
    Assert.DataMatches(mesh, model.LogicalMeshes[0]); // Custom assertion
}

[Fact]
public void MeshesToGlb_ExportsValidBinary()
{
    // Arrange
    var mesh = CreateTestMesh(...);
    var exporter = new GltfExporter();
    
    // Act
    var glbBytes = exporter.MeshesToGlb(new[] { mesh }.ToList());
    
    // Assert
    Assert.StartsWith(Encoding.ASCII.GetBytes("glTF"), glbBytes); // glTF magic
    Assert.True(glbBytes.Length > 0);
}
```

### Integration Tests
**File**: `Tests/UnityAssetParser.Tests/Integration/GltfExportIntegrationTests.cs`

```csharp
[Fact(Skip = "Requires real bundle fixture")]
public async Task RealBundle_ExportsToValidGlb()
{
    // Load real bundle (Cigar test fixture)
    var bundle = BundleFile.Load("Fixtures/Cigar.hhh");
    var serialized = bundle.GetPrimarySerializedFile();
    var meshes = new MeshExtractionService().ExtractMeshes(serialized);
    
    // Export to GLB
    var exporter = new GltfExporter();
    var glbData = exporter.MeshesToGlb(meshes);
    
    // Validate with Three.js GLTFLoader (via snapshot test)  
    var glbUrl = "data:application/octet-stream;base64," + Convert.ToBase64String(glbData);
    var canLoad = await ValidateWithThreeJsLoader(glbUrl);
    
    Assert.True(canLoad, "GLTFLoader should successfully load exported GLB");
}
```

### Snapshot Testing
- Export test meshes to GLB
- Reference snapshot: `Fixtures/expected_mesh.glb`
- Validation: Binary diff (GLB format is determined, should be identical)

## Implementation Checklist

- [ ] **Task 3**: Implement `GltfExporter.cs`
  - [ ] Install SharpGLTF NuGet
  - [ ] Core export logic
  - [ ] Submesh handling
  - [ ] Basic material assignment
  - [ ] GLB binary export

- [ ] **Task 4**: Create `GltfExportService.cs`
  - [ ] Wrapper service
  - [ ] Error handling & logging
  - [ ] Cache interface stubs (full impl later)
  - [ ] DI registration in `Program.cs`

- [ ] **Task 7**: Unit & Integration Tests
  - [ ] Exporter tests
  - [ ] Service tests
  - [ ] Snapshot validation

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| glTF validation spec complexity | Use SharpGLTF library (battle-tested) |
| Submesh boundary alignment issues | Unit tests with known fixtures |
| Material loss (no export yet) | Document as Phase 3 enhancement |
| IndexedDB caching (Task 6) blocks Phase 3 | Implement caching interface separately |

## Timeline
- **Phase 2 (Design)**: 1-2 hours ✅ (this doc)
- **Phase 3 (Implementation)**: 4-6 hours (divide across tasks 3-7)

---

## References

- **SharpGLTF**: https://github.com/vpenades/SharpGLTF
- **glTF 2.0 Spec**: https://registry.khronos.org/glTF/specs/2.0/
- **Three.js GLTFLoader**: https://threejs.org/docs/#examples/en/loaders/GLTFLoader

