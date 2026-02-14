namespace UnityAssetParser.Classes;

/// <summary>
/// Represents vertex channel information in Unity's VertexData structure.
/// This is a verbatim port from UnityPy/classes/generated.py ChannelInfo.
/// 
/// Channels describe how vertex attributes (positions, normals, UVs, etc.) are stored in the vertex buffer.
/// Each channel has a format, dimension, offset within its stream, and a stream index.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/generated.py
/// </summary>
public sealed class ChannelInfo
{
    /// <summary>
    /// Gets or sets the stream index this channel belongs to.
    /// </summary>
    public byte Stream { get; set; }

    /// <summary>
    /// Gets or sets the byte offset within the stream where this channel's data starts.
    /// </summary>
    public byte Offset { get; set; }

    /// <summary>
    /// Gets or sets the data format/type of this channel (e.g., float, color, etc.).
    /// Maps to VertexFormat/VertexFormat2017/VertexChannelFormat depending on Unity version.
    /// </summary>
    public byte Format { get; set; }

    /// <summary>
    /// Gets or sets the dimension/component count of this channel (e.g., 3 for Vector3, 2 for Vector2, 4 for Vector4).
    /// The lower 4 bits contain the actual dimension.
    /// </summary>
    public byte Dimension { get; set; }
}
