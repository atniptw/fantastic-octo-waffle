# UnityPy External Data Loading - Quick Reference Card

## M_StreamData Detection Pattern

```python
# Python
if isinstance(mesh, Mesh) and mesh.m_StreamData and mesh.m_StreamData.path:
    data = get_resource_data(
        mesh.m_StreamData.path,      # Filename: "cigar.resS"
        mesh.object_reader.assets_file,
        mesh.m_StreamData.offset,    # Byte offset in file
        mesh.m_StreamData.size       # Bytes to read
    )
    vertex_data.m_DataSize = data    # Replace or populate
```

```csharp
// C#
if (mesh.m_StreamData != null && !string.IsNullOrEmpty(mesh.m_StreamData.path))
{
    byte[] data = GetResourceData(
        mesh.m_StreamData.path,
        mesh.objectReader.assetsFile,
        (long)mesh.m_StreamData.offset,
        (long)mesh.m_StreamData.size
    );
    vertexData.m_DataSize = data;
}
```

---

## Filename Resolution

Try in order (UnityPy approach):
1. `cigar.resS` (original)
2. `cigar.resource`
3. `cigar.assets.resS`
4. `cigar.resS` (again, from cache)

Load file from `assetsFile.environment.GetCab(name)` or load dependencies if not found.

---

## Vertex Extraction Formula

**Per-vertex, per-component:**
```
offset = m_Stream.offset + m_Channel.offset + (v * m_Stream.stride) + (d * componentByteSize)
```

Where:
- `v` = vertex index (0 to vertexCount-1)
- `d` = component index (0 to dimension-1, e.g., 0-2 for XYZ)
- `m_Stream.offset` = base offset of this stream
- `m_Channel.offset` = field offset within stream
- `m_Stream.stride` = bytes per vertex in stream
- `componentByteSize` = 1, 2, or 4 bytes per component

**Example (POSITION with 3 components):**
```
v=0, d=0 (X): offset = 0 + 0 + (0*12) + (0*4) = 0
v=0, d=1 (Y): offset = 0 + 0 + (0*12) + (1*4) = 4
v=0, d=2 (Z): offset = 0 + 0 + (0*12) + (2*4) = 8
v=1, d=0 (X): offset = 0 + 0 + (1*12) + (0*4) = 12
```

---

## Channel Mapping (2018+)

| ID | Name | Dim | Type | Output Field |
|----|------|-----|------|--------------|
| 0 | POSITION | 3 | float | `m_Vertices` |
| 1 | NORMAL | 3 | float | `m_Normals` |
| 2 | TANGENT | 4 | float | `m_Tangents` |
| 3 | COLOR | 4 | float | `m_Colors` |
| 4 | TEXCOORD0 | 2 | float | `m_UV0` |
| 5 | TEXCOORD1 | 2 | float | `m_UV1` |
| 6 | TEXCOORD2 | 2 | float | `m_UV2` |
| 7 | TEXCOORD3 | 2 | float | `m_UV3` |
| 8-11 | TEXCOORD4-7 | 2 | float | `m_UV4-7` |
| 12 | BLENDWEIGHT | 4 | float | `m_BoneWeights` |
| 13 | BLENDINDICES | 4 | uint | `m_BoneIndices` |

---

## Data Type Mapping

| Format | Size | C# Type | Python |
|--------|------|---------|--------|
| `'f'` | 4 | `float` | `struct.unpack('f', ...)` |
| `'H'` | 2 | `ushort` | `struct.unpack('H', ...)` |
| `'I'` | 4 | `uint` | `struct.unpack('I', ...)` |
| `'B'` | 1 | `byte` | `struct.unpack('B', ...)` |
| `'e'` | 2 | `Half` (float16) | `struct.unpack('e', ...)` |

---

## Endianness Swap Logic

```python
# Python
swap = self.endianess == "<" and component_byte_size > 1
if swap:
    buff = buff[::-1]  # Reverse bytes in-place
```

```csharp
// C#
bool swap = this.endianess == "<" && componentByteSize > 1;
if (swap)
{
    Array.Reverse(componentBytes, componentOffset, componentByteSize);
}
```

---

## Bounds Check (CRITICAL)

```csharp
int maxVertexDataAccess = 
    (vertexCount - 1) * streamStride +
    channelOffset +
    streamOffset +
    componentByteSize * (channelDimension - 1) +
    componentByteSize;

if (maxVertexDataAccess > vertexDataBlob.Length)
    throw new InvalidOperationException("Out of bounds");
```

---

## Channel Mask Extraction

```python
# Python: Check if channel chn is in stream
channelMask = bin(m_Stream.channelMask)[::-1]
if channelMask[chn] == "1":
    # Process channel
```

```csharp
// C#: Equivalent
int channelMask = m_Stream.channelMask;
if (((channelMask >> chn) & 1) == 1)
{
    // Process channel
}
```

---

## Stream & Channel Access

```python
# Python
m_Channels: list[ChannelInfo]   # Array of 0-16 channels
m_Streams: list[StreamInfo]     # Array of 1-4 streams

for chn, m_Channel in enumerate(m_Channels):
    m_Stream = m_Streams[m_Channel.stream]  # Index into streams array
```

```csharp
// C#
List<ChannelInfo> m_Channels;
List<StreamInfo> m_Streams;

for (int chn = 0; chn < m_Channels.Count; chn++)
{
    ChannelInfo m_Channel = m_Channels[chn];
    StreamInfo m_Stream = m_Streams[m_Channel.stream];
}
```

---

## PackedBitVector Decompression

**Only for compressed meshes** (rare in REPO):

```python
# Scaling formula:
scale = packed.m_Range / ((1 << packed.m_BitSize) - 1)
float_value = int_value * scale + packed.m_Start
```

---

## Testing Output Format

**Python reference (UnityPy):**
```json
{
  "vertices": [[x, y, z], [x, y, z], ...],
  "normals": [[x, y, z], ...],
  "uv0": [[u, v], ...],
  "indices": [0, 1, 2, 3, 1, 2, ...]
}
```

**C# must match** (JSON diff validation):
```json
{
  "m_Vertices": [[x, y, z], ...],
  "m_Normals": [[x, y, z], ...],
  "m_UV0": [[u, v], ...],
  "m_IndexBuffer": [0, 1, 2, ...]
}
```

Use `JsonConvert.SerializeObject()` with appropriate settings to match Python output exactly (same float precision, same tuple formatting).

---

## Common Pitfalls

❌ **Don't**:
- Guess at file lookup order (use exact UnityPy pattern)
- Forget byte swap for little-endian systems
- Miss 4-byte alignment after arrays
- Forget bounds check (buffer overflow risk)
- Swap channel ID meanings between versions
- Assume channels are always contiguous (they're not)

✅ **Do**:
- Use `m_Stream.offset + m_Channel.offset` as base
- Validate file paths with all variations
- Load dependencies if not found in cache
- Test float precision against Python output
- Check channel bitmask before processing
- Validate bounds before every read

---

## Reference Files

- **UnityPy/helpers/MeshHelper.py** - Main extraction logic
- **UnityPy/helpers/ResourceReader.py** - File loading
- **UnityPy/helpers/PackedBitVector.py** - Compression (if needed)
- **UnityPyBoost/Mesh.cpp** - Performance-critical unpacking
- **UnityPy/classes/generated.py** - Data structure definitions

---

**Version**: 2026-02-01 | **Source**: K0lb3/UnityPy master branch
