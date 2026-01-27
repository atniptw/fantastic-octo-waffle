# Mesh Class Field Parsing Order - UnityPy Reference

**Source**: UnityPy/classes/generated.py (auto-generated from TypeTree data)
**Reference**: UnityPy/classes/legacy_patch/Mesh.pyi (type hints)

This document provides the exact field order and parsing logic for the Mesh class, extracted directly from UnityPy for C# porting.

---

## Complete Mesh Field Order

The Mesh class inherits from `NamedObject` and has the following fields in **parse/serialization order**:

### Mandatory (Non-Optional) Fields - Parsed First
These are **always present** and must be read in this exact order:

1. **m_BindPose** → `List[Matrix4x4f]` — Bone pose transforms (list of 4x4 matrices)
2. **m_CompressedMesh** → `CompressedMesh` — Compressed mesh data (struct)
3. **m_IndexBuffer** → `List[int]` — Triangle indices
4. **m_LocalAABB** → `AABB` — Axis-aligned bounding box (struct with min/max Vector3f)
5. **m_MeshCompression** → `int` — Compression level (0=None, 1=Low, 2=Medium, 3=High)
6. **m_MeshUsageFlags** → `int` — Usage flags bitmask
7. **m_Name** → `str` — Object name
8. **m_SubMeshes** → `List[SubMesh]` — Submesh array

### Optional Fields - Parsed If Present
These fields appear **conditionally** based on Unity version and file structure:

9. **m_BakedConvexCollisionMesh** → `Optional[List[int]]` — Pre-baked convex collision data
10. **m_BakedTriangleCollisionMesh** → `Optional[List[int]]` — Pre-baked triangle collision data
11. **m_BoneNameHashes** → `Optional[List[int]]` — Hash values of bone names
12. **m_BonesAABB** → `Optional[List[MinMaxAABB]]` — Per-bone bounding boxes
13. **m_CollisionTriangles** → `Optional[List[int]]` — Collision mesh triangle indices
14. **m_CollisionVertexCount** → `Optional[int]` — Collision vertex count
15. **m_Colors** → `Optional[List[ColorRGBA]]` — Vertex colors (RGBA, 0-255 range)
16. **m_CookingOptions** → `Optional[int]` — Physics cooking options bitmask
17. **m_IndexFormat** → `Optional[int]` — Index buffer format (0=16-bit, 1=32-bit) [2017.3+]
18. **m_IsReadable** → `Optional[bool]` — CPU-readable flag
19. **m_KeepIndices** → `Optional[bool]` — Keep indices flag
20. **m_KeepVertices** → `Optional[bool]` — Keep vertices flag
21. **m_MeshMetrics_0_** → `Optional[float]` — Mesh metric (usage)
22. **m_MeshMetrics_1_** → `Optional[float]` — Mesh metric (size)
23. **m_Normals** → `Optional[List[Vector3f]]` — Vertex normals
24. **m_RootBoneNameHash** → `Optional[int]` — Root bone name hash
25. **m_ShapeVertices** → `Optional[List[MeshBlendShapeVertex]]` — Blend shape vertices
26. **m_Shapes** → `Optional[Union[BlendShapeData, List[MeshBlendShape]]]` — Blend shapes (version-dependent type)
27. **m_Skin** → `Optional[Union[List[BoneInfluence], List[BoneWeights4]]]` — Bone weights/influences (version-dependent)
28. **m_StreamCompression** → `Optional[int]` — Vertex stream compression type
29. **m_StreamData** → `Optional[StreamingInfo]` — External resource reference for vertex data
30. **m_Tangents** → `Optional[List[Vector4f]]` — Vertex tangents (XYZW)
31. **m_UV** → `Optional[List[Vector2f]]` — UV channel 0 (maps to m_UV0 in processing)
32. **m_UV1** → `Optional[List[Vector2f]]` — UV channel 1
33. **m_Use16BitIndices** → `Optional[int]` — Force 16-bit index buffer [2017.3 and earlier]
34. **m_VariableBoneCountWeights** → `Optional[VariableBoneCountWeights]` — Variable bone count data
35. **m_VertexData** → `Optional[VertexData]` — Modern vertex data container [3.5+] (replaces individual vertex arrays)
36. **m_Vertices** → `Optional[List[Vector3f]]` — Vertex positions (XYZ)

---

## Version-Specific Parsing Logic

### Index Format Handling (m_IndexFormat vs m_Use16BitIndices)

```python
# From UnityPy/helpers/MeshHelper.py line 159-170
if self.version >= (4, 0):
    # Version 4.0+: No m_Use16BitIndices, use m_IndexFormat
    # m_IndexFormat: 0 = 16-bit, 1 = 32-bit
    self.m_Use16BitIndices = mesh.m_IndexFormat == 0

elif self.version >= (2017, 4) or (self.version[:2] == (2017, 3) and mesh.m_MeshCompression == 0):
    # Version 2017.3+ with uncompressed mesh: use m_IndexFormat
    self.m_Use16BitIndices = mesh.m_IndexFormat == 0

else:
    # Earlier versions: direct m_Use16BitIndices value
    self.m_Use16BitIndices = bool(mesh.m_Use16BitIndices)
```

### VertexData vs Legacy Vertex Arrays

```python
# From UnityPy/helpers/MeshHelper.py line 110-145
if self.version[0] < 4:
    # Version 3.x: Use legacy m_Streams_0_, m_Streams_1_, m_Streams_2_, m_Streams_3_
    # These are individual StreamInfo objects
    m_Streams = [
        vertex_data.m_Streams_0_,
        vertex_data.m_Streams_1_,
        vertex_data.m_Streams_2_,
        vertex_data.m_Streams_3_,
    ]
    m_Channels = self.get_channels(m_Streams)

elif self.version[0] == 4:
    # Version 4.x: m_Streams array + m_Channels array
    m_Streams = vertex_data.m_Streams
    m_Channels = vertex_data.m_Channels

else:
    # Version 5.x+: Only m_Channels, derive m_Streams from channel info
    m_Channels = vertex_data.m_Channels
    m_Streams = self.get_streams(m_Channels, vertex_data.m_VertexCount)
```

### Compressed vs Uncompressed Mesh

```python
# From UnityPy/helpers/MeshHelper.py line 185-196
# Process VertexData if version >= 3.5
if self.version >= (3, 5):
    self.read_vertex_data(m_Channels, m_Streams)

# Process CompressedMesh if version >= 2.6
if isinstance(mesh, Mesh) and self.version >= (2, 6):
    self.decompress_compressed_mesh()
```

---

## 4-Byte Alignment Rules

**Critical**: After reading byte arrays and certain field types, **4-byte alignment MUST be applied**.

```csharp
// Pseudo-code: 4-byte alignment logic
private void Align4(EndianBinaryReader reader)
{
    int position = reader.Position;
    int remainder = position % 4;
    if (remainder != 0)
    {
        reader.Position += 4 - remainder;  // Pad to next 4-byte boundary
    }
}

// Applied after:
// - byte arrays (m_IndexBuffer, m_VertexData.m_DataSize, etc.)
// - bool triplets (3 consecutive bools)
// - List[int] / List[byte] serialization
// - Any unpadded field type
```

**Reference**: UnityPyBoost/TypeTreeHelper.cpp line 854-866 shows `align4(reader)` called after complex types.

---

## VertexData Structure Details

### For Version 3.x
```csharp
public class VertexData
{
    public int m_VertexCount;
    public List<StreamInfo> m_Streams;  // Fixed 4 streams (0-3)
    // Legacy: m_Streams_0_, m_Streams_1_, m_Streams_2_, m_Streams_3_
}
```

### For Version 4.x
```csharp
public class VertexData
{
    public int m_VertexCount;
    public List<StreamInfo> m_Streams;          // Variable count
    public List<ChannelInfo> m_Channels;        // 6 channels
    public byte[] m_DataSize;                   // Vertex data binary
}
```

### For Version 5.x+
```csharp
public class VertexData
{
    public int m_VertexCount;
    public List<ChannelInfo> m_Channels;        // 6 channels (positions 0-5)
    public byte[] m_DataSize;                   // Vertex data binary
    // Streams are derived from ChannelInfo.stream indices
}
```

### StreamInfo Structure
```csharp
public class StreamInfo
{
    public uint32_t m_ChannelMask;      // Bitmask of active channels in this stream
    public uint32_t m_Offset;           // Offset in m_DataSize
    public uint32_t m_Stride;           // Bytes per vertex
    public uint32_t m_DivisorIndex;     // For instanced rendering
}
```

### ChannelInfo Structure
```csharp
public class ChannelInfo
{
    public uint8_t m_Stream;            // Which stream (0-3)
    public uint8_t m_Offset;            // Offset within stream
    public uint8_t m_Format;            // Vertex format (version-dependent enum)
    public uint8_t m_Dimension;         // Component count (1-4, 0=unused)
}
```

---

## IndexBuffer & SubMeshes Parsing

### IndexBuffer Format
```csharp
// Read index buffer based on m_Use16BitIndices flag
if (mesh.m_Use16BitIndices)
{
    // 2 bytes per index, interpret as uint16
    uint16_t[] indices = reader.ReadArray<uint16_t>(indexCount);
    mesh.m_IndexBuffer = indices.Select(i => (int)i).ToList();
}
else
{
    // 4 bytes per index, interpret as uint32
    uint32_t[] indices = reader.ReadArray<uint32_t>(indexCount);
    mesh.m_IndexBuffer = indices.Select(i => (int)i).ToList();
}
```

### SubMesh Structure
```csharp
public class SubMesh
{
    public uint32_t firstByte;      // Offset into index buffer (in bytes)
    public uint32_t indexCount;     // Number of indices in this submesh
    public int topology;            // MeshTopology enum (0=Triangles, 1=TriangleStrip, etc.)
    public uint32_t baseVertex;     // Vertex offset [2.6+]
    public uint32_t firstVertex;    // [4.0+]
    public uint32_t vertexCount;    // [4.0+]
    public int indexStart;          // [2017.2+]
    public int indexCount_alt;      // [2017.2+]
}
```

---

## StreamingInfo (External Resource Reference)

Used to reference external `.resS` files for vertex data:

```csharp
public class StreamingInfo
{
    public string path;             // File path within asset bundle (e.g., "data.resS")
    public uint64_t offset;         // Byte offset in resource file
    public uint32_t size;           // Byte size of vertex data
}
```

**Processing**:
1. If `mesh.m_StreamData` is present and `path` is non-empty, external data must be loaded
2. Resolved via `get_resource_data()` from bundle reader
3. Binary data is assigned to `m_VertexData.m_DataSize`

**Reference**: UnityPy/helpers/MeshHelper.py line 134-150

---

## CompressedMesh Handling

### CompressedMesh Structure
```csharp
public class CompressedMesh
{
    public PackedBitVector m_Vertices;          // Compressed position data
    public PackedBitVector m_UV;                // Compressed UV0 data
    public PackedBitVector m_Normals;           // Compressed normal data
    public PackedBitVector m_Tangents;          // Compressed tangent data
    public PackedBitVector m_Colors;            // Compressed color data
    public PackedBitVector m_BoneIndices;       // Compressed bone indices
    public PackedBitVector m_BoneWeights;       // Compressed bone weights
    public PackedBitVector m_Triangles;         // Compressed triangle indices
    public uint32_t m_UVInfo;                   // UV channel bitmask info
}
```

### Decompression Logic
```python
# From UnityPy/helpers/MeshHelper.py line 463-530
def decompress_compressed_mesh(self):
    m_CompressedMesh = self.src.m_CompressedMesh
    
    # Vertices: 3 floats per vertex, stored as packed bits
    if m_CompressedMesh.m_Vertices.m_NumItems > 0:
        self.m_VertexCount = m_CompressedMesh.m_Vertices.m_NumItems // 3
        self.m_Vertices = unpack_floats(m_CompressedMesh.m_Vertices, shape=(3,))
    
    # UVs: Per-channel extraction from m_UV based on m_UVInfo
    if m_CompressedMesh.m_UV.m_NumItems > 0:
        m_UVInfo = m_CompressedMesh.m_UVInfo
        # UV channels: extract based on kInfoBitsPerUV = 4 bits per channel
        # Bits [0:2] = dimension (2D/3D)
        # Bit [2] = channel exists flag
        # Bits [3:7] = next channel offset
        for uv_channel in range(8):
            # Extract channel data from m_UV bit vector
    
    # Normals: Apply scaling from min/max bounds
    # Indices: Unpack from triangle bit vector
    # Colors: Expand from RGBA8 format
```

---

## C# Porting Checklist

### Field Reading Order
- [ ] Read 8 mandatory fields in exact order (m_BindPose through m_SubMeshes)
- [ ] Read optional fields in documented order
- [ ] Apply 4-byte alignment after byte arrays
- [ ] Handle version-specific field presence

### Version Checks
- [ ] Check `version >= (3, 5)` for VertexData processing
- [ ] Check `version >= (2, 6)` for CompressedMesh
- [ ] Check `version[0] < 4` for legacy stream format (m_Streams_0_ through m_Streams_3_)
- [ ] Check `version >= (2017, 3)` for m_IndexFormat behavior

### VertexData Processing
- [ ] Handle m_Streams based on version (legacy 3.x vs dynamic 4.x+)
- [ ] Handle m_Channels for versions 4.0+
- [ ] Load external resource if m_StreamData.path is present
- [ ] Calculate stream offsets/strides for vertex unpacking

### Index/Submesh Handling
- [ ] Determine 16-bit vs 32-bit indices from m_Use16BitIndices or m_IndexFormat
- [ ] Read SubMesh array with version-appropriate field count
- [ ] Calculate firstByte offset for each submesh
- [ ] Handle baseVertex/firstVertex for version 4.0+

### CompressedMesh
- [ ] Check m_MeshCompression flag
- [ ] Unpack PackedBitVector for vertices, normals, UVs, colors, indices
- [ ] Apply min/max scaling to unpacked values
- [ ] Reconstruct full mesh from compressed data

---

## Key References from UnityPy

- **Main Mesh parsing**: UnityPy/classes/generated.py (auto-generated, see line 4416-4452)
- **Legacy type hints**: UnityPy/classes/legacy_patch/Mesh.pyi
- **Mesh processing**: UnityPy/helpers/MeshHelper.py (lines 71-625)
- **VertexData unpacking**: UnityPy/helpers/MeshHelper.py (lines 110-372)
- **CompressedMesh decompression**: UnityPy/helpers/MeshHelper.py (lines 463-615)
- **PackedBitVector unpacking**: UnityPy/helpers/PackedBitVector.py (unpack_floats, unpack_ints)
- **4-byte alignment**: UnityPyBoost/TypeTreeHelper.cpp line 854-866 (C++ reference)

---

## Summary

**The Mesh class ALWAYS reads fields in this strict order**:
1. **Mandatory** (8 fields): BindPose, CompressedMesh, IndexBuffer, LocalAABB, MeshCompression, MeshUsageFlags, Name, SubMeshes
2. **Optional** (28 fields): Rest in documented order, presence depends on version and TypeTree structure
3. **4-byte alignment** applied after byte arrays and after specific field combinations
4. **Version checks** determine whether to use legacy streams (3.x), dynamic streams (4.x), or derived streams (5.x+)
5. **IndexBuffer format** (16-bit vs 32-bit) determined by m_Use16BitIndices or m_IndexFormat depending on version

This is a direct port from UnityPy's auto-generated classes without any interpretation or learning—copy field order verbatim.
