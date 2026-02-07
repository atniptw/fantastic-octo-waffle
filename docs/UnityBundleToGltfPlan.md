# UnityFS (.hhh) → glTF/GLB → Three.js (Blazor WASM) plan

This plan assumes UnityFS bundles (the `.hhh` files used in this repo) and a
client-only Blazor WebAssembly viewer that renders with Three.js.

## 0) Current state in this repo (what already exists)

**Parsing + extraction**
- `MeshExtractionService` parses UnityFS bundles, finds Mesh objects, and
  emits `MeshGeometryDto` (positions, indices, normals, UVs, etc.).
- `MeshHelper` is a UnityPy-derived extractor for vertex/index data.

**Export**
- `GltfExporter` converts Unity meshes to glTF using SharpGLTF.
- `MeshExportService` can emit a Three.js BufferGeometry JSON format (slower).

**Status implication**
- Mesh extraction is in place, but materials/textures are not yet exported in
  glTF. That is the primary gap for “looks like in-game.”

## 1) Phase 1 — Validate parsing end-to-end (mesh only)

Goal: confirm the `.hhh` bundle can be parsed and a mesh can be rendered.

1. **Bundle parsing sanity check**
   - Use `MeshExtractionService.ExtractMeshes()` on a known `.hhh` bundle.
   - Confirm Mesh count > 0 and vertex/index lengths are non-zero.

2. **Generate GLB in WASM**
   - Convert extracted meshes into a GLB byte array via `GltfExporter`.
   - Feed GLB into Three.js `GLTFLoader` from an in-memory `Blob`.

3. **Render baseline**
   - Render the mesh with a neutral PBR material.
   - Validate scale/orientation and normals.

**Exit criteria**
- A `.hhh` file loads in the Blazor app and renders the base mesh in Three.js.

## 2) Phase 2 — Materials, colors, and textures

Goal: match Unity’s visual output as closely as possible.

Unity “look” depends on **materials, textures, and shader parameters**. This
requires mapping Unity material data into glTF PBR conventions. The next steps:

1. **Extract materials**
   - In SerializedFile, find `Material` objects (Unity ClassID 21).
   - Parse shader name, texture slots (e.g., `_MainTex`), and color vectors.

2. **Extract textures**
   - Parse `Texture2D` objects (ClassID 28).
   - Decode texture data into PNG/JPEG in WASM (if possible), or keep raw bytes.

3. **Map to glTF**
   - Map Unity’s standard shader values to glTF PBR:
     - Albedo → `baseColorTexture`
     - Color → `baseColorFactor`
     - Metallic/Smoothness → `metallicFactor` / `roughnessFactor`
     - Normal map → `normalTexture`
     - Emission → `emissiveTexture` / `emissiveFactor`
   - For non-standard shaders, default to albedo + color and log a warning.

4. **Vertex colors**
   - If meshes contain vertex colors, attach to glTF color attribute and
     multiply with base color in Three.js material if needed.

**Exit criteria**
- GLB renders with at least base color texture + metallic/roughness matching
  most common Unity materials.

## 3) Phase 3 — Streaming data and multi-node bundles

Unity bundles often keep vertex buffers in `.resS` nodes:

1. **Ensure `.resS` is loaded in browser**
   - `MeshExtractionService` already looks for a `.resS` node.
   - Confirm in WASM that the full bundle data is retained so streaming reads
     work during extraction.

2. **Multiple mesh nodes**
   - Support multiple Mesh objects per bundle (skinned + static).
   - Map each Mesh to a glTF mesh with separate primitives/submeshes.

**Exit criteria**
- Bundles with external streaming data render correctly.

## 4) Phase 4 — Lighting and scene parity

To approximate in-game look:

- Add a default Three.js lighting rig (HDRI/IBL + directional key).
- Use tone mapping and sRGB output to match Unity’s color space.
- Optionally read Unity `Light` or `Environment` data if present (lower priority).

## 5) Performance optimizations for WASM

- Move mesh parsing to a Web Worker or `Task.Run()` to keep UI responsive.
- Use `ArrayBuffer` + `Span<byte>` to avoid extra copies.
- Prefer GLB over JSON to avoid massive base64 strings.
- Cache parsed results in IndexedDB for repeat loads.

## 6) Concrete next actions (recommended order)

1. **Add a “Parse & Render” smoke test page**
   - Upload `.hhh` → parse → convert to GLB → render in Three.js.

2. **Confirm correctness of mesh extraction**
   - Compare vertex/triangle counts with UnityPy or AssetStudio for the same file.

3. **Implement minimal material + texture extraction**
   - Albedo + base color + normal map first.

4. **Extend glTF export to include materials + textures**
   - Bind textures to `MaterialBuilder` in `GltfExporter`.

## 7) Risks and realities

- Unity shaders are not 1:1 with glTF. Some materials will always deviate.
- Custom shaders may require heuristics or per-game overrides.
- Textures may be in formats that require transcoding (ETC, BCn, ASTC).

## 8) How to validate “looks like in-game”

- Use a reference renderer (UnityPy/AssetStudio or Unity Editor) to screenshot.
- Compare with Three.js output under similar lighting.
- Track deviations per shader and add targeted fixes.
