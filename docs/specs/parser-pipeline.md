# Unity AssetBundle Parser Pipeline

## Scope and Supported Formats

**Target:** Unity 2022.3.x AssetBundle format (UnityFS).

**In-scope:**
- `.hhh` files (AssetBundle archives containing serialized Unity objects).
- Object types: `Mesh`, `Texture2D`, `Material`, `Shader`.
- Compression: **LZ4** (uncompressed blocks) and **LZMA** (compressed blocks).
- Texture formats: **RGBA32**, **DXT1/DXT5** (BC1/BC3), **PNG**, **JPEG** (embedded).
- Materials: Mapping Unity **Standard shader** to three.js `MeshStandardMaterial`.
- Memory model: Support bundles up to ~500 MB uncompressed.

**Out-of-scope (v1):**
- Crunch-compressed textures (deferred to Phase 3).
- Advanced shaders (custom vertex/fragment); fallback to standard.
- Animation/skinning (cosmetics are static meshes).
- Nested prefabs or scene hierarchies (flat mesh list only).

---

## Binary Layout

### UnityFS Header

```c
struct UnityFSHeader {
  uint8_t signature[4];              // "UnityFS\0" (0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00)
  uint32_t version;                  // (big-endian) 6 for 2022.3
  uint32_t lzma_chunk_size;          // (big-endian) max uncompressed block size
  uint32_t file_size;                // (big-endian) total archive size (header + blocks + directory)
  uint32_t block_info_offset;        // (big-endian) offset to block info section
  uint32_t blocks_info_size;         // (big-endian) total size of block info
  // --- v4/v5 only ---
  uint32_t directory_info_offset;    // (big-endian) offset to directory (object table)
  uint32_t directory_info_size;      // (big-endian) size of directory
  uint32_t flags;                    // (big-endian) bit 7 = has directory; bit 6 = blocksInfoOnly
};
// Total: 32 bytes (v4+)
```

**Parsing:**
- Read first 32 bytes as big-endian integers.
- Validate signature.
- Verify version is 6 (Unity 2022.3).
- Locate block info and directory using offsets.

### Block Info Section

```c
struct BlockInfo {
  uint16_t uncompressed_size;        // (big-endian)
  uint16_t compressed_size;          // (big-endian)
  uint16_t flags;                    // (big-endian) bit 6 = compression (0=LZMA, 1=LZ4)
};
// Repeat for each block.
```

**Compression flags:**
- `0x00`: No compression (raw block).
- `0x40`: LZ4 compression.
- `0xC0`: LZMA compression.

**Example:** For a 5 MB bundle with 1 MB blocks:
- Block 0: 1 MB uncompressed → 500 KB LZ4-compressed.
- Block 1: 1 MB uncompressed → 400 KB LZMA-compressed.
- Blocks 2–4: Similar.

### Directory (Object Table)

```c
struct DirectoryInfo {
  uint32_t num_entries;              // (big-endian) count of objects
};
// Followed by num_entries DirectoryEntry structures:
struct DirectoryEntry {
  uint64_t offset;                   // (big-endian) byte offset in uncompressed stream
  uint32_t size;                     // (big-endian) object size in bytes
  uint32_t type_id;                  // (big-endian) internal type ID
  uint32_t class_id;                 // (big-endian) serialized class ID (91=Mesh, 28=Texture2D, etc.)
  uint16_t script_type_index;        // (big-endian) for custom scripts; -1 if none
  uint8_t is_destroyed;              // destroyed flag
  uint8_t padding;
};
```

**Class IDs (selected):**
- `91`: Mesh
- `28`: Texture2D
- `21`: Material
- `48`: Shader
- `4`: Transform, GameObject (skip)

---

## Parsing Steps

### Step 1: Read Header & Validate

1. Read 32 bytes → parse as UnityFSHeader.
2. Check signature == "UnityFS\0".
3. Verify version == 6.
4. Validate file size matches actual data.

**Output:** Header struct; offsets to block info and directory.

### Step 2: Read & Decompress Blocks

1. Seek to `header.block_info_offset`.
2. Read block descriptors (16 bytes each).
3. For each block:
   - Read `compressed_size` bytes from file.
   - Check compression flags.
   - If LZMA: Decompress using WASM LZMA decoder → output array.
   - If LZ4: Decompress using lz4js → output array.
   - If none: Copy as-is.
   - Concatenate all decompressed blocks into a single `uncompressed_data` ArrayBuffer.

**Error handling:**
- If any block decompresses to size != expected uncompressed size → abort; log error + block index.
- If LZMA fails → offer user option to clear cache and retry; suggest reporting if persists.

**Output:** Single `uncompressed_data: ArrayBuffer` (entire serialized content).

### Step 3: Read Directory & Build Object Index

1. Seek to `header.directory_info_offset` within `uncompressed_data`.
2. Read `num_entries`.
3. Parse each `DirectoryEntry` → store offset, size, type_id, class_id.
4. Build a map: `{ class_id: [entries...] }` for quick lookup by type.

**Output:** `objectIndex: Map<classID, DirectoryEntry[]>`.

### Step 4: Extract & Deserialize Supported Objects

For each `DirectoryEntry` with `class_id` in `[91, 28, 21]`:

1. **Mesh (class_id=91):**
   - Read `size` bytes from `uncompressed_data` starting at `offset`.
   - Parse as serialized Mesh (see [Mesh Deserialization](#mesh-deserialization)).
   - Extract: vertices, normals, tangents, UVs, bone-weights (if skinned; skip for v1), triangles/indices, submesh ranges.

2. **Texture2D (class_id=28):**
   - Read bytes; parse as serialized Texture2D (name, width, height, format, mipmap data).
   - If format is PNG/JPEG embedded → extract payload; decode with `createImageBitmap()`.
   - If format is DXT/ETC → note format; defer decoding to renderer (GPU decompression via three.js).

3. **Material (class_id=21):**
   - Parse serialized Material (name, shader name, properties).
   - Extract property map: `{ "_MainTex": textureRef, "_Metallic": 0.5, ... }`.

**Output:** Intermediate representation (IR):
```typescript
type ParsedBundle = {
  meshes: ParsedMesh[];
  textures: ParsedTexture[];
  materials: ParsedMaterial[];
};
```

---

## Object Types and Deserialization

### Mesh Deserialization

**Serialized Mesh structure (simplified):**
```
uint32_t  m_Name_length
char[]    m_Name
-- Vertices --
uint32_t  m_Vertices.size  (count)
float[]   m_Vertices (vec3, count*3 floats)
-- Normals --
uint32_t  m_Normals.size
float[]   m_Normals (vec3, count*3 floats)
-- Tangents --
uint32_t  m_Tangents.size
float[]   m_Tangents (vec4, count*4 floats)
-- UVs --
uint32_t  m_UV0.size
float[]   m_UV0 (vec2, count*2 floats)
-- Indices (triangles) --
uint32_t  m_Triangles.size (count of uint32 indices)
uint32[]  m_Triangles
-- Submeshes --
uint32_t  m_SubMeshes.size (count)
SubMesh[] m_SubMeshes { firstVertex, vertexCount, firstIndex, indexCount }
```

**Parsing code pattern:**
```typescript
const mesh = {
  name: readString(data, offset),
  vertices: readFloat32Array(data, offset, count * 3),
  normals: readFloat32Array(data, offset, count * 3),
  tangents: readFloat32Array(data, offset, count * 4),
  uv0: readFloat32Array(data, offset, count * 2),
  indices: readUint32Array(data, offset, indexCount),
  submeshes: readSubmeshes(data, offset, count)
};
```

### Texture2D Deserialization

```
uint32_t  m_Name_length
char[]    m_Name
-- Metadata --
uint32_t  m_Width
uint32_t  m_Height
int32_t   m_TextureFormat  // 4=RGBA32, 10=DXT1, 12=DXT5, etc.
uint32_t  m_MipCount
uint32_t  m_IsReadable
-- Pixel data --
uint32_t  m_ImageData_size
uint8_t[] m_ImageData (compressed or raw)
```

**Handling formats:**
- **RGBA32 (4):** 4 bytes per pixel; directly load to GPU.
- **DXT1/DXT5 (10/12):** Compressed texture format; three.js `CompressedTexture` support.
- **PNG/JPEG:** Embed as bytes; decode via `createImageBitmap({ type: 'image/png' })` in Web Worker.
- **Crunch (37):** Defer to Phase 3 (requires extra WASM codec).

### Material Deserialization

```
uint32_t  m_Name_length
char[]    m_Name
uint32_t  m_Shader offset (reference to Shader object)
uint32_t  m_UniformBuffer.size (serialized properties)
...       Property data (varies by shader)
```

**For Standard shader, extract:**
- `_MainTex` → base color texture.
- `_MetallicGloss` → packed metallic + smoothness (R=metallic, A=smoothness).
- `_BumpMap` → normal map.
- `_Smoothness` → direct smoothness value (if not packed).

**Mapping to three.js:**
```typescript
const material = new MeshStandardMaterial({
  map: baseColorTexture,
  normalMap: normalTexture,
  metalness: metallicValue,
  roughness: 1 - smoothnessValue,
  side: DoubleSide,
  envMapIntensity: 1.0
});
```

---

## Mapping to three.js

### Geometry Assembly

```typescript
// Create BufferGeometry from parsed mesh
const geometry = new BufferGeometry();
geometry.setAttribute('position', new BufferAttribute(
  new Float32Array(mesh.vertices), 3
));
geometry.setAttribute('normal', new BufferAttribute(
  new Float32Array(mesh.normals), 3
));
geometry.setAttribute('uv', new BufferAttribute(
  new Float32Array(mesh.uv0), 2
));
geometry.setIndex(new BufferAttribute(
  new Uint32Array(mesh.indices), 1
));

// If no normals provided, compute them
if (!mesh.normals.length) {
  geometry.computeVertexNormals();
}
```

### Material Assembly

```typescript
const materialMap = new Map<string, Material>();
for (const parsedMat of materials) {
  const mat = new MeshStandardMaterial({
    name: parsedMat.name,
    map: getTexture(parsedMat.baseColorTexture),
    normalMap: getTexture(parsedMat.normalTexture),
    metalness: parsedMat.metallic,
    roughness: parsedMat.roughness,
    side: DoubleSide
  });
  materialMap.set(parsedMat.name, mat);
}
```

### Scene Graph

```typescript
const group = new Group();
for (const mesh of meshes) {
  const geometry = createGeometry(mesh);
  const material = materialMap.get(mesh.materialName) || fallbackMaterial;
  const obj = new Mesh(geometry, material);
  obj.name = mesh.name;
  group.add(obj);
}

// Center and scale model
const box = new Box3().setFromObject(group);
const center = box.getCenter(new Vector3());
const size = box.getSize(new Vector3());
group.position.sub(center);

const scale = 5 / Math.max(size.x, size.y, size.z);
group.scale.multiplyScalar(scale);

scene.add(group);
```

---

## Performance Considerations

### Memory Management
- **Streaming:** Don't load entire uncompressed data into memory at once; decompress blocks incrementally.
- **Buffer reuse:** Reuse typed arrays where possible (e.g., temporary decompression buffers).
- **Garbage collection:** Post-processing should yield periodically to avoid GC pauses.

### Decompression Optimization
- **LZ4:** Use `lz4js` (JavaScript; ~50KB). Fast; supports streaming.
- **LZMA:** Use pre-compiled WebAssembly module (`@wasmer/wasm-xz` or similar; ~100KB compressed). ~10x faster than JS.
- **Pre-init:** Initialize WASM decoder once at app startup, reuse across bundles.

### Texture Decode
- **createImageBitmap:** Offload PNG/JPEG decoding to Web Worker via `createImageBitmap({ type: 'image/png' })`.
- **DXT/ETC:** Keep compressed; GPU decompresses (via three.js extension).

### Profiling
- Log parse time per phase (decompress, deserialize, scene build).
- Telemetry: bundle size, mesh count, texture count, parse duration.
- Budget: aim for < 3s total parse for typical 10–50 MB cosmetic bundle.

---

## Known Limitations and Gotchas

### LZMA Block Boundaries
- **Gotcha:** Each LZMA block is independently compressed; decoders must be reset between blocks.
- **Fix:** Initialize a fresh LZMA decoder per block or use a streaming interface if available.

### Texture Format Mismatches
- **Gotcha:** Crunch-compressed textures (common in older mods) require a Crunch decoder; bundling this adds ~200KB.
- **v1 workaround:** Detect Crunch format; display "Crunch textures not yet supported"; fetch uncompressed fallback if available.

### Missing Tangents
- **Gotcha:** Some meshes lack tangent data. Normal mapping requires tangent vectors for per-pixel orientation.
- **Fix:** Use `three.js/examples/geometries/TangentSpaceNormalMapGenerator.js` to compute tangents if missing.

### Shader Property Extraction
- **Gotcha:** Material properties (like `_Metallic`, `_MainTex`) are serialized in a format that varies by shader and unity version.
- **Approach for v1:** Hardcode parsing for Standard shader; for other shaders, gracefully skip properties and render with fallback material.

### Large Model Memory Overhead
- **Gotcha:** A 50 MB compressed bundle can decompress to 200+ MB; combined with three.js scene graph, peak memory can exceed 500 MB.
- **Mitigation:** Show warning UI if uncompressed size > 300 MB; offer to clear IndexedDB cache; suggest using a modern device.

### Reference Resolution
- **Gotcha:** Materials reference textures by offset into the object directory; texture names may not align with material property names.
- **Approach:** Build a reverse index during deserialization; match textures to materials by internal reference offsets.

---

## Testing & Validation

### Fixtures
- Maintain a set of small (<10 MB) test bundles from known mods (e.g., Masaicker/MoreHead v1.0).
- Store hashes and expected parse outputs (golden files).

### Unit Tests
- Binary reader functions: alignment, byte order, slicing.
- Decompression: known vectors for LZ4 and LZMA.
- Mesh/texture/material parsing: synthetic small structures.

### Integration Tests
- Full parse of a real small cosmetic bundle; assert mesh count, vertex count, material names.
- Verify three.js scene graph is valid and renderable.

### Acceptance Criteria (v1)
- [ ] Parse Masaicker/MoreHead 1.0.0 and render at least 3 meshes.
- [ ] Handle both LZ4 and LZMA block compression.
- [ ] Render base color and normal maps; tolerate missing metallic/smoothness.
- [ ] Parse-to-render time < 3s on mid-range laptop.
- [ ] Memory peak < 500 MB.
- [ ] Friendly error messages for unsupported formats.


