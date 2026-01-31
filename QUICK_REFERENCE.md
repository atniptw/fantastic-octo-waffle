# QUICK REFERENCE: TOP ISSUES AT A GLANCE

This is a **TL;DR** of the most critical problems. For full details, see:
- [COMPARISON_ANALYSIS.md](COMPARISON_ANALYSIS.md) - What's wrong
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) - How to fix it

---

## Issue 1: BundleFile BlocksInfo Location is WRONG

### The Problem
Your code uses a custom `CalculateBlocksInfoLocation()` function that UnityPy doesn't have. This is a red flag—it means you invented something instead of copying UnityPy.

**File**: `src/UnityAssetParser/Bundle/BundleFile.cs` (around line 85-100)

### What UnityPy Does
```python
# BundleFile.py lines 130-141
start = reader.Position
if self.dataflags & ArchiveFlags.BlocksInfoAtTheEnd:  # 0x80
    reader.Position = reader.Length - compressedSize
    blocksInfoBytes = reader.read_bytes(compressedSize)
    reader.Position = start
else:  # 0x40
    blocksInfoBytes = reader.read_bytes(compressedSize)
```

### What You Should Do
Replace the `CalculateBlocksInfoLocation()` call with:
```csharp
long startPos = stream.Position;
byte[] compressedBlocksInfo;

if ((header.DataFlags & 0x80) != 0)  // BlocksInfoAtTheEnd
{
    // Seek to: total_length - compressed_size
    stream.Position = stream.Length - header.CompressedBlocksInfoSize;
    compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
    int read = stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
    if (read != compressedBlocksInfo.Length)
        throw new Exception("Failed to read BlocksInfo");
    // Seek back to where we started
    stream.Position = startPos;
}
else  // BlocksAndDirectoryInfoCombined (0x40)
{
    // Read from current position
    compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
    int read = stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
    if (read != compressedBlocksInfo.Length)
        throw new Exception("Failed to read BlocksInfo");
}
```

### Why This Matters
**CRITICAL**: If BlocksInfo is at the end (flag 0x80), you MUST seek to `file.Length - compressedSize`. Getting this wrong corrupts the entire bundle structure. This is one of THE most important fixes.

---

## Issue 2: Missing 4-Byte Alignment Everywhere

### The Problem
After reading byte arrays, you must **always** align to 4 bytes. Missing this causes cascading data corruption.

**UnityPy does this everywhere**:
```python
data = reader.read_bytes(length)
reader.align_stream(4)  # ← ALWAYS
```

### Where to Check
Search your codebase for:
```csharp
reader.ReadBytes(  // Find all of these
```

For each one, verify the NEXT line is:
```csharp
reader.Align(4);
```

### Examples to Check

1. **PackedBitVector.cs** (line 114-145)
   ```csharp
   var dataLength = reader.ReadInt32();
   Data = dataLength > 0 ? reader.ReadBytes(dataLength) : Array.Empty<byte>();
   reader.Align(4);  // ← Is this here?
   
   BitSize = reader.ReadByte();
   reader.Align(4);  // ← Is this here?
   ```
   Status: ✓ Appears correct

2. **SerializedFile.cs** - TypeTree parsing
   - Check after string buffer reads
   - Status: ? AUDIT NEEDED

3. **Mesh.cs** - VertexData parsing
   - Check after m_DataSize byte read
   - Status: ? AUDIT NEEDED

### Why This Matters
**CRITICAL**: Without alignment, every field after a byte array is read at the wrong offset. This creates a cascading corruption that's hard to debug because downstream code looks "correct" but reads garbage data.

---

## Issue 3: SerializedFile v22+ Header is Wrong

### The Problem
Unity 2022.x bundles use a different header format (v22+). Your code may not parse it correctly.

**File**: `src/UnityAssetParser/SerializedFile/SerializedFileHeader.cs` or `SerializedFile.cs`

### What UnityPy Does (lines 258-267)
```python
if header.version >= 22:
    header.metadata_size = reader.read_u_int()      # offset 0
    header.file_size = reader.read_long()           # offset 4
    header.data_offset = reader.read_long()         # offset 12
    self.unknown = reader.read_long()               # offset 20
```

**Exact byte sequence**:
- Offset 0-3: metadata_size (uint32)
- Offset 4-11: file_size (int64)
- Offset 12-19: data_offset (int64)
- Offset 20-27: unknown (int64)

### What to Check
Your v22+ header parsing:
1. Read 4-byte uint (metadata_size) ✓ or ✗?
2. Read 8-byte long (file_size) ✓ or ✗?
3. Read 8-byte long (data_offset) ✓ or ✗?
4. Read 8-byte long (unknown) ✓ or ✗?
5. In exact order? ✓ or ✗?

### Why This Matters
**CRITICAL**: If the header is wrong, all subsequent offsets are wrong. The entire bundle is unreadable.

---

## Issue 4: Index Buffer Endianness Not Respected

### The Problem
Your index buffer unpacking uses `BitConverter.ToUInt16()` which respects **system endianness** (usually little-endian). UnityPy explicitly uses **little-endian** format string `"<"`.

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` UnpackIndexBuffer()

### What UnityPy Does (lines 159-175)
```python
if self.m_Use16BitIndices:
    char = "H"      # UInt16
else:
    char = "I"      # UInt32

# EXPLICIT little-endian format string
indices = struct.unpack(f"<{count}{char}", raw_indices)
```

### What You're Probably Doing
```csharp
if (use16Bit)
{
    var count = rawIndices.Length / 2;
    var indices = new uint[count];
    for (var i = 0; i < count; i++)
    {
        indices[i] = BitConverter.ToUInt16(rawIndices, i * 2);  // ← System endian!
    }
    return indices;
}
```

### The Fix
```csharp
private static uint[] UnpackIndexBuffer(byte[] rawIndices, bool use16Bit, bool isLittleEndian)
{
    if (use16Bit)
    {
        var count = rawIndices.Length / 2;
        var indices = new uint[count];
        for (int i = 0; i < count; i++)
        {
            if (isLittleEndian)
                indices[i] = BitConverter.ToUInt16(rawIndices, i * 2);
            else
                indices[i] = (uint)((rawIndices[i * 2 + 1] << 8) | rawIndices[i * 2]);
        }
        return indices;
    }
    // Similar for 32-bit...
}
```

### Why This Matters
**HIGH**: Index buffer defines triangle connectivity. Wrong endianness = wrong triangles = broken mesh geometry.

---

## Issue 5: Normal Z-Reconstruction Not Implemented

### The Problem
Compressed normals are stored as 2D (X, Y), and Z is computed from the sphere equation: X² + Y² + Z² = 1

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh()

### What UnityPy Does (lines 527-546)
```python
normalData = unpack_floats(m_CompressedMesh.m_Normals, shape=(2,))
signs = unpack_ints(m_CompressedMesh.m_NormalSigns, shape=(2,))

for srcNrm, sign, dstNrm in zip(normalData, signs, normals):
    x, y = srcNrm
    zsqr = 1 - x * x - y * y
    if zsqr >= 0:
        z = math.sqrt(zsqr)
    else:
        z = 0
        # Normalize
        len = sqrt(x^2 + y^2 + z^2)
        x, y, z = x/len, y/len, z/len
    if sign == 0:
        z *= -1  # Flip Z if sign bit is 0
    dstNrm[:] = x, y, z
```

### What to Do
Check your DecompressCompressedMesh() method:

1. Does it unpack normals as 2D? ✓ or ✗?
2. Does it read m_NormalSigns? ✓ or ✗?
3. Does it compute Z from X² + Y² + Z² = 1? ✓ or ✗?
4. Does it handle the case where zsqr < 0 (normalize vector)? ✓ or ✗?
5. Does it flip Z if sign == 0? ✓ or ✗?

If any are ✗, it's broken.

### Why This Matters
**CRITICAL**: Normals control lighting. Wrong normals = completely wrong appearance (flat or inverted surfaces).

---

## Issue 6: Bone Weight Decompression State Machine Missing

### The Problem
Bone weights are stored in a compressed format with a state machine that determines when to move to the next vertex.

**File**: `src/UnityAssetParser/Helpers/MeshHelper.cs` DecompressCompressedMesh()

### What UnityPy Does (lines 565-589)
```python
# Complex state machine
vertexIndex = 0
j = 0  # Component index (0-3)
sum = 0  # Weight accumulator

for weight, boneIndex in zip(weightsData, boneIndicesData):
    boneWeights[vertexIndex][j] = weight / 31  # Normalize
    boneIndices[vertexIndex][j] = boneIndex
    
    j += 1
    sum += weight
    
    # Advance to next vertex if weights sum to 31
    if sum >= 31:
        vertexIndex += 1
        j = 0
        sum = 0
    # OR if we've filled 4 components, compute 4th as (1 - sum)
    elif j == 3:
        boneWeights[vertexIndex][j] = 1 - sum  # 4th weight
        boneIndices[vertexIndex][j] = next(boneIndicesData)
        vertexIndex += 1
        j = 0
        sum = 0
```

### Critical Details
1. Weights are **integers 0-31**, not floats → divide by 31
2. At most 4 weights per vertex
3. 4th weight = 1 - sum of first 3
4. Move to next vertex when sum >= 31

### Why This Matters
**HIGH**: Bone weights control animation deformation. Wrong weights = broken skeletal animation.

---

## Quick Fix Priority

### Do These First (Tomorrow)
1. ✅ Issue 1: BlocksInfo location - 15 min
2. ✅ Issue 2: Alignment audit - 30 min
3. ✅ Issue 3: v22+ header - 20 min

### Then (This Week)
4. ✅ Issue 4: Index buffer endianness - 15 min
5. ✅ Issue 5: Normal Z-reconstruction - 45 min
6. ✅ Issue 6: Bone weight state machine - 45 min

### Then (Validation)
- Parse test bundles
- Compare with UnityPy output
- Iterate until match

---

## How to Verify Each Fix

After implementing each fix:

1. **Compile**: `dotnet build`
2. **Parse test bundle**: Run your parser on a known bundle
3. **Compare**: 
   ```bash
   # C# output
   dotnet run --project Tools/DebugTypeTree -- /path/to/bundle.hhh > csharp_output.json
   
   # Python output
   python3 scripts/dump_unitypy_object_tree.py /path/to/bundle.hhh > unitypy_output.json
   
   # Diff
   diff csharp_output.json unitypy_output.json
   ```
4. **Iterate**: If differences, fix the issue

---

## Key Takeaway

**You're re-implementing binary parsing logic.** Binary parsing is extremely unforgiving:

- ❌ **One byte off**: Complete failure
- ❌ **Wrong alignment**: Silent corruption
- ❌ **Wrong field order**: Garbage data
- ❌ **Wrong algorithm**: Wrong output

**Only ONE thing works**: Exact verbatim porting from a source of truth. UnityPy is that source.

**Stop inventing.** Start copying.

Good luck! 🚀
