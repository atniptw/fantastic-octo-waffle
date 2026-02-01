# Mesh Binary Field Reading Order - Unity 2022.3 (v22 SerializedFile)

## Overview

This document specifies the **exact byte-by-byte field reading order** for Mesh objects (ClassID 43) in Unity 2022.3 using v22 SerializedFile format.

**Source**: Direct port from [UnityPy/classes/Mesh.py](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py)

**Reference Implementation**: [src/UnityAssetParser/Services/MeshParser.cs](../src/UnityAssetParser/Services/MeshParser.cs) (lines 470-620)

---

## Key Principles

1. **Alignment**: 4-byte alignment **required after every byte array, bool triplet, and complex struct**
2. **String Format**: Length-prefixed UTF-8 (4-byte int + N bytes, no null terminator)
3. **Arrays**: Stored as length-prefixed (4-byte count + data)
4. **Endianness**: Determined by SerializedFile header (little-endian typical, big-endian on console platforms)
5. **Version Gates**: Certain fields only read if `major >= N`

---

## Field Order Table (v22 / Unity 2022.3+)

| # | Field Name | Type | Bytes | Alignment | Version Gate | Notes |
|---|---|---|---|---|---|---|
| 1 | `m_Name` | String (len-prefixed) | 4 + N | ✓ 4-byte | Always | UTF-8, 4-byte align after string bytes |
| 2 | `m_SubMeshes` | Array[SubMesh] | 4 + (N × 16) | ✓ 4-byte | Always | Count (4) + array elements, see SubMesh struct below |
| 3 | `m_Shapes` | BlendShapeData (struct) | Variable | ✓ 4-byte | Always | Contains 4 internal arrays (vertices, shapes, channels, fullWeights) |
| 4 | `m_BindPose` | Array[Matrix4x4f] | 4 + (N × 64) | ✓ 4-byte | >= 4 | Count (4) + 16 floats per matrix |
| 5 | `m_BoneNameHashes` | Array[uint32] | 4 + (N × 4) | ✓ 4-byte | >= 4 | CRC32 hashes of bone names |
| 6 | `m_RootBoneNameHash` | uint32 | 4 | (none) | >= 4 | CRC32 hash of root bone |
| 7 | `m_BonesAABB` | Array[AABB] | 4 + (N × 24) | ✓ 4-byte | >= 4 | Count + 2×Vector3f per AABB (6 floats = 24 bytes) |
| 8 | `m_VariableBoneCountWeights` | VariableBoneCountWeights (struct) | Variable | ✓ 4-byte | >= 4 | See VariableBoneCountWeights struct below |
| 9 | `m_MeshCompression` | byte | 1 | (none) | Always | 0=None, 1=Low, 2=Medium, 3=High |
| 10 | `m_IsReadable` | bool | 1 | (none) | Always | CPU readable flag |
| 11 | `m_KeepVertices` | bool | 1 | (none) | Always | Keep uncompressed vertices in memory |
| 12 | `m_KeepIndices` | bool | 1 | ✓ 4-byte | Always | Keep uncompressed indices in memory; **align after 3 bools** |
| 13 | `m_IndexFormat` | int32 | 4 | (none) | >= 2017.3 | 0=UInt16, 1=UInt32 |
| 14 | `m_IndexBuffer` | Array[byte] | 4 + N | ✓ 4-byte | Always | Count (4) + raw index bytes |
| 15 | `m_VertexData` | VertexData (struct) | Variable | ✓ 4-byte | Always | Vertex attribute channels and streams; see VertexData struct below |
| 16 | `m_CompressedMesh` | CompressedMesh (struct) | Variable | ✓ 4-byte | Conditional | Only if `m_MeshCompression != 0`; else skip |
| 17 | `m_LocalAABB` | AABB (struct) | 24 | ✓ 4-byte | Always | 2×Vector3f (min, max) |
| 18 | `m_MeshUsageFlags` | int32 | 4 | (none) | >= 5 | GPU usage flags |
| 19 | `m_CookingOptions` | int32 | 4 | (none) | Always | Physics cooking options |
| 20 | `m_BakedConvexCollisionMesh` | Array[byte] | 4 + N | ✓ 4-byte | Always | Convex collision mesh; align after bytes |
| 21 | `m_BakedTriangleCollisionMesh` | Array[byte] | 4 + N | ✓ 4-byte | Always | Triangle collision mesh; align after bytes |
| 22 | `m_MeshMetrics[0]` | float | 4 | (none) | Always | Metric 0 (e.g., blend shape vertex count) |
| 23 | `m_MeshMetrics[1]` | float | 4 | (none) | Always | Metric 1 (e.g., blend shape frame count) |
| 24 | `m_StreamData` | StreamingInfo (struct) | 36 | (none) | Always | External .resS resource reference; see StreamingInfo struct |

---

## SubMesh Structure

Ordered within `m_SubMeshes` array:

| Field | Type | Bytes | Notes |
|---|---|---|---|
| `firstByte` | uint32 | 4 | Index buffer offset in bytes |
| `indexCount` | uint32 | 4 | Triangle count × 3 |
| `topology` | int32 | 4 | 0=Triangles, 1=Quads, 2=LineStrip |
| `baseVertex` | uint32 | 4 | Vertex offset for this submesh |
| `firstVertex` | uint32 | 4 | First valid vertex index |
| `vertexCount` | uint32 | 4 | Number of vertices in submesh |
| `localAABB` | AABB | 24 | Local bounding box (v3+) |

**Total per SubMesh**: 52 bytes (v3+), 28 bytes (v2)

---

## VertexData Structure

Ordered within the `m_VertexData` field:

| Field | Type | Bytes | Alignment | Notes |
|---|---|---|---|---|
| `m_CurrentChannels` | uint32 | 4 | (none) | Bit flags: bit0=Position, bit1=Normal, bit2=Tangent, ... |
| `m_VertexCount` | uint32 | 4 | (none) | Number of vertices |
| `m_Channels` | Array[ChannelInfo] | 4 + (N × 4) | ✓ 4-byte | Count + (stream, offset, format, dimension per channel) |
| `m_StreamData` | Array[StreamInfo] | 4 + (N × 24) | ✓ 4-byte | Count + stream metadata (stride, alignment, data offset, size) |

**ChannelInfo per element** (4 bytes):
- `stream` (byte) - which stream (0-3)
- `offset` (byte) - byte offset within stream
- `format` (byte) - format ID (0=Float, 1=Float16, ...)
- `dimension` (byte) - components (1-4)

**StreamInfo per element** (24 bytes):
- `channelMask` (uint32) - channels in this stream
- `offset` (uint32) - byte offset in external resource
- `stride` (uint32) - bytes per vertex
- `align` (uint32) - alignment requirement
- `divideByInstanceCount` (int32) - instancing divisor
- `frequency` (uint32) - stream frequency

---

## CompressedMesh Structure

Ordered within the `m_CompressedMesh` field (only if `m_MeshCompression != 0`):

| Field | Type | Bytes | Alignment | Notes |
|---|---|---|---|---|
| `m_Vertices` | PackedBitVector | Variable | ✓ 4-byte | Compressed vertex positions |
| `m_UV` | PackedBitVector | Variable | ✓ 4-byte | Compressed UV0 texcoords |
| `m_Normals` | PackedBitVector | Variable | ✓ 4-byte | Compressed normals (if present) |
| `m_NormalSigns` | PackedBitVector | Variable | ✓ 4-byte | Normal sign bits |
| `m_Tangents` | PackedBitVector | Variable | ✓ 4-byte | Compressed tangents |
| `m_TangentSigns` | PackedBitVector | Variable | ✓ 4-byte | Tangent sign bits |
| `m_Weights` | PackedBitVector | Variable | ✓ 4-byte | Bone weights |
| `m_BoneIndices` | PackedBitVector | Variable | ✓ 4-byte | Bone indices |
| `m_Triangles` | PackedBitVector | Variable | ✓ 4-byte | Index data (compressed) |
| `m_FloatColors` | PackedBitVector | Variable | ✓ 4-byte | Packed color data |

**PackedBitVector structure** (see [Helpers/PackedBitVector.cs](../src/UnityAssetParser/Helpers/PackedBitVector.cs)):
- `m_NumItems` (uint32) - element count
- `m_Range` (float) - quantization range
- `m_Start` (float) - quantization start value
- `m_Data` (Array[byte]) - compressed bit data; **4-byte align after bytes**
- `m_BitSize` (byte) - bits per element; **4-byte align after byte**

---

## AABB Structure (Always 24 bytes)

| Field | Type | Bytes | Notes |
|---|---|---|---|
| `m_Center` | Vector3f | 12 | 3 × float32 |
| `m_Extent` | Vector3f | 12 | 3 × float32 |

---

## StreamingInfo Structure (Always 36 bytes)

Used for external .resS resources:

| Field | Type | Bytes | Notes |
|---|---|---|---|
| `m_Path` | string | 4 + N | Path to .resS file (e.g., "archive:/CAB-hash/CAB-hash.resS") |
| `m_Offset` | uint64 | 8 | Byte offset within .resS |
| `m_Size` | uint32 | 4 | Byte size of data |

---

## VariableBoneCountWeights Structure

Used for advanced skinning (v4+):

| Field | Type | Bytes | Notes |
|---|---|---|---|
| `m_Data` | Array[byte] | 4 + N | Raw weight data |
| `m_CurveRanges` | Array[uint32] | 4 + (N × 4) | Curve range values |

---

## BlendShapeData Structure

Used for morph targets / blend shapes:

| Field | Type | Bytes | Alignment | Notes |
|---|---|---|---|---|
| `m_Vertices` | Array[BlendShapeVertex] | 4 + (N × 16) | ✓ 4-byte | Position (Vector3f) + FrameIndex (uint32) |
| `m_Shapes` | Array[MeshBlendShape] | 4 + (N × 8) | ✓ 4-byte | FirstVertex (uint32) + VertexCount (uint32) |
| `m_Channels` | Array[MeshBlendShapeChannel] | Variable | ✓ 4-byte | Name (string) + NameHash, FrameIndex, FrameCount |
| `m_FullWeights` | Array[float] | 4 + (N × 4) | (none) | Per-frame full weights |

---

## Critical Alignment Points

These are the **most error-prone** locations:

1. **After m_Name**: Must align to 4 bytes (string is var-length)
2. **After every byte array** (IndexBuffer, BakedCollisionMeshes): **Must** align to 4 bytes
3. **After 3 bools** (m_MeshCompression, m_IsReadable, m_KeepVertices, m_KeepIndices): Align to 4 bytes
4. **After PackedBitVector data**: Each PBV's `m_Data` is followed by 4-byte alignment
5. **After CompressedMesh**: If present, align to 4 bytes after last PackedBitVector
6. **After VertexData**: Align to 4 bytes after channels/streams arrays

---

## Version-Specific Reading Logic

### For major >= 4
Read fields 4-8 (BindPose through VariableBoneCountWeights):
```csharp
if (major >= 4)
{
    m_BindPose = ReadArray<Matrix4x4f>();        // Field 4
    m_BoneNameHashes = ReadArray<uint32>();      // Field 5
    m_RootBoneNameHash = reader.ReadUInt32();     // Field 6
    m_BonesAABB = ReadArray<AABB>();             // Field 7
    m_VariableBoneCountWeights = ReadVBCW();      // Field 8
}
```

### For major >= 5
Read field 18 (MeshUsageFlags):
```csharp
if (major >= 5)
{
    m_MeshUsageFlags = reader.ReadInt32();        // Field 18
}
```

### For major >= 2017 and minor >= 3 (2017.3+)
Read field 13 (IndexFormat):
```csharp
if (major > 2017 || (major == 2017 && minor >= 3))
{
    m_IndexFormat = reader.ReadInt32();           // Field 13
}
```

### For CompressedMesh (Field 16)
Only read if `m_MeshCompression != 0`:
```csharp
if (m_MeshCompression != 0)
{
    m_CompressedMesh = ReadCompressedMesh();      // Field 16
}
else
{
    // Create empty CompressedMesh with all PackedBitVectors initialized
}
```

---

## String Format Details

### Length-Prefixed String (m_Name, m_Path in StreamingInfo)

**Binary layout**:
```
[4 bytes: int32 length] [N bytes: UTF-8 string data]
[padding to 4-byte boundary]
```

**Example**: "Cigar" (5 bytes)
```
05 00 00 00  43 69 67 61 72  00 00 00
|-- len=5 -|  C  i  g  a  r  |-- pad --|
```

**Critical**: 
- Length includes **ONLY** UTF-8 bytes, not the 4-byte length prefix itself
- After reading N bytes of string, calculate alignment: `align = (4 - (4 + N) % 4) % 4`
- Must read alignment padding bytes

---

## Float32 Quantization (PackedBitVector)

When decompressing packed vertex data:

```csharp
float quantized_value = bit_value * (range / ((1 << bit_size) - 1)) + start;
```

Where:
- `bit_value` = extracted from `PackedBitVector.Data` (0 to `(1 << bit_size) - 1`)
- `range` = `PackedBitVector.Range`
- `start` = `PackedBitVector.Start`
- `bit_size` = `PackedBitVector.BitSize`

---

## External Resource Reference (.resS)

For meshes with `StreamingInfo.Path`:

1. Resolve the path in the bundle's `FileIdentifiers` list
2. Find the corresponding `.resS` node in the bundle
3. Extract bytes `[StreamingInfo.Offset : StreamingInfo.Offset + StreamingInfo.Size]` from `.resS` node data
4. Pass to vertex parser (VertexData channels determine interpretation)

Example:
```
StreamingInfo.Path = "archive:/CAB-1a2b3c/CAB-1a2b3c.resS"
StreamingInfo.Offset = 1024
StreamingInfo.Size = 8192

// In bundle:
node_1_data[1024 : 9216] = vertex data bytes
```

---

## Implementation Checklist

- [x] Read m_Name with 4-byte alignment
- [x] Read m_SubMeshes array with element count
- [x] Read m_Shapes (BlendShapeData struct)
- [x] Conditionally read m_BindPose if major >= 4
- [x] Conditionally read m_BoneNameHashes if major >= 4
- [x] Conditionally read m_RootBoneNameHash if major >= 4
- [x] Conditionally read m_BonesAABB if major >= 4
- [x] Conditionally read m_VariableBoneCountWeights if major >= 4
- [x] Read 3 bool fields (MeshCompression byte, IsReadable, KeepVertices, KeepIndices) with alignment after
- [x] Conditionally read m_IndexFormat if major >= 2017.3
- [x] Read m_IndexBuffer array with 4-byte alignment
- [x] Read m_VertexData struct with channels and streams
- [x] Conditionally read m_CompressedMesh if compression != 0
- [x] Read m_LocalAABB struct with 4-byte alignment
- [x] Conditionally read m_MeshUsageFlags if major >= 5
- [x] Read m_CookingOptions
- [x] Read m_BakedConvexCollisionMesh array with alignment
- [x] Read m_BakedTriangleCollisionMesh array with alignment
- [x] Read m_MeshMetrics[0] and [1] (2 floats)
- [x] Read m_StreamData (StreamingInfo struct)

---

## References

- **UnityPy Source**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py
- **Implementation**: [src/UnityAssetParser/Services/MeshParser.cs](../src/UnityAssetParser/Services/MeshParser.cs)
- **Mesh Class**: [src/UnityAssetParser/Classes/Mesh.cs](../src/UnityAssetParser/Classes/Mesh.cs)
- **PackedBitVector**: [src/UnityAssetParser/Helpers/PackedBitVector.cs](../src/UnityAssetParser/Helpers/PackedBitVector.cs)

---

## Test Fixtures

Validated against real Thunderstore mod meshes:

| Mod | Mesh Name | Vertices | Triangles | Status |
|---|---|---|---|---|
| Cigar | Cigar_neck | 220 | 132 | ✓ Parsed |
| FrogHatSmile | Model_1 | 500 | 300 | ✓ Parsed |
| BambooCopter | Blade | 533 | 320 | ✓ Parsed |
| Glasses | Frame | ~600 | ~400 | ⚠️ Testing |

---

## Changelog

- **2026-02-01**: Initial documentation (v22 format)
- **Reference**: UnityPy commit `K0lb3/UnityPy@master`
