# Mesh-Only Parser Plan

## Goals
- Extract static mesh geometry from `.hhh` UnityFS bundles.
- Export a valid GLB with mesh nodes and a default material.
- Use UnityPy and AssetStudio as the authoritative references for file layouts.

## Non-Goals (Initial Pass)
- No skinning, bones, or animation.
- No textures or materials beyond a flat default.
- No compressed mesh decoding (unless required by fixtures).

## Inputs and Outputs
- Inputs: `.hhh` files that contain UnityFS bundles and embedded `*.assets` payloads.
- Output: GLB (glTF 2.0 binary) with static meshes only.

## Current Capability Snapshot
- UnityFS detection and block info parsing with LZ4 block decompression.
- UnityFS payload slicing to access entry bytes.
- SerializedFile header/object table parsing (no object decode yet).

## Parsing Pipeline
1. Identify container type (UnityFS vs SerializedFile).
2. UnityFS:
   - Read header (`size`, block info sizes, `flags`).
   - Decompress block info (LZ4 or uncompressed; LZMA TBD).
   - Decompress data blocks into a contiguous stream.
   - Slice entries and identify `*.assets` or `CAB-*` payloads.
3. SerializedFile:
   - Read header and metadata section.
   - Read types and object table.
   - Locate `Mesh` objects by class ID (43).
4. Mesh decode:
   - Parse `m_SubMeshes`, `m_IndexBuffer`, `m_IndexFormat`, `m_VertexData`.
   - Extract POSITION (and optionally NORMAL) only.
5. Build GLB:
   - One mesh per Unity `Mesh`.
   - One node per mesh.
   - Default material only.

## Data Model Evolution
- Add `SerializedTypeInfo` with type tree blobs for `Mesh`.
- Add a mesh DTO with positions, indices, and submesh ranges.
- Store per-entry payloads when parsing UnityFS.

## Type Tree Variants
- Some builds set `enableTypeTree` false or strip type tree data.
- In that case we do not fall back to a static type database; we only decode
  meshes when a type tree is present in the file.
- If type trees are missing, surface a warning and skip mesh decoding.

## Mesh Decoding Notes (Unity 2020-2022)
- Class ID 43 (`Mesh`).
- Required fields:
  - `m_SubMeshes`: index ranges + topology.
  - `m_IndexFormat` (or `m_Use16BitIndices`).
  - `m_IndexBuffer`.
  - `m_VertexData` (channels + raw buffer).
- Ignore for now:
  - `m_CompressedMesh`, `m_BindPose`, `m_BoneNameHashes`, `m_Shapes`, `m_StreamData`.

### VertexData Channel Formats (UnityPy/AssetStudio)
- Unity 2019+ `VertexFormat` values:
  - 0: float32 (4 bytes)
  - 1: float16 (2 bytes)
  - 2: UNorm8 (1 byte)
  - 3: SNorm8 (1 byte)
  - 4: UNorm16 (2 bytes)
  - 5: SNorm16 (2 bytes)
  - 6: UInt8 (1 byte)
  - 7: SInt8 (1 byte)
  - 8: UInt16 (2 bytes)
  - 9: SInt16 (2 bytes)
  - 10: UInt32 (4 bytes)
  - 11: SInt32 (4 bytes)
- Unity 2017-2018 uses `VertexFormat2017` (same ordinal meanings as above).
- Unity 4-5 uses `VertexChannelFormat` and requires mapping:
  - Float -> Float
  - Float16 -> Float16
  - Color -> UNorm8
  - Byte -> UInt8
  - UInt32 -> UInt32
- UNorm/SNorm normalization (AssetStudio MeshHelper):
  - UNorm8: $x/255$
  - SNorm8: $max(x/127, -1)$
  - UNorm16: $x/65535$
  - SNorm16: $max(x/32767, -1)$
- UnityPy references: `UnityPy/enums/VertexFormat.py`, `UnityPy/helpers/MeshHelper.py`.
- AssetStudio references: `AssetStudio/Classes/Mesh.cs` (MeshHelper).

### VertexData Reader Checklist
- Read `m_VertexData.m_VertexCount`.
- Read channel descriptors: `stream`, `offset`, `format`, `dimension`.
- Treat `dimension` as `dimension & 0xF` (AssetStudio `ChannelInfo`).
- Compute per-stream stride from channels in the same stream.
- Locate POSITION channel and decode float3 per vertex.
- Optionally locate NORMAL channel and decode float3 per vertex.
- Use `m_VertexData.m_DataSize` and the raw data blob for buffer slicing.

### Stride Calculation Example
- Group channels by `stream` and compute stride as the max of `(offset + formatSize * dimension)` per channel.
- Example (stream 0): POSITION offset 0, format float32 (4 bytes), dimension 3 -> 12 bytes.
- Example (stream 0): NORMAL offset 12, format float32, dimension 3 -> 24 bytes.
- Resulting stride for stream 0: 24 bytes.
- Align stream offsets to 16 bytes when stacking streams (UnityPy/AssetStudio).

### Index Buffer Reader Checklist
- Determine index size:
  - `m_IndexFormat` == 0 -> 16-bit indices.
  - `m_IndexFormat` == 1 -> 32-bit indices.
- Slice `m_IndexBuffer` by each submesh `firstByte` and `indexCount`.
- Map Unity topology to glTF primitive modes (triangles expected in fixtures).

### Minimal glTF Buffer Layout Example
- One buffer containing: positions (float32 vec3), then indices.
- BufferViews:
  - View 0: positions, target ARRAY_BUFFER.
  - View 1: indices, target ELEMENT_ARRAY_BUFFER.
- Accessors:
  - Positions: type VEC3, componentType 5126 (FLOAT).
  - Indices: type SCALAR, componentType 5123 (UNSIGNED_SHORT) or 5125 (UNSIGNED_INT).

## Validation Strategy
- Cross-check UnityFS entries and mesh counts against UnityPy/AssetStudio.
- Validate decompression invariants:
  - Sum of uncompressed blocks equals output size.
  - Entry offsets and sizes are within bounds.
- For a known fixture:
  - Compare index/vertex counts with UnityPy/AssetStudio output.
  - Compute a simple hash of mesh data for regression checks.

## Resource Streams (.resS)
- Some meshes and textures store binary data in external resource streams.
- Serialized files reference these via a `StreamedResource`/`StreamingInfo` record
  with `path`, `offset`, and `size`.
- When `m_StreamData.path` is set, replace `m_VertexData.m_DataSize` with the
  resolved resource bytes before decoding (UnityPy/AssetStudio behavior).
- UnityFS bundles typically include `.resS` or `.resource` entries; resolve the
  path against bundle entry names (case-insensitive) and read the byte slice.
- For mesh-only output, only the vertex/index payloads need to be resolved.

## References
- UnityPy (bundle + mesh parsing): https://github.com/K0lb3/UnityPy
- AssetStudio (bundle + mesh parsing): https://github.com/Perfare/AssetStudio

## Roadmap
- Add LZMA block decompression if required.
- Add skinning support (bones + weights).
- Add materials and textures.
