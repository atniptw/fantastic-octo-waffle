# Mesh Binary Field Order - Execution Summary

**Research Date**: February 1, 2026  
**Unity Version**: 2022.3 (v22 SerializedFile format)  
**Source**: UnityPy Mesh.py port  
**Implementation**: [MeshParser.cs](../src/UnityAssetParser/Services/MeshParser.cs)

---

## STRUCTURED FIELD TABLE (as requested)

| Field Name | Type | Bytes | Alignment | Version Gate | Notes |
|---|---|---|---|---|---|
| m_Name | String | 4+N | ✓ 4-byte | Always | Length-prefixed UTF-8, align after string |
| m_SubMeshes | Array[SubMesh] | 4+52N | ✓ 4-byte | Always | 7 fields per SubMesh, v3+ has AABB |
| m_Shapes | BlendShapeData | Variable | ✓ 4-byte | Always | 4 internal arrays (vertices, shapes, channels, weights) |
| m_BindPose | Array[Matrix4x4f] | 4+64N | ✓ 4-byte | ≥ 4 | 16 floats per matrix = 64 bytes |
| m_BoneNameHashes | Array[uint32] | 4+4N | ✓ 4-byte | ≥ 4 | CRC32 hashes for skeleton matching |
| m_RootBoneNameHash | uint32 | 4 | — | ≥ 4 | Root bone CRC32 hash |
| m_BonesAABB | Array[AABB] | 4+24N | ✓ 4-byte | ≥ 4 | 2×Vector3f (center, extent) per bone |
| m_VariableBoneCountWeights | VariableBoneCountWeights | Variable | ✓ 4-byte | ≥ 4 | Byte array + CurveRanges array |
| m_MeshCompression | byte | 1 | — | Always | 0=None, 1=Low, 2=Medium, 3=High |
| m_IsReadable | bool | 1 | — | Always | CPU readable flag |
| m_KeepVertices | bool | 1 | — | Always | Keep uncompressed vertices |
| m_KeepIndices | bool | 1 | ✓ 4-byte | Always | **CRITICAL: align after these 4 bytes** |
| m_IndexFormat | int32 | 4 | — | ≥ 2017.3 | 0=UInt16, 1=UInt32; v2017.3+ only |
| m_IndexBuffer | Array[byte] | 4+N | ✓ 4-byte | Always | Triangle indices (raw bytes) |
| m_VertexData | VertexData | Variable | ✓ 4-byte | Always | Channels, streams (positions, normals, UVs, tangents) |
| m_CompressedMesh | CompressedMesh | Variable | ✓ 4-byte | Conditional | **ONLY if MeshCompression ≠ 0** |
| m_LocalAABB | AABB | 24 | ✓ 4-byte | Always | Mesh bounding box (min, max Vector3f) |
| m_MeshUsageFlags | int32 | 4 | — | ≥ 5 | GPU usage flags (dynamic, streaming) |
| m_CookingOptions | int32 | 4 | — | Always | Physics cooking parameters |
| m_BakedConvexCollisionMesh | Array[byte] | 4+N | ✓ 4-byte | Always | PhysX convex collision data |
| m_BakedTriangleCollisionMesh | Array[byte] | 4+N | ✓ 4-byte | Always | PhysX triangle collision data |
| m_MeshMetrics[0] | float | 4 | — | Always | Metric 0 (blend shape vertex count) |
| m_MeshMetrics[1] | float | 4 | — | Always | Metric 1 (blend shape frame count) |
| m_StreamData | StreamingInfo | 36 | — | Always | External .resS resource reference |

**N** = array element count

---

## FIELD READING SEQUENCE (Pseudo-code)

```
Position 0:
1.  name = ReadAlignedString()           // 4-byte align after var-length string
    Align(4)

2.  subMeshes = ReadArray<SubMesh>()    // 4-byte align after array
    Align(4)

3.  shapes = ReadBlendShapeData()        // 4-byte align after struct
    Align(4)

4.  IF (major >= 4):
        bindPose = ReadArray<Matrix4x4f>()      // 4-byte align after array
        Align(4)

5.  IF (major >= 4):
        boneNameHashes = ReadArray<uint32>()    // 4-byte align after array
        Align(4)

6.  IF (major >= 4):
        rootBoneNameHash = ReadUInt32()         // No alignment needed

7.  IF (major >= 4):
        bonesAABB = ReadArray<AABB>()           // 4-byte align after array
        Align(4)

8.  IF (major >= 4):
        variableBoneCountWeights = ReadVBCW()   // 4-byte align after struct
        Align(4)

9.  meshCompression = ReadByte()        // No alignment yet

10. isReadable = ReadBool()             // No alignment yet

11. keepVertices = ReadBool()           // No alignment yet

12. keepIndices = ReadBool()            // **MUST align after these 4 bytes**
    Align(4)

13. IF (major > 2017 || (major == 2017 && minor >= 3)):
        indexFormat = ReadInt32()               // No alignment needed

14. indexBuffer = ReadArray<byte>()     // 4-byte align after array
    Align(4)

15. vertexData = ReadVertexData()       // 4-byte align after struct
    Align(4)

16. IF (meshCompression != 0):
        compressedMesh = ReadCompressedMesh()   // 4-byte align after struct
        Align(4)
    ELSE:
        compressedMesh = CreateEmpty()

17. localAABB = ReadAABB()              // 4-byte align after struct
    Align(4)

18. IF (major >= 5):
        meshUsageFlags = ReadInt32()            // No alignment needed

19. cookingOptions = ReadInt32()        // No alignment needed

20. bakedConvexCollisionMesh = ReadArray<byte>()     // 4-byte align after array
    Align(4)

21. bakedTriangleCollisionMesh = ReadArray<byte>()   // 4-byte align after array
    Align(4)

22. meshMetrics[0] = ReadFloat()        // No alignment needed

23. meshMetrics[1] = ReadFloat()        // No alignment needed

24. streamData = ReadStreamingInfo()    // No alignment (end of object)
```

---

## CRITICAL ALIGNMENT CHECKLIST

✓ After `m_Name` (string is variable-length)  
✓ After `m_SubMeshes` array  
✓ After `m_Shapes` struct  
✓ After `m_BindPose` array (if present)  
✓ After `m_BoneNameHashes` array (if present)  
✓ After `m_BonesAABB` array (if present)  
✓ After `m_VariableBoneCountWeights` struct (if present)  
✓ **After 3-bool triplet** (m_MeshCompression, m_IsReadable, m_KeepVertices, m_KeepIndices = **MOST IMPORTANT**)  
✓ After `m_IndexBuffer` array  
✓ After `m_VertexData` struct  
✓ After `m_CompressedMesh` struct (if compression != 0)  
✓ After `m_LocalAABB` struct  
✓ After `m_BakedConvexCollisionMesh` array  
✓ After `m_BakedTriangleCollisionMesh` array  

---

## VERSION GATES (Conditional Reads)

### Fields requiring version >= 4
- m_BindPose (bone transformation matrices)
- m_BoneNameHashes (bone identification)
- m_RootBoneNameHash (skeleton root)
- m_BonesAABB (per-bone bounds)
- m_VariableBoneCountWeights (advanced skinning)

### Fields requiring version >= 5
- m_MeshUsageFlags (GPU optimization flags)

### Fields requiring version >= 2017.3
- m_IndexFormat (index size selection)

### Fields conditional on compression
- m_CompressedMesh: **Only read if `m_MeshCompression != 0`**

---

## INLINE STRUCT DETAILS

### SubMesh (52 bytes for v3+)
```
[0]   firstByte (uint32)         = index buffer byte offset
[4]   indexCount (uint32)        = triangle count × 3
[8]   topology (int32)           = 0=Triangles, 1=Quads, 2=LineStrip
[12]  baseVertex (uint32)        = vertex offset for this submesh
[16]  firstVertex (uint32)       = first valid vertex index
[20]  vertexCount (uint32)       = number of vertices
[24]  localAABB (AABB = 24 bytes) = bounding box (v3+ only)
```

### AABB (24 bytes)
```
[0]   m_Center (Vector3f = 12 bytes)  = 3 floats (x, y, z)
[12]  m_Extent (Vector3f = 12 bytes)  = 3 floats (half-width per axis)
```

### VertexData (Variable)
```
[0]   m_CurrentChannels (uint32)   = channel bitmask
[4]   m_VertexCount (uint32)       = total vertices
[8]   m_Channels (Array[ChannelInfo])    = stream/format/dimension per channel
      m_StreamData (Array[StreamInfo])   = stride/offset/size per stream
```

### PackedBitVector (Variable)
```
[0]   m_NumItems (uint32)    = element count
[4]   m_Range (float)        = quantization range
[8]   m_Start (float)        = quantization start value
[12]  m_Data (Array[byte])   = bit-packed binary data [align after]
[?]   m_BitSize (byte)       = bits per element [align after]
```

### StreamingInfo (Min 36 bytes)
```
[0]   m_Path (String)    = "archive:/CAB-hash/CAB-hash.resS" [4-byte align after]
[?]   m_Offset (uint64)  = byte offset in .resS
[?]   m_Size (uint32)    = byte size
```

---

## COMMON PARSING ERRORS

1. **Missing alignment after byte arrays** → cascading data corruption
2. **Reading m_BindPose without checking version >= 4** → reads garbage
3. **Reading m_IndexFormat without checking version >= 2017.3** → offset mismatch
4. **Reading m_CompressedMesh unconditionally** → crashes if compression == 0
5. **Not aligning after bool triplet** → all subsequent fields shifted by 3 bytes
6. **Treating `m_DataSize` as length** → reads wrong number of bytes from PackedBitVector
7. **Not reading `m_StreamData` even if compression == 0** → external .resS data lost

---

## VALIDATION CRITERIA

✓ Field order matches byte-for-byte with UnityPy reference  
✓ All 4-byte alignment points enforced  
✓ Version gates correctly conditionally skip fields  
✓ String handling includes length-prefix + UTF-8 + alignment  
✓ Array counts precede array elements  
✓ CompressedMesh conditional read works correctly  
✓ StreamingInfo always read (even for uncompressed meshes)  

---

## REFERENCE DOCUMENTS

- [MESH_FIELD_ORDER_REFERENCE.md](./MESH_FIELD_ORDER_REFERENCE.md) - Full detailed specification
- [MESH_FIELD_ORDER_QUICK_REFERENCE.md](./MESH_FIELD_ORDER_QUICK_REFERENCE.md) - Extended reference with examples
- [MeshParser.cs](../src/UnityAssetParser/Services/MeshParser.cs) - C# implementation
- UnityPy: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py

---

*Generated: 2026-02-01 | Format: Unity 2022.3+ v22 SerializedFile*
