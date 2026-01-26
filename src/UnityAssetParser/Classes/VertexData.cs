namespace UnityAssetParser.Classes;

/// <summary>
/// Represents vertex data structure in Unity meshes.
/// This is a verbatim port from UnityPy/classes/generated.py VertexData.
/// 
/// VertexData contains the actual binary vertex buffer data (m_DataSize) along with metadata
/// describing how to interpret that data (channels and streams).
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/generated.py
/// </summary>
public sealed class VertexData
{
    /// <summary>
    /// Gets or sets the number of vertices in this mesh.
    /// </summary>
    public uint VertexCount { get; set; }

    /// <summary>
    /// Gets or sets the array of channel descriptors.
    /// Each channel describes one vertex attribute (position, normal, UV, etc.).
    /// Available in Unity version >= 4.
    /// </summary>
    public ChannelInfo[]? Channels { get; set; }

    /// <summary>
    /// Gets or sets the array of stream descriptors.
    /// Each stream groups multiple channels with a common stride.
    /// Available in Unity version >= 4 (explicitly) or computed from channels in newer versions.
    /// </summary>
    public StreamInfo[]? Streams { get; set; }

    /// <summary>
    /// Gets or sets the raw binary vertex data buffer.
    /// This contains all vertex attribute data packed according to the streams and channels.
    /// </summary>
    public byte[]? DataSize { get; set; }

    // Legacy fields for Unity version < 4
    /// <summary>
    /// Gets or sets stream 0 for Unity version &lt; 4 (legacy).
    /// </summary>
    public StreamInfo? Streams0 { get; set; }

    /// <summary>
    /// Gets or sets stream 1 for Unity version &lt; 4 (legacy).
    /// </summary>
    public StreamInfo? Streams1 { get; set; }

    /// <summary>
    /// Gets or sets stream 2 for Unity version &lt; 4 (legacy).
    /// </summary>
    public StreamInfo? Streams2 { get; set; }

    /// <summary>
    /// Gets or sets stream 3 for Unity version &lt; 4 (legacy).
    /// </summary>
    public StreamInfo? Streams3 { get; set; }
}
