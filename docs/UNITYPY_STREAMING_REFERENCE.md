# UnityPy VertexData & IndexBuffer Streaming Reference

**Source**: https://github.com/K0lb3/UnityPy (direct port from Python)

For C# porting: copy logic verbatim, do NOT reverse-engineer or guess.

---

## 1. Detecting External Data (m_StreamData Field)

### Python Reference (MeshHelper.py, lines 134-159)

```python
# Check if Mesh has external streaming data
if (
    isinstance(mesh, Mesh) and mesh.m_StreamData and mesh.m_StreamData.path
):
    stream_data = mesh.m_StreamData
    assert mesh.object_reader, "No object reader assigned to the input Mesh!"
    data = get_resource_data(
        stream_data.path,
        mesh.object_reader.assets_file,
        stream_data.offset,
        stream_data.size,
    )
    vertex_data.m_DataSize = data  # Replace inline data with external data
```

### Key Points:
- **Detection**: Check `if mesh.m_StreamData and mesh.m_StreamData.path`
- **Fields**: Access `path`, `offset`, `size` from `StreamingInfo` struct
- **Result**: Populate `m_VertexData.m_DataSize` with external data blob

### C# Implementation Strategy:
```csharp
// Pseudocode structure
if (mesh.m_StreamData != null && !string.IsNullOrEmpty(mesh.m_StreamData.path))
{
    var streamData = mesh.m_StreamData;
    var externalData = GetResourceData(
        streamData.path,
        mesh.objectReader.assetsFile,
        (long)streamData.offset,
        (long)streamData.size
    );
    vertexData.m_DataSize = externalData;
}
```

---

## 2. Reading StreamingInfo Structure (Path, Offset, Size)

### Python Definition (generated.py, Mesh class)

From UnityPy classes:
```python
@unitypy_define
class Mesh(NamedObject):
    m_StreamData: Optional[StreamingInfo] = None
    m_VertexData: Optional[VertexData] = None
```

### StreamingInfo Structure Fields:

The `StreamingInfo` contains:
- **path**: `str` - Filename/path to external resource (e.g., "cigar.resS", "froghat.resS")
- **offset**: `uint` or `ulong` - Byte offset into the external file
- **size**: `uint` or `ulong` - Size in bytes of the data to read

### Example from Texture2D legacy patch (shows exact usage):

```python
def _Texture2D_get_image_data(self: Texture2D):
    if self.image_data:
        return self.image_data
    if self.m_StreamData:
        from ...helpers.ResourceReader import get_resource_data

        return get_resource_data(
            self.m_StreamData.path,           # Path field
            self.object_reader.assets_file,
            self.m_StreamData.offset,         # Offset field
            self.m_StreamData.size,           # Size field
        )
    raise ValueError("No image data found")
```

---

## 3. Loading Data from External .resS File

### ResourceReader Implementation (ResourceReader.py)

```python
def get_resource_data(res_path: str, assets_file: "SerializedFile", offset: int, size: int):
    """Load data from external .resS resource file"""
    basename = ntpath.basename(res_path)
    name, ext = ntpath.splitext(basename)
    
    # Try multiple possible filename variations
    possible_names = [
        basename,                           # Original name
        f"{name}.resource",
        f"{name}.assets.resS",
        f"{name}.resS",                     # Standard variation
    ]
    
    environment = assets_file.environment
    reader = None
    
    # First pass: try to find in already-loaded files
    for possible_name in possible_names:
        reader = environment.get_cab(possible_name)
        if reader:
            break
    
    # Second pass: load dependencies if not found
    if not reader:
        assets_file.load_dependencies(possible_names)
        for possible_name in possible_names:
            reader = environment.get_cab(possible_name)
            if reader:
                break
        if not reader:
            raise FileNotFoundError(f"Resource file {basename} not found")
    
    return _get_resource_data(reader, offset, size)


def _get_resource_data(reader: EndianBinaryReader, offset: int, size: int):
    """Extract bytes from stream at offset"""
    reader.Position = offset
    return reader.read_bytes(size)
```

### C# Implementation Strategy:
```csharp
public static byte[] GetResourceData(string resPath, SerializedFile assetsFile, long offset, long size)
{
    string basename = Path.GetFileName(resPath);
    string name = Path.GetFileNameWithoutExtension(basename);
    
    string[] possibleNames = new[]
    {
        basename,
        $"{name}.resource",
        $"{name}.assets.resS",
        $"{name}.resS",
    };
    
    var environment = assetsFile.Environment;
    EndianBinaryReader? reader = null;
    
    // First pass: search cache
    foreach (var possibleName in possibleNames)
    {
        reader = environment.GetCab(possibleName);
        if (reader != null) break;
    }
    
    // Second pass: load dependencies
    if (reader == null)
    {
        assetsFile.LoadDependencies(possibleNames);
        foreach (var possibleName in possibleNames)
        {
            reader = environment.GetCab(possibleName);
            if (reader != null) break;
        }
        if (reader == null)
            throw new FileNotFoundException($"Resource file {basename} not found");
    }
    
    reader.Position = offset;
    return reader.ReadBytes((int)size);
}
```

---

## 4. Extracting Individual Vertex Channels

### Data Flow in UnityPy (MeshHelper.py, lines 110-159)

**Step 1: Get streams and channels**
```python
# For Unity 2017.3+
m_Streams = vertex_data.m_Streams         # List of StreamInfo
m_Channels = vertex_data.m_Channels       # List of ChannelInfo
```

**Step 2: Read vertex data from m_VertexData.m_DataSize**
```python
def read_vertex_data(self, m_Channels: list[ChannelInfo], m_Streams: list[StreamInfo]) -> None:
    m_VertexData = self.src.m_VertexData
    if m_VertexData is None:
        return

    self.m_VertexCount = m_VertexCount = m_VertexData.m_VertexCount
    
    # Loop through each channel (POSITION, NORMAL, TEXCOORD0, etc.)
    for chn, m_Channel in enumerate(m_Channels):
        if m_Channel.dimension == 0:
            continue
        
        m_Stream = m_Streams[m_Channel.stream]
        channelMask = bin(m_Stream.channelMask)[::-1]
        
        # Check if this channel is active in this stream
        if channelMask[chn] == "1":
            # Extract component data for this channel
            # ... (see Section 5 for component extraction)
```

### Channel Assignment (MeshHelper.py, lines 390-425)

```python
def assign_channel_vertex_data(self, channel: int, component_data: list):
    if self.version[0] >= 2018:
        if channel == 0:  # kShaderChannelVertex / POSITION
            self.m_Vertices = component_data
        elif channel == 1:  # kShaderChannelNormal / NORMAL
            self.m_Normals = component_data
        elif channel == 2:  # kShaderChannelTangent
            self.m_Tangents = component_data
        elif channel == 3:  # kShaderChannelColor
            self.m_Colors = component_data
        elif channel == 4:  # kShaderChannelTexCoord0 / TEXCOORD0
            self.m_UV0 = component_data
        elif channel == 5:  # kShaderChannelTexCoord1 / TEXCOORD1
            self.m_UV1 = component_data
        elif channel == 6:  # kShaderChannelTexCoord2 / TEXCOORD2
            self.m_UV2 = component_data
        elif channel == 7:  # kShaderChannelTexCoord3 / TEXCOORD3
            self.m_UV3 = component_data
        # ... more channels up to 13 for bone weights/indices
```

### Channel Mapping Table (2018+):
| Channel | Name | Dimension | Type |
|---------|------|-----------|------|
| 0 | POSITION | 3 | float |
| 1 | NORMAL | 3 | float |
| 2 | TANGENT | 4 | float |
| 3 | COLOR | 4 | float or RGBA8 |
| 4 | TEXCOORD0 | 2 | float |
| 5 | TEXCOORD1 | 2 | float |
| 6 | TEXCOORD2 | 2 | float |
| 7 | TEXCOORD3 | 2 | float |
| 8-11 | TEXCOORD4-7 | 2 | float |
| 12 | BLENDWEIGHT | 4 | float |
| 13 | BLENDINDICES | 4 | uint |

---

## 5. Reading Vertex Attributes from Streams

### Core Extraction Loop (MeshHelper.py, lines 324-388)

```python
def read_vertex_data(self, m_Channels: list[ChannelInfo], m_Streams: list[StreamInfo]) -> None:
    m_VertexData = self.src.m_VertexData
    if m_VertexData is None:
        return

    self.m_VertexCount = m_VertexCount = m_VertexData.m_VertexCount
    
    # Iterate through each channel
    for chn, m_Channel in enumerate(m_Channels):
        if m_Channel.dimension == 0:
            continue
        
        m_Stream = m_Streams[m_Channel.stream]
        channelMask = bin(m_Stream.channelMask)[::-1]
        
        if channelMask[chn] == "1":
            # Get format info
            component_dtype = self.get_channel_dtype(m_Channel)  # e.g., "f", "H", "B"
            component_byte_size = self.get_channel_component_size(m_Channel)
            swap = self.endianess == "<" and component_byte_size > 1
            channel_dimension = m_Channel.dimension & 0xF
            
            # Extract raw component bytes
            componentBytes = bytearray(
                m_VertexCount * channel_dimension * component_byte_size
            )
            
            # For each vertex
            vertexBaseOffset = m_Stream.offset + m_Channel.offset
            for v in range(m_VertexCount):
                vertexOffset = vertexBaseOffset + m_Stream.stride * v
                
                # For each component of the attribute (e.g., X, Y, Z for POSITION)
                for d in range(channel_dimension):
                    componentOffset = vertexOffset + component_byte_size * d
                    vertexDataSrc = componentOffset
                    componentDataSrc = component_byte_size * (v * channel_dimension + d)
                    
                    # Read component from external data blob
                    buff = m_VertexData.m_DataSize[
                        vertexDataSrc : vertexDataSrc + component_byte_size
                    ]
                    
                    # Byte swap if needed (endianness)
                    if swap:
                        buff = buff[::-1]
                    
                    # Write to component buffer
                    componentBytes[
                        componentDataSrc : componentDataSrc + component_byte_size
                    ] = buff
            
            # Unpack binary data to typed tuples
            component_data = list(
                struct.iter_unpack(
                    f">{channel_dimension}{component_dtype}",
                    componentBytes
                )
            )
            
            # Assign to appropriate mesh attribute
            self.assign_channel_vertex_data(chn, component_data)
```

### Key Algorithm Points:
1. **Base offset**: `vertexBaseOffset = m_Stream.offset + m_Channel.offset`
2. **Per-vertex offset**: `vertexOffset = vertexBaseOffset + m_Stream.stride * v`
3. **Per-component offset**: `componentOffset = vertexOffset + component_byte_size * d`
4. **Byte swap**: Only if endianness mismatch AND component size > 1
5. **Unpack**: Use struct to convert raw bytes to typed values (float, uint, etc.)

---

## 6. Handling PackedBitVector for Compressed Data

### Decompression Approach (PackedBitVector.py)

For **compressed** meshes (not common in REPO mods but included for completeness):

```python
def unpack_floats(
    packed: "PackedBitVector",
    start: int = 0,
    count: Optional[int] = None,
    shape: Optional[Tuple[int, ...]] = None,
) -> List[Any]:
    """Decompress bit-packed floating point data"""
    assert packed.m_BitSize is not None and packed.m_Range is not None and packed.m_Start is not None
    
    if packed.m_BitSize == 0:
        # All values are same (min value)
        quantized = [packed.m_Start] * (packed.m_NumItems if count is None else count)
    else:
        # Read as integers, then scale to float
        quantized_f64 = unpack_ints(packed, start, count)
        scale = packed.m_Range / ((1 << packed.m_BitSize) - 1)
        quantized = [x * scale + packed.m_Start for x in quantized_f64]
    
    return reshape(quantized, shape)


def unpack_ints(
    packed: "PackedBitVector",
    start: int = 0,
    count: Optional[int] = None,
    shape: Optional[Tuple[int, ...]] = None,
) -> List[Any]:
    """Decompress bit-packed integer data"""
    m_BitSize = packed.m_BitSize
    m_Data = packed.m_Data
    
    bitPos = m_BitSize * start
    indexPos = bitPos // 8
    bitPos %= 8
    
    if count is None:
        count = packed.m_NumItems
    
    data = [0] * count
    
    for i in range(count):
        bits = 0
        value = 0
        
        # Extract m_BitSize bits from packed data
        while bits < m_BitSize:
            # Extract bits from current byte
            value |= (m_Data[indexPos] >> bitPos) << bits
            
            # Calculate how many bits we extracted
            num = min(m_BitSize - bits, 8 - bitPos)
            
            # Move position forward
            bitPos += num
            bits += num
            
            # Move to next byte if needed
            if bitPos == 8:
                indexPos += 1
                bitPos = 0
        
        # Mask to actual bit size
        data[i] = value & ((1 << m_BitSize) - 1)
    
    return reshape(data, shape)
```

### Scaling Formula:
```
float_value = int_value * (range / ((1 << bit_size) - 1)) + start
```

---

## 7. Index Buffer Extraction

### Python Reference (MeshHelper.py, lines 159-185)

```python
def process(self):
    # ...
    if self.m_IndexBuffer:
        raw_indices = bytes(self.m_IndexBuffer)
        
        # Determine index size (16-bit vs 32-bit)
        if self.m_Use16BitIndices:
            char = "H"  # uint16
            index_size = 2
        else:
            char = "I"  # uint32
            index_size = 4
        
        # Unpack indices
        self.m_IndexBuffer = cast(
            List[int],
            struct.unpack(f"<{len(raw_indices) // index_size}{char}", raw_indices),
        )
```

### SubMesh Iteration (MeshHelper.py, lines 625-648)

```python
def get_triangles(self) -> List[List[Tuple[int, ...]]]:
    assert self.m_IndexBuffer is not None
    assert self.src.m_SubMeshes is not None
    
    submeshes: List[List[Tuple[int, ...]]] = []
    
    for m_SubMesh in self.src.m_SubMeshes:
        # Calculate start index (convert from bytes to index count)
        firstIndex = m_SubMesh.firstByte // 2
        if not self.m_Use16BitIndices:
            firstIndex //= 2
        
        indexCount = m_SubMesh.indexCount
        topology = m_SubMesh.topology
        
        # Extract triangles based on topology
        if topology == MeshTopology.Triangles:
            triangles = [
                tuple(self.m_IndexBuffer[i : i + 3]) 
                for i in range(firstIndex, firstIndex + indexCount, 3)
            ]
        # ... handle other topologies (TriangleStrip, Quads, etc.)
        
        submeshes.append(triangles)
    
    return submeshes
```

---

## Summary: C# Port Checklist

- [ ] **Detect streaming**: Check `mesh.m_StreamData?.path` is not null
- [ ] **Load external**: Use `GetResourceData(path, assetsFile, offset, size)`
- [ ] **Try filename variants**: `.resS`, `.assets.resS`, `.resource`, etc.
- [ ] **Extract channels**: Loop through `m_Channels`, check `m_Stream.channelMask`
- [ ] **Read vertices**: `offset = stream.offset + channel.offset + stream.stride * v`
- [ ] **Handle endianness**: Swap bytes if little-endian AND size > 1 byte
- [ ] **Assign channels**: Map channel ID (0-13) to mesh attribute (POSITION, NORMAL, etc.)
- [ ] **Extract indices**: Unpack as uint16 or uint32 based on `m_Use16BitIndices`
- [ ] **PackedBitVector** (if needed): Bit-unpack + scale formula

---

**Do NOT guess or reverse-engineer—copy these patterns exactly.**
