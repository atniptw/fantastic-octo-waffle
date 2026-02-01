# UnityPy C# Port Stubs - Vertex Data & Streaming

Complete C# method stubs derived directly from UnityPy Python implementation.

---

## 1. Detect and Load External Vertex Data

**Source**: UnityPy/helpers/MeshHelper.py, lines 134-159

```csharp
/// <summary>
/// Load external vertex data from .resS resource file if mesh has m_StreamData
/// </summary>
private void LoadExternalVertexData(Mesh mesh, VertexData vertexData)
{
    // Check if mesh has external streaming data
    if (mesh.m_StreamData != null && !string.IsNullOrEmpty(mesh.m_StreamData.path))
    {
        var streamData = mesh.m_StreamData;
        
        if (mesh.objectReader == null)
            throw new InvalidOperationException("No object reader assigned to the input Mesh");
        
        // Load data from external .resS file using offset and size
        byte[] externalData = GetResourceData(
            streamData.path,
            mesh.objectReader.assetsFile,
            (long)streamData.offset,
            (long)streamData.size
        );
        
        // Replace/populate m_DataSize with external data
        vertexData.m_DataSize = externalData;
    }
}
```

---

## 2. Get Resource Data (ResourceReader Pattern)

**Source**: UnityPy/helpers/ResourceReader.py

```csharp
/// <summary>
/// Load data from external .resS resource file
/// Tries multiple filename variations to locate the file
/// </summary>
public static byte[] GetResourceData(
    string resPath,
    SerializedFile assetsFile,
    long offset,
    long size)
{
    string basename = Path.GetFileName(resPath);
    string name = Path.GetFileNameWithoutExtension(basename);
    
    // Try multiple possible filename variations (standard UnityPy approach)
    string[] possibleNames = new[]
    {
        basename,                    // Original filename from StreamingInfo.path
        $"{name}.resource",
        $"{name}.assets.resS",
        $"{name}.resS",             // Standard variation
    };
    
    var environment = assetsFile.environment;
    EndianBinaryReader reader = null;
    
    // First pass: search cache for already-loaded files
    foreach (string possibleName in possibleNames)
    {
        reader = environment.GetCab(possibleName);
        if (reader != null)
            break;
    }
    
    // Second pass: load dependencies if not found
    if (reader == null)
    {
        assetsFile.LoadDependencies(possibleNames);
        
        foreach (string possibleName in possibleNames)
        {
            reader = environment.GetCab(possibleName);
            if (reader != null)
                break;
        }
    }
    
    if (reader == null)
        throw new FileNotFoundException($"Resource file '{basename}' not found");
    
    // Read data at offset
    reader.Position = offset;
    return reader.ReadBytes((int)size);
}
```

---

## 3. Read Vertex Data Stream

**Source**: UnityPy/helpers/MeshHelper.py, lines 324-388

```csharp
/// <summary>
/// Extract individual vertex attributes (POSITION, NORMAL, TEXCOORD0, etc.)
/// from the vertex data blob using channel and stream information
/// </summary>
private void ReadVertexData(
    List<ChannelInfo> m_Channels,
    List<StreamInfo> m_Streams)
{
    VertexData m_VertexData = this.src.m_VertexData;
    
    if (m_VertexData == null)
        return;
    
    int m_VertexCount = m_VertexData.m_VertexCount;
    this.m_VertexCount = m_VertexCount;
    
    byte[] vertexDataBlob = m_VertexData.m_DataSize; // External or inline data
    
    // Iterate through each channel (POSITION, NORMAL, COLOR, TEXCOORD0, etc.)
    for (int chn = 0; chn < m_Channels.Count; chn++)
    {
        ChannelInfo m_Channel = m_Channels[chn];
        
        // Skip if dimension is 0 (channel not present)
        if (m_Channel.dimension == 0)
            continue;
        
        StreamInfo m_Stream = m_Streams[m_Channel.stream];
        
        // Check if this channel is active in this stream (via bitmask)
        int channelMask = m_Stream.channelMask;
        if (((channelMask >> chn) & 1) == 0)
            continue; // Channel not in this stream
        
        // Get data type information for this channel
        char componentDtype = GetChannelDtype(m_Channel);        // e.g., 'f', 'H', 'B'
        int componentByteSize = GetChannelComponentSize(m_Channel);
        bool swap = this.endianess == "<" && componentByteSize > 1;
        int channelDimension = m_Channel.dimension & 0xF;
        
        // Allocate buffer for all components of this channel across all vertices
        byte[] componentBytes = new byte[
            m_VertexCount * channelDimension * componentByteSize
        ];
        
        // Extract raw bytes for this channel from the vertex data
        int vertexBaseOffset = m_Stream.offset + m_Channel.offset;
        
        for (int v = 0; v < m_VertexCount; v++)
        {
            int vertexOffset = vertexBaseOffset + m_Stream.stride * v;
            
            // For each component of the attribute (X, Y, Z for POSITION; X, Y for TEXCOORD0, etc.)
            for (int d = 0; d < channelDimension; d++)
            {
                int componentOffset = vertexOffset + componentByteSize * d;
                int vertexDataSrc = componentOffset;
                int componentDataDst = componentByteSize * (v * channelDimension + d);
                
                // Read component from external/inline data
                // CRITICAL: Must not go out of bounds
                if (vertexDataSrc + componentByteSize > vertexDataBlob.Length)
                    throw new InvalidOperationException(
                        $"Vertex data access out of bounds: offset={vertexDataSrc}, size={componentByteSize}");
                
                Array.Copy(
                    vertexDataBlob,
                    vertexDataSrc,
                    componentBytes,
                    componentDataDst,
                    componentByteSize
                );
                
                // Byte swap if needed (endianness mismatch)
                if (swap)
                {
                    Array.Reverse(componentBytes, componentDataDst, componentByteSize);
                }
            }
        }
        
        // Unpack binary buffer to typed tuples (float, uint, etc.)
        var componentData = UnpackComponentData(
            componentBytes,
            componentDtype,
            channelDimension,
            componentByteSize,
            m_VertexCount
        );
        
        // Assign to appropriate mesh attribute based on channel ID
        AssignChannelVertexData(chn, componentData);
    }
}
```

---

## 4. Get Channel Data Type

**Source**: UnityPy/helpers/MeshHelper.py, lines 445-461

```csharp
/// <summary>
/// Map ChannelInfo format field to struct.unpack format character
/// Returns: 'f' (float), 'H' (uint16), 'B' (uint8), 'I' (uint32), etc.
/// </summary>
private char GetChannelDtype(ChannelInfo m_Channel)
{
    if (this.version[0] < 2017)
    {
        // VertexChannelFormat enum
        var format = (VertexChannelFormat)m_Channel.format;
        return VERTEX_CHANNEL_FORMAT_STRUCT_TYPE_MAP[format];
    }
    else if (this.version[0] < 2019)
    {
        // VertexFormat2017 enum
        var format = (VertexFormat2017)m_Channel.format;
        return VERTEX_FORMAT_2017_STRUCT_TYPE_MAP[format];
    }
    else
    {
        // VertexFormat enum (2019+)
        var format = (VertexFormat)m_Channel.format;
        return VERTEX_FORMAT_STRUCT_TYPE_MAP[format];
    }
}

private int GetChannelComponentSize(ChannelInfo m_Channel)
{
    char dtype = GetChannelDtype(m_Channel);
    
    return dtype switch
    {
        'B' or 'b' => 1,  // uint8, int8
        'H' or 'h' => 2,  // uint16, int16
        'f' or 'I' or 'i' => 4,  // float, uint32, int32
        'Q' or 'q' => 8,  // uint64, int64
        'e' => 2,         // float16
        _ => throw new ArgumentException($"Unknown dtype: {dtype}")
    };
}
```

---

## 5. Assign Channel Vertex Data

**Source**: UnityPy/helpers/MeshHelper.py, lines 390-425

```csharp
/// <summary>
/// Assign unpacked component data to the appropriate mesh attribute
/// based on channel ID
/// </summary>
private void AssignChannelVertexData(int channel, List<object> componentData)
{
    if (this.version[0] >= 2018)
    {
        // 2018+ channel mapping
        switch (channel)
        {
            case 0:  // kShaderChannelVertex
                this.m_Vertices = componentData.Cast<(float, float, float)>().ToList();
                break;
            case 1:  // kShaderChannelNormal
                this.m_Normals = componentData.Cast<(float, float, float)>().ToList();
                break;
            case 2:  // kShaderChannelTangent
                this.m_Tangents = componentData.Cast<(float, float, float, float)>().ToList();
                break;
            case 3:  // kShaderChannelColor
                this.m_Colors = componentData.Cast<(float, float, float, float)>().ToList();
                break;
            case 4:  // kShaderChannelTexCoord0
                this.m_UV0 = componentData.Cast<(float, float)>().ToList();
                break;
            case 5:  // kShaderChannelTexCoord1
                this.m_UV1 = componentData.Cast<(float, float)>().ToList();
                break;
            case 6:  // kShaderChannelTexCoord2
                this.m_UV2 = componentData.Cast<(float, float)>().ToList();
                break;
            case 7:  // kShaderChannelTexCoord3
                this.m_UV3 = componentData.Cast<(float, float)>().ToList();
                break;
            case 8:  // kShaderChannelTexCoord4
                this.m_UV4 = componentData.Cast<(float, float)>().ToList();
                break;
            case 9:  // kShaderChannelTexCoord5
                this.m_UV5 = componentData.Cast<(float, float)>().ToList();
                break;
            case 10: // kShaderChannelTexCoord6
                this.m_UV6 = componentData.Cast<(float, float)>().ToList();
                break;
            case 11: // kShaderChannelTexCoord7
                this.m_UV7 = componentData.Cast<(float, float)>().ToList();
                break;
            case 12: // kShaderChannelBlendWeight
                this.m_BoneWeights = componentData.Cast<(float, float, float, float)>().ToList();
                break;
            case 13: // kShaderChannelBlendIndices
                this.m_BoneIndices = componentData.Cast<(uint, uint, uint, uint)>().ToList();
                break;
            default:
                throw new ArgumentException($"Unknown channel {channel}");
        }
    }
    else
    {
        // Pre-2018 channel mapping (similar but different order)
        // ... implement as per UnityPy legacy mapping
    }
}
```

---

## 6. Unpack Component Data

**Source**: UnityPy/UnityPyBoost/Mesh.cpp (C++ optimized), fallback to Python logic

```csharp
/// <summary>
/// Convert packed binary buffer to typed tuples
/// Emulates: struct.iter_unpack(f">{dim}{dtype}", data)
/// </summary>
private List<object> UnpackComponentData(
    byte[] componentBytes,
    char dtype,
    int dimension,
    int componentByteSize,
    int vertexCount)
{
    var result = new List<object>(vertexCount);
    
    for (int i = 0; i < vertexCount; i++)
    {
        int baseOffset = i * dimension * componentByteSize;
        var tuple = new object[dimension];
        
        for (int d = 0; d < dimension; d++)
        {
            int offset = baseOffset + d * componentByteSize;
            tuple[d] = UnpackSingleComponent(componentBytes, offset, dtype, componentByteSize);
        }
        
        // Create typed tuple
        result.Add(CreateTuple(tuple));
    }
    
    return result;
}

private object UnpackSingleComponent(byte[] data, int offset, char dtype, int size)
{
    return dtype switch
    {
        'f' when size == 4 => BitConverter.ToSingle(data, offset),
        'e' when size == 2 => Half.ToHalf(data, offset), // Float16
        'H' when size == 2 => BitConverter.ToUInt16(data, offset),
        'h' when size == 2 => BitConverter.ToInt16(data, offset),
        'I' when size == 4 => BitConverter.ToUInt32(data, offset),
        'i' when size == 4 => BitConverter.ToInt32(data, offset),
        'Q' when size == 8 => BitConverter.ToUInt64(data, offset),
        'q' when size == 8 => BitConverter.ToInt64(data, offset),
        'B' when size == 1 => data[offset],
        'b' when size == 1 => (sbyte)data[offset],
        _ => throw new ArgumentException($"Unknown dtype: {dtype}, size: {size}")
    };
}

private object CreateTuple(object[] components)
{
    return components.Length switch
    {
        2 => ((float)components[0], (float)components[1]),
        3 => ((float)components[0], (float)components[1], (float)components[2]),
        4 => ((float)components[0], (float)components[1], (float)components[2], (float)components[3]),
        _ => throw new ArgumentException($"Unsupported tuple length: {components.Length}")
    };
}
```

---

## 7. Extract Index Buffer

**Source**: UnityPy/helpers/MeshHelper.py, lines 625-648

```csharp
/// <summary>
/// Extract triangles from index buffer by submesh
/// </summary>
private List<List<(int, int, int)>> GetTriangles()
{
    if (this.m_IndexBuffer == null || this.src.m_SubMeshes == null)
        return new List<List<(int, int, int)>>();
    
    var submeshes = new List<List<(int, int, int)>>();
    
    foreach (SubMesh m_SubMesh in this.src.m_SubMeshes)
    {
        // Calculate start index from byte offset
        int firstIndex = m_SubMesh.firstByte / 2;
        if (!this.m_Use16BitIndices)
            firstIndex /= 2;
        
        int indexCount = m_SubMesh.indexCount;
        MeshTopology topology = m_SubMesh.topology;
        
        var triangles = new List<(int, int, int)>();
        
        if (topology == MeshTopology.Triangles)
        {
            // Each 3 indices form a triangle
            for (int i = firstIndex; i < firstIndex + indexCount; i += 3)
            {
                if (i + 2 < this.m_IndexBuffer.Count)
                {
                    triangles.Add((
                        this.m_IndexBuffer[i],
                        this.m_IndexBuffer[i + 1],
                        this.m_IndexBuffer[i + 2]
                    ));
                }
            }
        }
        else if (topology == MeshTopology.TriangleStrip)
        {
            // Strip topology: vertices alternate winding
            for (int i = firstIndex; i < firstIndex + indexCount - 2; i++)
            {
                if (((i - firstIndex) & 1) == 1)
                {
                    // Odd: reverse winding
                    triangles.Add((
                        this.m_IndexBuffer[i + 1],
                        this.m_IndexBuffer[i],
                        this.m_IndexBuffer[i + 2]
                    ));
                }
                else
                {
                    // Even: normal winding
                    triangles.Add((
                        this.m_IndexBuffer[i],
                        this.m_IndexBuffer[i + 1],
                        this.m_IndexBuffer[i + 2]
                    ));
                }
            }
        }
        // ... handle other topologies as needed
        
        submeshes.Add(triangles);
    }
    
    return submeshes;
}
```

---

## 8. Bounds Check (CRITICAL)

**Source**: UnityPyBoost/Mesh.cpp, lines 49-68

```csharp
/// <summary>
/// Validate that vertex data access won't go out of bounds
/// CRITICAL: Do this before reading to prevent buffer overruns
/// </summary>
private void ValidateVertexDataAccess(
    VertexData vertexData,
    int vertexCount,
    int streamStride,
    int streamOffset,
    int channelOffset,
    int componentByteSize,
    int channelDimension)
{
    int maxVertexDataAccess = 
        (vertexCount - 1) * streamStride + 
        channelOffset + 
        streamOffset + 
        componentByteSize * (channelDimension - 1) + 
        componentByteSize;
    
    if (maxVertexDataAccess > vertexData.m_DataSize.Length)
    {
        throw new InvalidOperationException(
            $"Vertex data access out of bounds: " +
            $"max={maxVertexDataAccess}, length={vertexData.m_DataSize.Length}"
        );
    }
}
```

---

## Implementation Order (Recommended)

1. **GetResourceData()** - Core file loading (no dependencies)
2. **GetChannelDtype()** + **GetChannelComponentSize()** - Data type mapping
3. **ValidateVertexDataAccess()** - Safety checks
4. **UnpackComponentData()** - Binary unpacking
5. **AssignChannelVertexData()** - Channel assignment
6. **ReadVertexData()** - Main extraction loop (uses 2-5)
7. **LoadExternalVertexData()** - External data loading (uses 1)
8. **GetTriangles()** - Index extraction

---

## Testing Checklist

- [ ] Load `.resS` file with correct offset/size
- [ ] Unpack float/uint16/uint32 components correctly
- [ ] Handle byte swapping for endianness
- [ ] Extract all 8+ UV channels
- [ ] Bounds check: no buffer overruns
- [ ] Support both 16-bit and 32-bit indices
- [ ] Validate against Python reference output (JSON diff)

---

**Key Principle**: Every method above is a direct port from UnityPy.  
Do NOT modify logic without comparing against source.
