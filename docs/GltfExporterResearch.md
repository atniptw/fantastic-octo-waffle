# glTF Export Library Research & Decision Document

## Objective
Evaluate C# libraries for converting parsed Unity `Mesh` objects to glTF/GLB format for efficient Three.js rendering.

## Requirements
- **Input**: Parsed `Mesh` objects (vertices, indices, normals, UVs, submeshes)
- **Output**: GLB binary format (or glTF + .bin)
- **Integration**: WASM-compatible (runs in Blazor)
- **Scope**: Static meshes only, basic materials (no animations/rigs)
- **Constraints**:
  - Minimal additional dependencies
  - Deterministic output (for testing/validation)
  - MIT/Apache license preferred
  - Maintainability (active project preferred)

## Candidate Libraries

### 1. **SharpGLTF** ⭐ Recommended
**Repository**: https://github.com/vpenades/SharpGLTF  
**NuGet**: `SharpGLTF.Core`  

**Pros**:
- ✅ Pure C#, no native dependencies (WASM-safe)
- ✅ Mature, well-documented, actively maintained
- ✅ Programmatic API for building glTF from geometry data
- ✅ Supports both glTF JSON + binary (.glb)
- ✅ Handles materials (PBR, textures)
- ✅ Submesh/primitives support
- ✅ Can extend with custom material definitions
- ✅ MIT license

**Cons**:
- Adds ~500KB to NuGet package (negligible for Blazor)

**Example Usage** (API preview):
```csharp
var model = ModelRoot.CreateInstance();
var mesh = model.CreateMesh("MeshName");
var prim = mesh.CreatePrimitive()
    .WithVertexAccessor("POSITION", vertices)
    .WithVertexAccessor("NORMAL", normals)
    .WithIndexAccessor(indices);
model.SaveAsGLB("output.glb");
```

**Assessment**: **BEST CHOICE** for this project. Well-suited to programmatic mesh generation.

---

### 2. **Assimp.NET** 
**Repository**: https://github.com/assimp-net/assimp-net  
**NuGet**: `AssimpNet`

**Pros**:
- ✅ Supports 40+ formats (glTF included)
- ✅ Can export to GLB
- ✅ Handles complex materials

**Cons**:
- ❌ Requires native `assimp` library (C++ DLL dependency)
- ❌ NOT WASM-compatible (no native library in browser)
- ❌ Overkill for static mesh export
- ❌ More complex to use for simple geometry

**Assessment**: **NOT VIABLE** for Blazor WASM (requires native binary).

---

### 3. **Assimp.NET (Pure C# Fork)** 
**Repository**: https://github.com/snoopotic/Assimp.NET (unmaintained)

**Pros**:
- Pure C# variant exists

**Cons**:
- ❌ Unmaintained (last commit 2016)
- ❌ Limited glTF support
- ❌ Not recommended for production

**Assessment**: **AVOID** (dead project).

---

### 4. **Custom glTF Writer** (Hand-rolled)
**Approach**: Implement minimal glTF 2.0 writer in C#

**Pros**:
- ✅ Zero external dependencies
- ✅ Full control over output format
- ✅ Lightweight (~400 LOC)
- ✅ Deterministic, easy to test

**Cons**:
- ❌ Must maintain custom code
- ❌ No material/texture support (without expanding)
- ❌ Need to handle glTF spec correctly (alignment, chunk layout)
- ❌ Testing/validation harder (must compare against reference implementations)

**Feasibility**: Viable but requires careful implementation (binary layout is critical).  
**Effort**: 6-8 hours to implement + test rigorously.

**Assessment**: **VIABLE BUT NOT PREFERRED** when SharpGLTF exists.

---

## Comparison Matrix

| Criterion | SharpGLTF | Assimp.NET | Custom |
|-----------|-----------|-----------|--------|
| **WASM-Safe** | ✅ Yes | ❌ No | ✅ Yes |
| **Maintained** | ✅ Yes | ❌ No | ⚠️ Self |
| **Material Support** | ✅ Good | ✅ Good | ⚠️ Limited |
| **Submesh Support** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Ease of Use** | ✅ Easy | ✅ Easy | ❌ Hard |
| **Dependencies** | ✅ Minimal | ❌ Native DLL | ✅ None |
| **Learning Curve** | ✅ Low | ⚠️ High | ❌ High |
| **Test Validation** | ✅ Easy | ✅ Easy | ⚠️ Hard |
| **License** | ✅ MIT | ✅ MIT | — |

---

## Recommendation

### 🏆 **Use SharpGLTF**

**Rationale**:
1. **WASM-compatible** — Pure C#, no native dependencies
2. **Proven** — Production-ready, community battle-tested
3. **Feature-rich** — Handles materials, submeshes, submesh groups
4. **Low maintenance** — Rely on upstream updates
5. **Faster to market** — Documented API, examples available
6. **Testing** — Standard glTF output, validate against Three.js GLTFLoader

**Integration Plan**:
- Install: `dotnet add package SharpGLTF.Core`
- Create: `UnityAssetParser/Export/GltfExporter.cs`
  - Accept parsed `Mesh` objects
  - Map to SharpGLTF primitives
  - Export GLB blob
- Create: `BlazorApp/Services/GltfExportService.cs`
  - Wraps exporter with caching/error handling
  - Integrates with `AssetRenderer` pipeline

**Next Steps**:
1. ✅ **Phase 1 (Research)** — Done
2. → **Phase 2 (Design)** — Define class structure, data mappings
3. → Implement exporter with unit tests

---

## References

- **glTF 2.0 Specification**: https://registry.khronos.org/glTF/specs/2.0/
- **SharpGLTF Documentation**: https://jgodfrey.github.io/SharpGLTF/
- **SharpGLTF GitHub**: https://github.com/vpenades/SharpGLTF
- **Three.js GLTFLoader**: https://threejs.org/docs/#examples/en/loaders/GLTFLoader

