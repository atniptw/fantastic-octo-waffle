# Unity Parsing Notes

 

## Overview
Direct port of UnityPy parsing rules covering UnityFS bundle nodes, SerializedFile object metadata, Mesh structure, and extraction of positions/indices from external .resS resources.

## Bundle Structure (.hhh)
- UnityFS bundle with nodes
- Node 0: SerializedFile (objects/metadata)
- Node 1: .resS resource (vertex/index payloads)

## SerializedFile Parsing

### Structure
SerializedFile is the container format for Unity scene/asset data inside UnityFS bundles. It contains:
- **Header**: Format version, endianness, file/data sizes
- **Type Tree**: Type metadata describing object layouts (optional, version-dependent)
- **Object Table**: List of objects with PathID, ClassID, offset, size
- **File Identifiers**: External file references for cross-bundle dependencies
- **Object Data Region**: Raw serialized object payloads

### Header Formats

#### Version >= 22 (Unity 2022+)
- MetadataSize (uint32) - size of metadata region (header + type tree + object table)
- FileSize (int64) - total file size
- Version (uint32) - SerializedFile format version
- DataOffset (int64) - start of object data blob
- Endianness (byte) - 0 = little-endian, 1 = big-endian
- Reserved (3 bytes padding)

#### Version >= 14 < 22 (Unity 5.x - 2021)
- MetadataSize (uint32)
- FileSize (uint32)
- Version (uint32)
- DataOffset (uint32)
- Endianness (byte)
- Reserved (3 bytes padding)

#### Version < 14
Different layout with Unity version string, target platform, and EnableTypeTree flag. Port exact UnityPy branches for these older formats.

### Endianness Handling
- If `Endianness == 1` (big-endian), all multi-byte integers must be byte-swapped after reading
- `EndianBinaryReader` wrapper class conditionally swaps bytes based on header flag
- Apply endianness consistently to all fields (header, type tree, object table, type tree nodes)
- Big-endian rare (console platforms: PS3, Xbox 360, Wii U)

### Type Tree
Contains type metadata for object deserialization:
- `ClassId`: Unity ClassID (43 = Mesh, 28 = Texture2D, etc.)
- `IsStrippedType`: Whether editor-only data is removed
- `ScriptTypeIndex`: For MonoBehaviour scripts (-1 if not applicable)
- `ScriptId`: MD5 hash for MonoBehaviour (16 bytes, version >= 17)
- `OldTypeHash`: Type hash (16 bytes, version >= 5)
- `TypeTreeNodes`: Field structure (if `EnableTypeTree` is true)
- `TypeDependencies`: Dependency indices (version >= 21)

#### TypeTreeNode
- `Type`: Field type name (e.g., "Vector3f", "int")
- `Name`: Field name (e.g., "m_Position", "m_Size")
- `ByteSize`: Field size in bytes (-1 for variable-length)
- `Index`: Node index in tree
- `TypeFlags`: Flags (bit 0x4000 indicates array)
- `Version`: Field version
- `MetaFlag`: Metadata flags
- `Level`: Tree depth (0 = root, 1 = child, etc.)

For version >= 10, strings are stored in a string table for deduplication (referenced by offsets).
For version < 10, strings are inline null-terminated.

### Object Table
Each `ObjectInfo` entry:
- `PathId`: Unique object identifier within file (m_PathID)
- `ByteStart`: Offset relative to DataOffset where object data starts
- `ByteSize`: Object payload size in bytes
- `TypeId`: Index into type tree, or direct ClassID (if no type tree)
- `ClassId`: Unity ClassID (resolved from TypeId via type tree lookup)
- `ScriptTypeIndex`: For MonoBehaviour (version >= 11)
- `Stripped`: 1 = stripped, editor-only data removed (version >= 15)

Layout varies by version:
- Version >= 14: PathId (int64), ByteStart (int64 for v22+, uint32 otherwise), ByteSize (uint32), TypeId (int32)
- Version < 14: Different sizes and alignment (port exact UnityPy layout)

4-byte alignment required before object table and between entries for version >= 14.

### ClassId Resolution
- If type tree present and `HasTypeTree == true`: `ClassId = TypeTree.Types[ObjectInfo.TypeId].ClassId`
- Else: `ClassId = ObjectInfo.TypeId` (direct use as ClassID)
- Handle edge case: TypeId out of range → throw `InvalidObjectInfoException`

### External References (FileIdentifier)
Each `FileIdentifier`:
- `Guid`: 16-byte GUID (version >= 6, else `Guid.Empty`)
- `Type`: Identifier type (0 = not serialized, other values per Unity internal)
- `PathName`: External file path (e.g., "archive:/CAB-hash/CAB-hash.resS")

Resolution: When object references external file (e.g., `StreamingInfo.path`), match `PathName` to BundleFile nodes by exact string comparison.

### Data Offset & Object Data
- Object data region starts at `Header.DataOffset`
- Each object's absolute offset: `DataOffset + ObjectInfo.ByteStart`
- Validate: `ByteStart + ByteSize ≤ FileSize - DataOffset`
- Expose `ObjectDataRegion` as `data[DataOffset..FileSize]` for efficient slicing

### Parsing Algorithm
1. Parse header (version-specific layout)
2. Validate header bounds (FileSize, DataOffset, MetadataSize)
3. Create `EndianBinaryReader` based on Endianness flag
4. Read version-specific metadata (Unity version string, target platform, EnableTypeTree flag)
5. Parse type tree (type count, then each SerializedType with version-specific fields)
6. Align to 4-byte boundary
7. Parse object table (object count, then each ObjectInfo with version-specific fields)
8. Resolve ClassIDs for objects (TypeId → ClassId via type tree lookup)
9. Align to 4-byte boundary
10. Parse file identifiers (external references)
11. Validate object bounds (ByteStart + ByteSize within data region)
12. Extract object data region slice
13. Return `SerializedFile` instance

### API Usage
```csharp
// Parse SerializedFile from bundle node
var serializedFile = SerializedFile.Parse(nodeData.Span);

// Check for Mesh objects (ClassID 43)
bool hasRenderable = serializedFile.GetObjectsByClassId(43).Any();

// Find specific object by PathId
var meshObj = serializedFile.FindObject(pathId: 123);

// Read object payload for downstream parser
var objectData = serializedFile.ReadObjectData(meshObj);
// Pass to Mesh parser, Texture2D parser, etc.
```

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
- Implementation: `serializedFile.GetObjectsByClassId(43).Any()` → bool renderable

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