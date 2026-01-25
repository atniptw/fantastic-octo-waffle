namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents metadata for a single object in a SerializedFile.
/// Contains PathId, position, size, and type information.
/// </summary>
public sealed class ObjectInfo
{
    /// <summary>
    /// Unique object identifier within the file (m_PathID).
    /// </summary>
    public long PathId { get; set; }

    /// <summary>
    /// Offset relative to DataOffset where this object's data starts (m_ByteStart).
    /// </summary>
    public long ByteStart { get; set; }

    /// <summary>
    /// Size of object payload in bytes (m_ByteSize).
    /// </summary>
    public uint ByteSize { get; set; }

    /// <summary>
    /// Index into type tree, or direct ClassID.
    /// </summary>
    public int TypeId { get; set; }

    /// <summary>
    /// Unity ClassID (43 = Mesh, 28 = Texture2D, etc.).
    /// Resolved from TypeId via type tree lookup.
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// Script type index for MonoBehaviour (version >= 11).
    /// </summary>
    public ushort ScriptTypeIndex { get; set; } = 0xFFFF; // -1 as ushort

    /// <summary>
    /// Stripped flag: 1 = stripped (editor-only data removed) (version >= 15).
    /// </summary>
    public byte Stripped { get; set; }
}
