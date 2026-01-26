namespace UnityAssetParser.Classes;

/// <summary>
/// Represents a vertex stream in Unity's VertexData structure.
/// This is a verbatim port from UnityPy/classes/generated.py StreamInfo.
/// 
/// Streams organize vertex data into separate buffers. Each stream contains one or more channels
/// (identified by channelMask bits) and has a stride that determines spacing between vertices.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/generated.py
/// </summary>
public sealed class StreamInfo
{
    /// <summary>
    /// Gets or sets the bitmask indicating which channels are present in this stream.
    /// Bit N set means channel N is in this stream (e.g., bit 0 = vertex positions, bit 1 = normals).
    /// </summary>
    public uint ChannelMask { get; set; }

    /// <summary>
    /// Gets or sets the byte offset where this stream's data starts in the vertex data buffer.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Gets or sets the byte stride between consecutive vertices in this stream.
    /// Stride = sum of (dimension * component_size) for all channels in this stream.
    /// </summary>
    public uint Stride { get; set; }

    /// <summary>
    /// Gets or sets the divider operation (used in advanced streaming scenarios, typically 0).
    /// </summary>
    public uint DividerOp { get; set; }

    /// <summary>
    /// Gets or sets the frequency (used in advanced streaming scenarios, typically 0).
    /// </summary>
    public uint Frequency { get; set; }
}
