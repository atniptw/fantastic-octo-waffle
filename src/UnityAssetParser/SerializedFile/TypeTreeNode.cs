namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a single node in a type tree field structure.
/// Describes a field's type, name, size, and position in the hierarchy.
/// Uses a recursive tree structure to match UnityPy's architecture.
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
    /// Index in the type tree node array (original flat list position).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Type flags (bit 0x01 indicates array wrapper in modern Unity).
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

    /// <summary>
    /// Child nodes in the tree hierarchy.
    /// Empty for primitive types, populated for complex types.
    /// </summary>
    public List<TypeTreeNode> Children { get; set; } = new();

    /// <summary>
    /// Checks if this node represents an array type.
    /// Arrays have TypeFlags bit 0x01 or type name "vector"/"staticvector".
    /// </summary>
    public bool IsArray => (TypeFlags & 0x01) != 0 || Type == "vector" || Type == "staticvector";

    /// <summary>
    /// Checks if this node is a primitive type (no children).
    /// </summary>
    public bool IsPrimitive => Children.Count == 0;

    /// <summary>
    /// Gets the first child node, or null if none exist.
    /// </summary>
    public TypeTreeNode? FirstChild => Children.Count > 0 ? Children[0] : null;
}
