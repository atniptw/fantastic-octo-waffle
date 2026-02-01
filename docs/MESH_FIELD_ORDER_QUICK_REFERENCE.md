# Mesh Field Order - Quick Reference Table

## Unity 2022.3 (v22) SerializedFile Format - ClassID 43

### Complete Field Reading Sequence

| Order | Field Name | Type | Bytes | Alignment Required | Version Gate | Critical Notes |
|-------|---|---|----|----|---|---|
| 1 | m_Name | String (length-prefixed) | 4 + N | ✓ YES (4-byte) | Always | UTF-8, must align after var-length string |
| 2 | m_SubMeshes | Array[SubMesh] | 4 + (N×52) | ✓ YES (4-byte) | Always | Count + 7 fields per element (v3+) |
| 3 | m_Shapes | BlendShapeData (struct) | Variable | ✓ YES (4-byte) | Always | 4 internal arrays: vertices, shapes, channels, weights |
| 4 | m_BindPose | Array[Matrix4x4f] | 4 + (N×64) | ✓ YES (4-byte) | ≥ v4 | 16 floats (64 bytes) per matrix |
| 5 | m_BoneNameHashes | Array[uint32] | 4 + (N×4) | ✓ YES (4-byte) | ≥ v4 | CRC32 hash values |
| 6 | m_RootBoneNameHash | uint32 | 4 | NO | ≥ v4 | Single CRC32 hash |
| 7 | m_BonesAABB | Array[AABB] | 4 + (N×24) | ✓ YES (4-byte) | ≥ v4 | AABB = 2×Vector3f (min, max) |
| 8 | m_VariableBoneCountWeights | VariableBoneCountWeights | Variable | ✓ YES (4-byte) | ≥ v4 | Byte array + CurveRanges array |
| 9 | m_MeshCompression | byte | 1 | NO | Always | 0=None, 1=Low, 2=Medium, 3=High |
| 10 | m_IsReadable | bool | 1 | NO | Always | CPU readable flag |
| 11 | m_KeepVertices | bool | 1 | NO | Always | Keep uncompressed data |
| 12 | m_KeepIndices | bool | 1 | ✓ YES (4-byte) | Always | **ALIGN AFTER THIS BOOL TRIPLET** |
| 13 | m_IndexFormat | int32 | 4 | NO | ≥ 2017.3 | 0=UInt16, 1=UInt32 |
| 14 | m_IndexBuffer | Array[byte] | 4 + N | ✓ YES (4-byte) | Always | Raw triangle index bytes |
| 15 | m_VertexData | VertexData (struct) | Variable | ✓ YES (4-byte) | Always | Channels, streams (positions, normals, UVs) |
| 16 | m_CompressedMesh | CompressedMesh (struct) | Variable | ✓ YES (4-byte) | Conditional | **ONLY if MeshCompression ≠ 0** |
| 17 | m_LocalAABB | AABB (struct) | 24 | ✓ YES (4-byte) | Always | Bounding box (center, extent) |
| 18 | m_MeshUsageFlags | int32 | 4 | NO | ≥ v5 | GPU flags (dynamic, streaming) |
| 19 | m_CookingOptions | int32 | 4 | NO | Always | Physics options |
| 20 | m_BakedConvexCollisionMesh | Array[byte] | 4 + N | ✓ YES (4-byte) | Always | Convex collision mesh |
| 21 | m_BakedTriangleCollisionMesh | Array[byte] | 4 + N | ✓ YES (4-byte) | Always | Triangle collision mesh |
| 22 | m_MeshMetrics[0] | float | 4 | NO | Always | Blend shape metric 0 |
| 23 | m_MeshMetrics[1] | float | 4 | NO | Always | Blend shape metric 1 |
| 24 | m_StreamData | StreamingInfo (struct) | 36 | NO | Always | Path string (var) + offset (8) + size (4) |

---

## Inline Struct Definitions

### SubMesh (52 bytes for v3+, 28 bytes for v2)

| Byte Offset | Field | Type | Size | Notes |
|---|---|---|---|---|
| 0 | firstByte | uint32 | 4 | Index buffer offset |
| 4 | indexCount | uint32 | 4 | Triangle count × 3 |
| 8 | topology | int32 | 4 | 0=Triangles, 1=Quads, 2=LineStrip |
| 12 | baseVertex | uint32 | 4 | Vertex offset |
| 16 | firstVertex | uint32 | 4 | First valid vertex |
| 20 | vertexCount | uint32 | 4 | Vertex count |
| 24 | localAABB | AABB | 24 | 2×Vector3f (v3+ only) |

### AABB (Always 24 bytes)

| Byte Offset | Field | Type | Size | Notes |
|---|---|---|---|---|
| 0 | m_Center | Vector3f | 12 | 3 floats: x, y, z |
| 12 | m_Extent | Vector3f | 12 | 3 floats: x, y, z |

### VertexData (Variable, stored as struct)

| Field | Type | Bytes | Alignment | Purpose |
|---|---|---|---|---|
| m_CurrentChannels | uint32 | 4 | (none) | Channel availability bitmask |
| m_VertexCount | uint32 | 4 | (none) | Total vertices |
| m_Channels | Array[ChannelInfo] | 4 + (N×4) | ✓ 4-byte | Vertex attribute metadata |
| m_StreamData | Array[StreamInfo] | 4 + (N×24) | ✓ 4-byte | Stream layout (stride, offset, size) |

**ChannelInfo** (4 bytes per element):
- stream (1 byte), offset (1 byte), format (1 byte), dimension (1 byte)

**StreamInfo** (24 bytes per element):
- channelMask (4), offset (4), stride (4), align (4), divideByInstanceCount (4), frequency (4)

### PackedBitVector (Variable)

| Field | Type | Bytes | Alignment | Purpose |
|---|---|---|---|---|
| m_NumItems | uint32 | 4 | (none) | Element count |
| m_Range | float | 4 | (none) | Quantization range |
| m_Start | float | 4 | (none) | Quantization offset |
| m_Data | Array[byte] | 4 + N | ✓ 4-byte | Bit-packed data |
| m_BitSize | byte | 1 | ✓ 4-byte | Bits per element |

### StreamingInfo (36 bytes minimum)

| Field | Type | Bytes | Alignment | Notes |
|---|---|---|---|---|
| m_Path | String | 4 + N | ✓ 4-byte | Path to .resS (e.g., "archive:/CAB-hash/CAB-hash.resS") |
| m_Offset | uint64 | 8 | (none) | Byte offset in resource |
| m_Size | uint32 | 4 | (none) | Byte size |

---

## Critical Implementation Rules

### Rule 1: 4-Byte Alignment After Byte Arrays
**Every** byte array must be followed by alignment padding:
```csharp
byte[] data = reader.ReadBytes(count);
reader.Align(4);  // MUST DO THIS
```

### Rule 2: 4-Byte Alignment After Bool Triplet
After reading the 3 consecutive bools (m_MeshCompression, m_IsReadable, m_KeepVertices, m_KeepIndices = 4 bytes total):
```csharp
reader.ReadByte();   // m_MeshCompression
reader.ReadBool();   // m_IsReadable
reader.ReadBool();   // m_KeepVertices
reader.ReadBool();   // m_KeepIndices  
reader.Align(4);     // MUST ALIGN HERE (3 bytes of padding)
```

### Rule 3: Conditional Reads (Version Gates)

Always check version BEFORE reading conditional fields:
```csharp
if (major >= 4)
    bindPose = ReadArray<Matrix4x4f>();

if (major >= 5)
    meshUsageFlags = reader.ReadInt32();

if (major >= 2017 || (major == 2017 && minor >= 3))
    indexFormat = reader.ReadInt32();

if (meshCompression != 0)
    compressedMesh = ReadCompressedMesh();
```

### Rule 4: String Reading (Length-Prefixed)

```csharp
int stringLength = reader.ReadInt32();  // Not including the 4-byte length itself
string value = reader.ReadUtf8String(stringLength);
reader.Align(4);  // Important: align after var-length string
```

---

## Version Detection

**Unity 2022.3 = version (2022, 3, x, x)**

```csharp
int major = version.Item1;
int minor = version.Item2;

// v22 format detection (always true for 2022+)
if (majorVersion >= 22) { ... }
```

For backward compatibility with older Unity versions (v3-v21):
- v4: Added skinning fields (BindPose, BoneNameHashes, etc.)
- v5: Added MeshUsageFlags
- 2017.3+: Added IndexFormat

---

## Byte Count Summary

For a minimal v2022 Mesh:

| Component | Min Bytes | Notes |
|---|---|---|
| m_Name | 5 | 4-byte length + 1-char string |
| m_SubMeshes | 60 | Count (4) + 1 submesh (52) |
| m_Shapes | 20 | 4 counts × 4 bytes |
| m_BindPose | 8 | Count (4) + 0 matrices |
| m_BoneNameHashes | 4 | Count (4) only |
| m_RootBoneNameHash | 4 | Single uint32 |
| m_BonesAABB | 4 | Count (4) only |
| m_VariableBoneCountWeights | 8 | Count (4) × 2 |
| m_MeshCompression..m_KeepIndices | 4 | 3 bytes + 1 padding |
| m_IndexBuffer | 4 | Count (4) only |
| m_VertexData | 16 | 2 counts + 2 uint32 |
| m_LocalAABB | 24 | Min/max Vector3f |
| m_CookingOptions | 4 | Single int32 |
| m_BakedConvexCollisionMesh | 4 | Count (4) only |
| m_BakedTriangleCollisionMesh | 4 | Count (4) only |
| m_MeshMetrics | 8 | 2 floats |
| m_StreamData | 28 | Path count (4) + offset (8) + size (4) + padding |
| **TOTAL** | **~215 bytes** | Minimal mesh with no data |

---

## References

- **UnityPy Mesh.py**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py
- **Implementation**: `/src/UnityAssetParser/Services/MeshParser.cs`
- **Full Docs**: [docs/MESH_FIELD_ORDER_REFERENCE.md](./MESH_FIELD_ORDER_REFERENCE.md)
