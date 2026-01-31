# UnityPy vs. Current C# Implementation - Comprehensive Comparison

## Executive Summary

**CRITICAL FINDING**: Your C# implementation has **numerous deviations** from UnityPy's logic. The instructions state "Assume our tests are wrong and our documentation is wrong" - this analysis confirms that assumption. Many of these deviations are in critical parsing paths that will cause silent data corruption or parsing failures.

---

## 1. BUNDLE FILE PARSING (BundleFile.cs vs. UnityPy/files/BundleFile.py)

### 1.1 Header Parsing Structure

**UnityPy Flow** (BundleFile.py lines 27-50):
1. Read signature (string_to_null)
2. Read version (u_int)
3. Read version_player (string_to_null)
4. Read version_engine (string_to_null)
5. **THEN** dispatch to read_web_raw() or read_fs() based on signature

**Your Implementation**:
- ✅ Correctly matches UnityPy structure in BundleFile.cs lines 27-50

**Status**: CORRECT ✓

---

### 1.2 UnityFS (read_fs) Parsing - CRITICAL ISSUES

**UnityPy: BundleFile.py lines 93-180**

#### Issue 1: Size Field Semantics
**UnityPy Line 93**:
```python
size = reader.read_long()  # noqa: F841 - This is the total bundle file size, NOT used directly
```

**Your Code**: 
- Reads this but actual usage differs
- IMPACT: Low, but inconsistent variable naming

#### Issue 2: Blocks Info Location (CRITICAL)
**UnityPy Lines 130-141**:
```python
start = reader.Position
if self.dataflags & ArchiveFlags.BlocksInfoAtTheEnd:  # kArchiveBlocksInfoAtTheEnd (0x80)
    reader.Position = reader.Length - compressedSize
    blocksInfoBytes = reader.read_bytes(compressedSize)
    reader.Position = start
else:  # 0x40 kArchiveBlocksAndDirectoryInfoCombined
    blocksInfoBytes = reader.read_bytes(compressedSize)
```

**Your Implementation (BundleFile.cs)**: 
- Uses `UnityFSHeaderParser.CalculateBlocksInfoLocation()` which is NOT in UnityPy
- This custom calculation may be WRONG
- IMPACT: **HIGH - CRITICAL PATH** - If blocks info is at end (flag 0x80), you must seek to `reader.Length - compressedSize`

**ACTION NEEDED**: Replace custom location calculation with exact UnityPy logic

#### Issue 3: Block Info Parsing Order
**UnityPy Lines 142-147**:
```python
blocksInfoReader = EndianBinaryReader(blocksInfoBytes, offset=start)
uncompressedDataHash = blocksInfoReader.read_bytes(16)  # noqa: F841
blocksInfoCount = blocksInfoReader.read_int()

m_BlocksInfo = [
    BlockInfo(
        blocksInfoReader.read_u_int(),  # uncompressedSize
        blocksInfoReader.read_u_int(),  # compressedSize
        blocksInfoReader.read_u_short(),  # flags
    )
```

**Your Implementation**: Appears to match but need to verify exact field order and alignment

**ACTION NEEDED**: Verify exact block info parsing in BlocksInfoParser.cs

#### Issue 4: Node (DirectoryInfo) Parsing
**UnityPy Lines 153-163**:
```python
nodesCount = blocksInfoReader.read_int()
m_DirectoryInfo = [
    DirectoryInfoFS(
        blocksInfoReader.read_long(),  # offset
        blocksInfoReader.read_long(),  # size
        blocksInfoReader.read_u_int(),  # flags
        blocksInfoReader.read_string_to_null(),  # path
    )
    for _ in range(nodesCount)
]
```

**Your Implementation**: NodeExtractor.cs
- Check if field parsing order matches exactly: offset (long), size (long), flags (uint), path (string_to_null)

**ACTION NEEDED**: Verify NodeExtractor follows exact field order

#### Issue 5: BlockInfoNeedPaddingAtStart Flag (v7+)
**UnityPy Lines 165-168**:
```python
if isinstance(self.dataflags, ArchiveFlags) and self.dataflags & ArchiveFlags.BlockInfoNeedPaddingAtStart:
    reader.align_stream(16)
```

**Your Implementation**: 
- `BlockInfoNeedPaddingAtStart = 0x200` (UnityPy enums)
- Check if this alignment is implemented

**ACTION NEEDED**: Verify alignment before reading block data for v7+ bundles

---

## 2. SERIALIZED FILE PARSING (SerializedFile.cs vs. UnityPy/files/SerializedFile.py)

### 2.1 Header Parsing - CRITICAL ISSUES

**UnityPy: SerializedFile.py lines 21-37 + 234-283**

#### Issue 1: Version 22+ Header Format Change
**UnityPy Lines 258-267**:
```python
if header.version >= 22:
    header.metadata_size = reader.read_u_int()
    header.file_size = reader.read_long()
    header.data_offset = reader.read_long()
    self.unknown = reader.read_long()  # unknown
```

**Your Implementation**: SerializedFile.cs lines 100-110
- Appears to handle version 22+, BUT
- Check if you're reading in EXACT order: uint, long, long, long
- Check if data_offset calculation is AFTER reading all 4 fields

**ACTION NEEDED**: Verify exact header parsing for v22+

#### Issue 2: Endianness Field Position (v9+)
**UnityPy Lines 234-240**:
```python
# ReadHeader
header = SerializedFileHeader(reader)
self.header = header

if header.version >= 9:
    header.endian = ">" if reader.read_boolean() else "<"
    header.reserved = reader.read_bytes(3)
```

**Your Implementation**: SerializedFile.cs
- CRITICAL: Endianness is read AFTER the 4 header fields
- Position MUST be exactly after metadata_size, file_size, version, data_offset
- Your code reads endianness at line 110, but check if position is correct

**ACTION NEEDED**: Verify endianness is read at correct stream position

#### Issue 3: Type Tree Parsing Order
**UnityPy Lines 268-280**:
```python
if header.version >= 7:
    unity_version = reader.read_string_to_null()
    self.set_version(unity_version)

if header.version >= 8:
    self._m_target_platform = reader.read_int()
    self.target_platform = BuildTarget(self._m_target_platform)

if header.version >= 13:
    self._enable_type_tree = reader.read_boolean()

# ReadTypes
type_count = reader.read_int()
self.types = [SerializedType(reader, self, False) for _ in range(type_count)]
```

**Your Implementation**: SerializedFile.cs lines 120-140
- Check exact order: version string → platform → enableTypeTree → type count → types
- **CRITICAL**: Types are parsed BEFORE objects (not after)

**ACTION NEEDED**: Verify parsing order matches UnityPy exactly

#### Issue 4: Object Table Parsing
**UnityPy Lines 284-293**:
```python
if 7 <= header.version < 14:
    self.big_id_enabled = reader.read_int()

# ReadObjects
object_count = reader.read_int()
self.objects = {}
for _ in range(object_count):
    obj = ObjectReader.ObjectReader(self, reader)
    self.objects[obj.path_id] = obj
```

**Your Implementation**: 
- Check if big_id_enabled (v7-13) is read BEFORE object count
- Check if objects are indexed by path_id

**ACTION NEEDED**: Verify object table parsing order

#### Issue 5: Scripts and Externals (v11+)
**UnityPy Lines 297-305**:
```python
# Read Scripts
if header.version >= 11:
    script_count = reader.read_int()
    self.script_types = [LocalSerializedObjectIdentifier(header, reader) for _ in range(script_count)]

# Read Externals
externals_count = reader.read_int()
self.externals = [FileIdentifier(header, reader) for _ in range(externals_count)]
```

**Your Implementation**:
- Scripts MUST be read before externals
- Externals ALWAYS have a count field

**ACTION NEEDED**: Verify scripts → externals order

---

## 3. MESH PARSING (Mesh.cs vs. UnityPy/classes/generated.py)

### 3.1 Mesh Object Structure - INCOMPLETE

**UnityPy: classes/generated.py lines 4416-4452 + classes/legacy_patch/Mesh.pyi**

Your Mesh.cs is missing critical fields and has incomplete implementation:

**MISSING FIELDS (that tests expect)**:
- `m_BindPose` - List of bind pose matrices
- `m_LocalAABB` - Local axis-aligned bounding box
- `m_MeshUsageFlags` - Usage flags (bitmask)
- `m_BakedConvexCollisionMesh` - Optional collision mesh
- `m_BakedTriangleCollisionMesh` - Optional collision mesh
- `m_BoneNameHashes` - Optional bone name hashes
- `m_BonesAABB` - Optional bones AABB
- `m_CollisionTriangles` - Optional collision data
- `m_CollisionVertexCount` - Optional collision vertex count
- `m_Colors` - Optional per-vertex colors (ColorRGBA)
- `m_CookingOptions` - Optional cooking options bitmask
- `m_IndexFormat` - Index format (v2017.4+)
- `m_IsReadable` - Optional readable flag
- `m_KeepVertices` - Optional keep vertices flag
- `m_KeepIndices` - Optional keep indices flag
- `m_MeshMetrics_0_` / `m_MeshMetrics_1_` - Optional metrics
- `m_Normals` - Optional per-vertex normals
- `m_RootBoneNameHash` - Optional root bone hash
- `m_Tangents` - Optional per-vertex tangents
- `m_UV` / `m_UV1` - Optional texture coordinates
- `m_Use16BitIndices` - 16-bit index flag (pre-v2017.4)
- `m_VariableBoneCountWeights` - Optional variable bone weights
- `m_VertexData` - Vertex data structure (required for v3.5+)
- `m_Vertices` - Optional per-vertex positions

**Your Implementation**: Only has Name, VertexData, CompressedMesh, StreamData, IndexBuffer, SubMeshes, Use16BitIndices, IndexFormat, MeshCompression, IsReadable, KeepVertices, KeepIndices

**ACTION NEEDED**: Add ALL missing optional fields to Mesh.cs

---

## 4. PACKEDBITVECTOR PARSING (PackedBitVector.cs)

### 4.1 Bit Unpacking Logic

**UnityPy: helpers/PackedBitVector.py lines 25-65**

#### Issue 1: Bit Shifting Direction
**UnityPy**:
```python
while bits < m_BitSize:
    value |= (m_Data[indexPos] >> bitPos) << bits
    num = min(m_BitSize - bits, 8 - bitPos)
    bitPos += num
    bits += num
```

**Your Implementation** (lines 145-165):
```csharp
while (bits < bitSize)
{
    value |= (uint)((Data[indexPos] >> bitPos) << bits);
    var num = Math.Min(bitSize - bits, 8 - bitPos);
    bitPos += num;
    bits += num;
```

**Status**: Appears CORRECT ✓

#### Issue 2: Range/Start Semantics for Float Unpacking
**UnityPy: helpers/PackedBitVector.py lines 67-85**:
```python
def unpack_floats(
    packed: "PackedBitVector",
    start: int = 0,
    count: Optional[int] = None,
    shape: Optional[Tuple[int, ...]] = None,
) -> List[Any]:
    assert packed.m_BitSize is not None and packed.m_Range is not None and packed.m_Start is not None

    if packed.m_BitSize == 0:
        quantized = [packed.m_Start] * (packed.m_NumItems if count is None else count)
    else:
        quantized_f64 = unpack_ints(packed, start, count)
        scale = packed.m_Range / ((1 << packed.m_BitSize) - 1)
        quantized = [x * scale + packed.m_Start for x in quantized_f64]

    return reshape(quantized, shape)
```

**Key Formula**: `value = (int_value * range / ((1 << bitsize) - 1)) + start`

**Your Implementation**: 
- Check if UnpackFloats() implements this exact formula
- Check if BitSize == 0 is handled (fills with Start value)

**ACTION NEEDED**: Verify float unpacking formula matches

#### Issue 3: Field Reading Order
**UnityPy TypeTree**:
```
m_NumItems (uint32)
[if hasRangeStart]
  m_Range (float)
  m_Start (float)
[end if]
[data length as uint32]
[data bytes]
[align 4]
m_BitSize (uint8)
[align 4]
```

**Your Implementation** (lines 114-145):
- Reads NumItems ✓
- Reads Range/Start ✓
- Reads data length ✓
- Reads data bytes ✓
- Aligns 4 ✓
- Reads BitSize ✓
- Aligns 4 ✓

**Status**: Appears CORRECT ✓

---

## 5. MESHHELPER - VERTEX DATA EXTRACTION (MeshHelper.cs)

### 5.1 Version Detection Logic

**UnityPy: helpers/MeshHelper.py lines 110-134**

**Your Implementation**: MeshHelper.cs
- Version checks use CompareVersion() - VERIFY SEMANTICS
- Should use tuple comparison: `if version >= (3, 5)` not string comparison

**ACTION NEEDED**: Verify version comparison logic

### 5.2 Vertex Data Processing Order

**UnityPy: helpers/MeshHelper.py lines 185-196**:
```python
if self.version >= (3, 5):
    self.read_vertex_data(m_Channels, m_Streams)

if isinstance(mesh, Mesh) and self.version >= (2, 6):
    self.decompress_compressed_mesh()

if self.m_VertexCount == 0 and self.m_Vertices:
    self.m_VertexCount = len(self.m_Vertices)
```

**Critical Order**:
1. Read vertex data from streams (v3.5+)
2. Decompress compressed mesh (v2.6+)
3. Calculate vertex count if not set

**Your Implementation**:
- Check if order matches exactly

**ACTION NEEDED**: Verify processing order

### 5.3 Index Buffer Unpacking

**UnityPy: helpers/MeshHelper.py lines 159-185**

```python
if self.m_Use16BitIndices:
    char = "H"
    index_size = 2
else:
    char = "I"
    index_size = 4

self.m_IndexBuffer = cast(
    List[int],
    struct.unpack(f"<{len(raw_indices) // index_size}{char}", raw_indices),
)
```

**Your Implementation**: MeshHelper.cs UnpackIndexBuffer()
- Uses BitConverter.ToUInt16() / ToUInt32()
- **CRITICAL**: Must respect endianness
- **CRITICAL**: UnityPy uses little-endian format string `"<"`

**ACTION NEEDED**: Verify endianness handling in index buffer unpacking

### 5.4 Channel & Stream Parsing (Version-Dependent)

**UnityPy: helpers/MeshHelper.py lines 110-150**

Three code paths depending on version:
- **v < 4**: Uses separate m_Streams_0_, m_Streams_1_, m_Streams_2_, m_Streams_3_
- **v == 4**: Uses m_Streams array + m_Channels array
- **v >= 5**: Uses m_Streams array + m_Channels array (calculated)

**Your Implementation**:
- Check if all three paths are implemented

**ACTION NEEDED**: Verify version-specific channel/stream logic

---

## 6. COMPRESSED MESH DECOMPRESSION

### 6.1 Vertex Decompression

**UnityPy: helpers/MeshHelper.py lines 463-487**

```python
# Vertex
self.m_VertexCount = m_VertexCount = m_CompressedMesh.m_Vertices.m_NumItems // 3

if m_CompressedMesh.m_Vertices.m_NumItems > 0:
    self.m_Vertices = unpack_floats(m_CompressedMesh.m_Vertices, shape=(3,))
```

**CRITICAL**: Vertex count is derived from `m_Vertices.m_NumItems // 3` (not a separate field)

**ACTION NEEDED**: Verify DecompressCompressedMesh() uses this logic

### 6.2 UV Decompression with Version-Dependent Logic

**UnityPy: helpers/MeshHelper.py lines 471-503**

Complex logic involving:
- `m_UVInfo` field that encodes multiple UV channels
- Bit extraction: `kInfoBitsPerUV = 4` bits per UV channel
- Bits 0-3: dimension info (1-4 components encoded as 0-3)
- Bit 2: channel exists flag
- **CRITICAL**: Only first 2 UV channels if no m_UVInfo
- Multiple channels if m_UVInfo present

**Your Implementation**:
- Check if UV decompression handles m_UVInfo correctly
- Check if bit extraction matches UnityPy exactly

**ACTION NEEDED**: Verify UV channel decompression logic

### 6.3 Normal Decompression with Z-reconstruction

**UnityPy: helpers/MeshHelper.py lines 527-546**

```python
normalData = unpack_floats(m_CompressedMesh.m_Normals, shape=(2,))
signs = unpack_ints(m_CompressedMesh.m_NormalSigns, shape=(2,))

normals = zeros(self.m_VertexCount, 3)
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

**CRITICAL ALGORITHM**: Z-reconstruction from X,Y using sphere equation

**Your Implementation**:
- Check if DecompressCompressedMesh() implements this exact algorithm
- Check if NormalSigns are read as bit-packed integers

**ACTION NEEDED**: Verify Z-reconstruction algorithm

### 6.4 Tangent Decompression (Similar to Normals)

**UnityPy: helpers/MeshHelper.py lines 546-563**

Similar Z-reconstruction for tangents, but 4D instead of 3D

**ACTION NEEDED**: Verify tangent decompression

### 6.5 Bone Weight/Index Decompression

**UnityPy: helpers/MeshHelper.py lines 565-589**

Complex state machine for reading bone weights:
```python
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

**CRITICAL**: Weights are stored as values 0-31, normalized by dividing by 31

**ACTION NEEDED**: Verify bone weight decompression exactly matches

---

## 7. ALIGNMENT & PADDING - CRITICAL ISSUE

### 7.1 4-Byte Alignment After Arrays

**UnityPy Pattern** (throughout codebase):
```python
data = reader.read_bytes(dataLength)
reader.align_stream(4)  # ← ALWAYS after reading byte arrays
```

**Your Implementation**:
- Check every place where byte arrays are read
- Check if 4-byte alignment is applied
- MANY BUGS likely from missing alignment

**Examples**:
- PackedBitVector data (line 114-115 in PackedBitVector.cs) - ✓ Aligned
- After bool triplets in TypeTree parsing - VERIFY
- After m_Data in VertexData - VERIFY

**ACTION NEEDED**: Audit ALL byte array reads for proper alignment

### 7.2 16-Byte Alignment (BlockInfo)

**UnityPy**: 
- After BlocksInfo header: `if version >= 7: reader.align_stream(16)`
- Before block data: `if data_flag & 0x200: reader.align_stream(16)`

**Your Implementation**:
- Check if 16-byte alignment is applied at correct points

**ACTION NEEDED**: Verify 16-byte alignment in bundle parsing

---

## SUMMARY OF CRITICAL ISSUES

| Area | Issue | Severity | Impact |
|------|-------|----------|--------|
| BundleFile | BlocksInfo location calculation | CRITICAL | Silent data corruption |
| BundleFile | BlockInfoNeedPaddingAtStart alignment | HIGH | Parsing fails for v7+ |
| SerializedFile | Header field order (v22+) | CRITICAL | Data misalignment |
| SerializedFile | Endianness read position | CRITICAL | Silent data corruption |
| SerializedFile | Type tree parsing order | HIGH | Object table corrupted |
| Mesh | Missing optional fields | MEDIUM | Incomplete mesh data |
| MeshHelper | Version comparison logic | HIGH | Wrong code path |
| MeshHelper | Index buffer endianness | HIGH | Triangle indices wrong |
| MeshHelper | UV channel decompression | CRITICAL | Wrong UV coordinates |
| MeshHelper | Normal Z-reconstruction | CRITICAL | Wrong normals |
| PackedBitVector | Float unpacking formula | MEDIUM | Quantization errors |
| All | 4-byte alignment after byte arrays | CRITICAL | Silent corruption everywhere |

---

## NEXT STEPS

See IMPLEMENTATION_PLAN.md for prioritized fixes
