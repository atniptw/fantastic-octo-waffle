# Unity Parsing Notes

 

## Overview
Direct port of UnityPy parsing rules covering UnityFS bundle nodes, Mesh structure, and extraction of positions/indices from external .resS resources.

## Bundle Structure (.hhh)
- UnityFS bundle with nodes
- Node 0: SerializedFile (objects/metadata)
- Node 1: .resS resource (vertex/index payloads)

## Mesh Parsing (ClassID 43)
- Field order with 4-byte alignment
- VertexData channels/streams
- CompressedMesh (PackedBitVector)
- StreamingInfo references external .resS

## Minimal Extraction Flow
1. Parse Mesh fields
2. Read StreamingInfo
3. Slice .resS by offset/size
4. Read positions via channels/streams
5. Read indices from triangles or IndexBuffer

## Renderable Check (Shallow Parse)
- Goal: Determine if an asset file is renderable without heavy data reads.
- Method: Open the asset (UnityFS → SerializedFile node, or direct SerializedFile) and read the object table/types.
- If any object has `ClassID 43` (Mesh), mark as renderable.
- Do not read vertex payloads or `.resS` bytes in this step; defer to full parse when the user selects the file to render.

## Full Parse (On Demand)
- Resolve `StreamingInfo` path and offsets to the corresponding `.resS`.
- Extract attributes via `VertexData` channels/streams (positions, normals, uvs, tangents).
- When data is compressed, use `CompressedMesh` with `PackedBitVector` to reconstruct triangles and attributes.
- Export minimal geometry (positions + indices) first for fast preview, optionally enrich with normals/uv later.

## Geometry Export for Three.js
- Build flat arrays: positions (XYZ), indices (triangle list), optional normals and uvs.
- Preserve submesh boundaries by emitting `groups` (`start`, `count`, `materialIndex`).
- Ensure indices reference valid vertex range; choose `Uint16` vs `Uint32` based on max index.
- If normals absent, let the renderer compute them.