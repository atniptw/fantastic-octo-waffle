# EXACT IMPLEMENTATION PLAN - UnityPy Port (Reading Only)

This plan prioritizes fixes from highest-impact to lowest, with exact line references to UnityPy.

---

## PHASE 1: CORE PARSING INFRASTRUCTURE (Foundation)

### Fix 1.1: 4-Byte Alignment Audit & Fix

**UnityPy Reference**: Everywhere `read_bytes()` is followed by `align_stream(4)`

**Locations to Check/Fix**:

1. **PackedBitVector.cs** (line 114-145)
   - After reading m_Data: `reader.Align(4)` ✓ ALREADY DONE
   - After reading BitSize: `reader.Align(4)` ✓ ALREADY DONE
   - Status: VERIFY both alignments are present

2. **SerializedFile.cs** - TypeTree parsing
   - After reading string buffers in TypeTree
   - After reading node data
   - Status: AUDIT needed

3. **Mesh.cs** - VertexData parsing
   - After m_VertexData.m_DataSize byte array
   - Status: AUDIT needed

4. **BundleFile.cs** - BlocksInfo parsing
   - After compressed blocks info decompression
   - Status: AUDIT needed

**Action**: Search codebase for `reader.Read.*Bytes()` and verify `reader.Align(4)` follows

**Acceptance**: All byte array reads have 4-byte alignment after them

---

### Fix 1.2: BundleFile - Replace BlocksInfo Location Calculation

**UnityPy Reference**: `BundleFile.py` lines 130-141

**Current Implementation Problem**:
- Uses custom `UnityFSHeaderParser.CalculateBlocksInfoLocation()`
- Not in UnityPy - WRONG APPROACH

**Required Change**:

Replace:
```csharp
var blocksInfoLocation = headerParser.CalculateBlocksInfoLocation(header, stream.Length);
stream.Position = blocksInfoLocation.BlocksInfoPosition;
```

With:
```csharp
long start = stream.Position;
byte[] compressedBlocksInfo;

if ((header.DataFlags & 0x80) != 0)  // BlocksInfoAtTheEnd = 0x80
{
    stream.Position = stream.Length - header.CompressedBlocksInfoSize;
    compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
    stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
    stream.Position = start;
}
else  // BlocksAndDirectoryInfoCombined = 0x40
{
    compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
    stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
}
```

**File**: `src/UnityAssetParser/Bundle/BundleFile.cs` (around line 85-100)

**Acceptance**: Blocks info location matches UnityPy exactly

---

### Fix 1.3: BundleFile - Add BlockInfoNeedPaddingAtStart Alignment

**UnityPy Reference**: `BundleFile.py` lines 165-168

**Required Change**:

After decompressing BlocksInfo, add:
```csharp
if (header.DataFlags >= 7 && (header.DataFlags & 0x200) != 0)  // BlockInfoNeedPaddingAtStart
{
    reader.Align(16);
}
```

**Location**: `BundleFile.cs` after blocks info decompression, before reading block list

**File**: `src/UnityAssetParser/Bundle/BundleFile.cs` (around line 130-135)

**Acceptance**: 16-byte alignment applied for v7+ bundles with flag 0x200

---

### Fix 1.4: SerializedFile - Fix Header Parsing for v22+

**UnityPy Reference**: `SerializedFile.py` lines 258-267

**Current Implementation Problem**:
- May not read fields in exact order for v22+
- Field order is: metadata_size, file_size, data_offset, unknown (all 8-byte aligned)

**Required Change**:

Ensure v22+ header reads exactly:
```csharp
if (header.Version >= 22)
{
    header.MetadataSize = endianReader.ReadUInt32();
    header.FileSize = endianReader.ReadInt64();
    header.DataOffset = endianReader.ReadInt64();
    var unknown = endianReader.ReadInt64();
}
```

**File**: `src/UnityAssetParser/SerializedFile/SerializedFileHeader.cs` or `SerializedFile.cs`

**Acceptance**: Header fields read in exact order for v22+

---

### Fix 1.5: SerializedFile - Verify Endianness Read Position

**UnityPy Reference**: `SerializedFile.py` lines 234-241

**Current Implementation Problem**:
- Endianness is read AFTER base header fields
- Must be read as first byte of version-dependent fields
- Position-sensitive!

**Required Change**:

Verify sequence:
1. Read 4 header UInt32s (metadata_size, file_size, version, data_offset)
2. IF version >= 9: Read boolean (endianness), then 3 reserved bytes
3. THEN continue with version-dependent fields

**File**: `src/UnityAssetParser/SerializedFile/SerializedFile.cs` lines 100-120

**Acceptance**: Endianness read at exact position per UnityPy flow

---

## PHASE 2: CRITICAL PARSING PATHS

### Fix 2.1: SerializedFile - Type Tree Parsing Order

**UnityPy Reference**: `SerializedFile.py` lines 268-293

**Current Implementation Problem**:
- Order may be wrong: should be version string → platform → enableTypeTree → type count

**Required Order**:
```
if version >= 7:
    read unity_version (string_to_null)
if version >= 8:
    read target_platform (int)
if version >= 13:
    read enable_type_tree (boolean)
    
read type_count (int)
for each type:
    parse SerializedType
```

**File**: `src/UnityAssetParser/SerializedFile/SerializedFile.cs` lines 120-150

**Acceptance**: Metadata fields read in exact order

---

### Fix 2.2: SerializedFile - Object Table Parsing

**UnityPy Reference**: `SerializedFile.py` lines 284-293

**Required**:
```
if 7 <= version < 14:
    read big_id_enabled (int)

read object_count (int)
for each object:
    create ObjectInfo
```

**File**: `src/UnityAssetParser/SerializedFile/SerializedFile.cs`

**Acceptance**: Objects indexed by PathId, big_id_enabled read for v7-13

---

### Fix 2.3: BundleFile - Node Parsing Order

**UnityPy Reference**: `BundleFile.py` lines 153-163

**Exact Field Order**:
```
for each node:
    read offset (long/64-bit)
    read size (long/64-bit)
    read flags (uint/32-bit)
    read path (string_to_null)
```

**File**: `src/UnityAssetParser/Bundle/NodeExtractor.cs` or equivalent

**Acceptance**: Node fields parsed in exact order: offset, size, flags, path

---

## PHASE 3: MESH EXTRACTION (Complex)

### Fix 3.1: MeshHelper - Version Comparison

**UnityPy Reference**: `MeshHelper.py` lines 110-134

**Current Problem**:
- `CompareVersion()` may have wrong semantics
- Should be tuple comparison: (major, minor, patch, build)

**Required**:
```csharp
// Correct comparisons
if (CompareVersion(_version, (3, 5, 0, 0)) >= 0)  // >= 3.5
if (CompareVersion(_version, (2, 6, 0, 0)) >= 0)  // >= 2.6
if (CompareVersion(_version, (4, 0, 0, 0)) >= 0)  // >= 4.0
if (CompareVersion(_version, (2017, 4, 0, 0)) >= 0)  // >= 2017.4
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs`

**Acceptance**: Version comparisons match UnityPy semantics

---

### Fix 3.2: MeshHelper - Index Buffer Unpacking (Endianness)

**UnityPy Reference**: `MeshHelper.py` lines 159-175

**Current Problem**:
- Must respect binary endianness
- UnityPy uses: `struct.unpack(f"<{count}{fmt}", raw_indices)`
- Explicit little-endian format string

**Required**:
```csharp
private static uint[] UnpackIndexBuffer(byte[] rawIndices, bool use16Bit, bool isLittleEndian)
{
    if (use16Bit)
    {
        var count = rawIndices.Length / 2;
        var indices = new uint[count];
        for (int i = 0; i < count; i++)
        {
            ushort val;
            if (isLittleEndian)
                val = BitConverter.ToUInt16(rawIndices, i * 2);
            else
                val = (ushort)((rawIndices[i * 2] << 8) | rawIndices[i * 2 + 1]);
            indices[i] = val;
        }
        return indices;
    }
    // Similar for 32-bit...
}
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` UnpackIndexBuffer method

**Acceptance**: Endianness respected in index buffer unpacking

---

### Fix 3.3: MeshHelper - Channel/Stream Logic (Version-Dependent)

**UnityPy Reference**: `MeshHelper.py` lines 110-150

**Three Paths**:

**Path A: Version < 4 (m_Streams_0_ through m_Streams_3_)**
```python
m_Streams = [
    vertex_data.m_Streams_0_,
    vertex_data.m_Streams_1_,
    vertex_data.m_Streams_2_,
    vertex_data.m_Streams_3_,
]
m_Channels = self.get_channels(m_Streams)
```

**Path B: Version == 4 (m_Streams array + m_Channels array)**
```python
m_Streams = vertex_data.m_Streams
m_Channels = vertex_data.m_Channels
```

**Path C: Version >= 5 (m_Streams array + computed channels)**
```python
m_Channels = vertex_data.m_Channels
m_Streams = self.get_streams(m_Channels, vertex_data.m_VertexCount)
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` Extract() method

**Acceptance**: All three version-dependent paths implemented

---

### Fix 3.4: Mesh - Add Missing Fields

**UnityPy Reference**: `classes/generated.py` lines 4416-4452

**Add to Mesh.cs**:
```csharp
public float[]? BindPose { get; set; }          // Matrix4x4f list
public AABB? LocalAABB { get; set; }
public uint MeshUsageFlags { get; set; }
public int[]? BakedConvexCollisionMesh { get; set; }
public int[]? BakedTriangleCollisionMesh { get; set; }
public int[]? BoneNameHashes { get; set; }
public MinMaxAABB[]? BonesAABB { get; set; }
public int[]? CollisionTriangles { get; set; }
public uint? CollisionVertexCount { get; set; }
public ColorRGBA[]? Colors { get; set; }
public int? CookingOptions { get; set; }
public bool? IsReadable { get; set; }
public bool? KeepVertices { get; set; }
public bool? KeepIndices { get; set; }
public float? MeshMetrics0 { get; set; }
public float? MeshMetrics1 { get; set; }
public Vector3f[]? Normals { get; set; }
public int? RootBoneNameHash { get; set; }
public Vector4f[]? Tangents { get; set; }
public Vector2f[]? UV { get; set; }
public Vector2f[]? UV1 { get; set; }
public VariableBoneCountWeights? VariableBoneCountWeights { get; set; }
public Vector3f[]? Vertices { get; set; }
```

**File**: `src/UnityAssetParser/Classes/Mesh.cs`

**Acceptance**: All optional fields added to Mesh class

---

## PHASE 4: COMPRESSED MESH DECOMPRESSION

### Fix 4.1: DecompressCompressedMesh - Vertex Count Calculation

**UnityPy Reference**: `MeshHelper.py` lines 463-475

**Required**:
```csharp
// Vertex count from CompressedMesh
private void DecompressCompressedMesh(CompressedMesh compMesh)
{
    if (compMesh?.Vertices == null) return;
    
    // CRITICAL: VertexCount is derived, not stored
    _vertexCount = compMesh.Vertices.NumItems / 3;
    
    if (compMesh.Vertices.NumItems > 0)
    {
        _positions = UnpackFloatsThenReshape(compMesh.Vertices, null, (3,));
    }
    // ...
}
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh method

**Acceptance**: Vertex count = m_Vertices.m_NumItems // 3

---

### Fix 4.2: DecompressCompressedMesh - Normal Z-Reconstruction

**UnityPy Reference**: `MeshHelper.py` lines 527-546

**Critical Algorithm**:
```python
for srcNrm, sign, dstNrm in zip(normalData, signs, normals):
    x, y = srcNrm
    zsqr = 1 - x * x - y * y
    if zsqr >= 0:
        z = math.sqrt(zsqr)
        dstNrm[:] = x, y, z
    else:
        z = 0
        dstNrm[:] = normalize(x, y, z)
    if sign == 0:
        dstNrm[2] *= -1
```

**Required C# Implementation**:
```csharp
private void DecompressNormals(PackedBitVector normalsPacked, PackedBitVector signsPacked)
{
    var normalData = UnpackFloats(normalsPacked, shape: (2,));
    var signs = UnpackInts(signsPacked);
    
    var normals = new Vector3[_vertexCount];
    
    for (int i = 0; i < _vertexCount; i++)
    {
        float x = normalData[i * 2];
        float y = normalData[i * 2 + 1];
        float zsqr = 1f - x * x - y * y;
        float z;
        
        if (zsqr >= 0)
        {
            z = (float)Math.Sqrt(zsqr);
        }
        else
        {
            z = 0;
            // Normalize vector
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len > 0)
            {
                x /= len;
                y /= len;
                z /= len;
            }
        }
        
        if (signs[i] == 0)
            z *= -1;
        
        normals[i] = new Vector3(x, y, z);
    }
    
    _normals = ConvertVector3ToFloatArray(normals);
}
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh method

**Acceptance**: Normals reconstructed with Z-computation from X,Y pairs

---

### Fix 4.3: DecompressCompressedMesh - UV Channel Decompression

**UnityPy Reference**: `MeshHelper.py` lines 471-503

**Complex Logic**:
```python
m_UVInfo = m_CompressedMesh.m_UVInfo
if m_UVInfo is not None and m_UVInfo != 0:
    kInfoBitsPerUV = 4
    kUVDimensionMask = 3
    kUVChannelExists = 4
    
    uvSrcOffset = 0
    for uv_channel in range(8):
        texCoordBits = m_UVInfo >> (uv_channel * kInfoBitsPerUV)
        texCoordBits &= (1 << kInfoBitsPerUV) - 1
        if (texCoordBits & kUVChannelExists) != 0:
            uvDim = 1 + int(texCoordBits & kUVDimensionMask)
            m_UV = unpack_floats(m_CompressedMesh.m_UV, uvSrcOffset, m_VertexCount * uvDim, shape=(uvDim,))
            setattr(self, f"m_UV{uv_channel}", m_UV)
            uvSrcOffset = uvDim * m_VertexCount
else:
    # Default: UV0 and possibly UV1
    self.m_UV0 = unpack_floats(m_CompressedMesh.m_UV, 0, m_VertexCount * 2, shape=(2,))
    if m_CompressedMesh.m_UV.m_NumItems >= m_VertexCount * 4:
        self.m_UV1 = unpack_floats(m_CompressedMesh.m_UV, m_VertexCount * 2, m_VertexCount * 2, shape=(2,))
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh method

**Acceptance**: UV channels extracted with bit-packed info decoding

---

### Fix 4.4: DecompressCompressedMesh - Bone Weight Decompression

**UnityPy Reference**: `MeshHelper.py` lines 565-589

**Critical State Machine**:
```python
vertexIndex = 0
j = 0
sum = 0

boneIndicesIterator = iter(boneIndicesData)
for weight, boneIndex in zip(weightsData, boneIndicesIterator):
    boneWeights[vertexIndex][j] = weight / 31
    boneIndices[vertexIndex][j] = boneIndex
    
    j += 1
    sum += weight
    
    if sum >= 31:
        vertexIndex += 1
        j = 0
        sum = 0
    elif j == 3:
        boneWeights[vertexIndex][j] = 1 - sum
        boneIndices[vertexIndex][j] = next(boneIndicesIterator)
        vertexIndex += 1
        j = 0
        sum = 0
```

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh method

**Acceptance**: Bone weights decompressed with state machine logic matching UnityPy

---

### Fix 4.5: PackedBitVector - Float Unpacking Formula

**UnityPy Reference**: `PackedBitVector.py` lines 67-85

**Critical Formula**:
```python
if packed.m_BitSize == 0:
    quantized = [packed.m_Start] * count
else:
    quantized_f64 = unpack_ints(packed, start, count)
    scale = packed.m_Range / ((1 << packed.m_BitSize) - 1)
    quantized = [x * scale + packed.m_Start for x in quantized_f64]
```

**Required C# Implementation**:
```csharp
public float[] UnpackFloats(int start = 0, int? count = null)
{
    if (Range == null || Start == null)
        throw new InvalidOperationException("Range and Start must be set");
    
    var intData = UnpackInts(start, count);
    var actualCount = count ?? (int)NumItems;
    var result = new float[actualCount];
    
    if (BitSize == 0 || BitSize == null)
    {
        // Fill with Start value
        for (int i = 0; i < actualCount; i++)
            result[i] = Start.Value;
    }
    else
    {
        float scale = Range.Value / ((1 << BitSize.Value) - 1);
        for (int i = 0; i < actualCount; i++)
        {
            result[i] = intData[i] * scale + Start.Value;
        }
    }
    
    return result;
}
```

**File**: `src/UnityAssetParser/Helpers/PackedBitVector.cs`

**Acceptance**: Float unpacking uses exact formula from UnityPy

---

## PHASE 5: VALIDATION & TESTING

### Validation 5.1: Parse Known Bundles

**Test with fixtures**:
- Cigar_neck (220 verts)
- ClownNose_head (500 verts)
- BambooCopter (533 verts)
- Glasses (~600 verts)

**Steps**:
1. Parse with C# implementation
2. Parse with UnityPy
3. JSON serialize both
4. Diff - must be identical for critical fields

**Acceptance**: C# output matches UnityPy output exactly

---

## EXECUTION CHECKLIST

### Phase 1 Checklist
- [ ] 1.1 - Audit all byte array alignment
- [ ] 1.2 - Fix BlocksInfo location calculation
- [ ] 1.3 - Add BlockInfoNeedPaddingAtStart alignment
- [ ] 1.4 - Fix v22+ header parsing
- [ ] 1.5 - Verify endianness read position

### Phase 2 Checklist
- [ ] 2.1 - Fix type tree parsing order
- [ ] 2.2 - Fix object table parsing
- [ ] 2.3 - Fix node parsing order

### Phase 3 Checklist
- [ ] 3.1 - Fix version comparison logic
- [ ] 3.2 - Fix index buffer endianness
- [ ] 3.3 - Implement all version-dependent channel/stream paths
- [ ] 3.4 - Add all missing Mesh fields

### Phase 4 Checklist
- [ ] 4.1 - Fix vertex count calculation
- [ ] 4.2 - Implement normal Z-reconstruction
- [ ] 4.3 - Implement UV channel decompression
- [ ] 4.4 - Implement bone weight state machine
- [ ] 4.5 - Implement float unpacking formula

### Phase 5 Checklist
- [ ] 5.1 - Validate against known bundles
- [ ] Parse and compare Cigar
- [ ] Parse and compare ClownNose
- [ ] Parse and compare BambooCopter
- [ ] Parse and compare Glasses

---

## VERIFICATION SCRIPT (Python)

```python
import json
import subprocess

def compare_implementations(bundle_path):
    # Parse with C#
    csharp_result = subprocess.run(
        ['dotnet', 'run', '--project', 'Tools/DebugTypeTree', '--', bundle_path],
        capture_output=True, text=True
    )
    
    # Parse with UnityPy
    unitypy_result = subprocess.run(
        ['python3', 'scripts/dump_unitypy_object_tree.py', bundle_path],
        capture_output=True, text=True
    )
    
    # Parse JSON
    csharp_data = json.loads(csharp_result.stdout)
    unitypy_data = json.loads(unitypy_result.stdout)
    
    # Compare critical fields
    critical_fields = [
        'm_Vertices', 'm_Normals', 'm_Tangents', 'm_UV',
        'm_IndexBuffer', 'm_SubMeshes', 'm_VertexData'
    ]
    
    for field in critical_fields:
        if csharp_data.get(field) != unitypy_data.get(field):
            print(f"MISMATCH in {field}")
            print(f"  C#: {csharp_data.get(field)}")
            print(f"  UnityPy: {unitypy_data.get(field)}")
            return False
    
    return True
```

---

## EXPECTED TIMELINE

- **Phase 1**: 1-2 hours (infrastructure fixes)
- **Phase 2**: 2-3 hours (core parsing)
- **Phase 3**: 3-4 hours (mesh extraction)
- **Phase 4**: 4-6 hours (decompression - most complex)
- **Phase 5**: 1-2 hours (validation & testing)

**Total**: ~11-17 hours of focused implementation

---

## CRITICAL REMINDER

**DO NOT DEVIATE FROM UNITYPY LOGIC**
- Copy formulas verbatim
- Copy field order verbatim
- Copy alignment rules verbatim
- When in doubt, check UnityPy source code

**TESTS ARE WRONG - UNITYPY IS RIGHT**
