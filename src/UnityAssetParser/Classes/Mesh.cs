namespace UnityAssetParser.Classes;

/// <summary>
/// Represents a Unity Mesh object (ClassID 43).
/// This is a partial port from UnityPy/classes/Mesh.py focusing on fields needed for geometry extraction.
/// 
/// Contains vertex data, index buffer, submeshes, and optional compressed mesh data.
/// Full deserialization is complex; MeshHelper handles extraction of renderable geometry.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py
/// </summary>
public sealed class Mesh
{
    /// <summary>
    /// Gets or sets the mesh name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vertex data structure containing all vertex attributes.
    /// </summary>
    public VertexData? VertexData { get; set; }

    /// <summary>
    /// Gets or sets the compressed mesh data (used when mesh compression is enabled).
    /// </summary>
    public CompressedMesh? CompressedMesh { get; set; }

    /// <summary>
    /// Gets or sets the streaming info for external .resS resource data.
    /// When present, vertex/index data is stored externally rather than inline.
    /// </summary>
    public Bundle.StreamingInfo? StreamData { get; set; }

    /// <summary>
    /// Gets or sets the raw index buffer bytes.
    /// Interpreted as UInt16 or UInt32 array depending on Use16BitIndices or IndexFormat.
    /// </summary>
    public byte[]? IndexBuffer { get; set; }

    /// <summary>
    /// Gets or sets the submesh array defining material boundaries.
    /// </summary>
    public SubMesh[]? SubMeshes { get; set; }

    /// <summary>
    /// Gets or sets whether 16-bit indices are used (vs 32-bit).
    /// Available in Unity version &lt; 2017.4.
    /// </summary>
    public bool? Use16BitIndices { get; set; }

    /// <summary>
    /// Gets or sets the index format (0 = UInt16, 1 = UInt32).
    /// Available in Unity version >= 2017.4.
    /// </summary>
    public int? IndexFormat { get; set; }

    /// <summary>
    /// Gets or sets the mesh compression level (0 = none, 1 = low, 2 = medium, 3 = high).
    /// </summary>
    public byte MeshCompression { get; set; }

    /// <summary>
    /// Gets or sets whether the mesh is readable at runtime (can be accessed from scripts).
    /// </summary>
    public bool IsReadable { get; set; }

    /// <summary>
    /// Gets or sets whether mesh data should be kept in memory (vs unloaded after upload to GPU).
    /// </summary>
    public bool KeepVertices { get; set; }

    /// <summary>
    /// Gets or sets whether index buffer data should be kept in memory.
    /// </summary>
    public bool KeepIndices { get; set; }
}
