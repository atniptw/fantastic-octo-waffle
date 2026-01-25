namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a single node in a type tree field structure.
/// Describes a field's type, name, size, and position in the hierarchy.
/// </summary>
public sealed class TypeTreeNode
{
    /// <summary>
    /// Field type name (e.g., "Vector3f", "int", "string").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Field name (e.g., "m_Position", "m_Size").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Size of this field in bytes (-1 for variable-length types).
    /// </summary>
    public int ByteSize { get; set; }

    /// <summary>
    /// Index in the type tree node array.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Type flags (bit 0x4000 indicates array).
    /// </summary>
    public int TypeFlags { get; set; }

    /// <summary>
    /// Version of this field.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Metadata flags.
    /// </summary>
    public int MetaFlag { get; set; }

    /// <summary>
    /// Tree depth level (0 = root, 1 = child, etc.).
    /// </summary>
    public int Level { get; set; }
}
