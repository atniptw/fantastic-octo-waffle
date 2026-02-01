namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents type metadata with optional field tree structure.
/// </summary>
public sealed class SerializedType
{
    /// <summary>
    /// Unity ClassID for this type.
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// Whether this is a stripped type.
    /// </summary>
    public bool IsStrippedType { get; set; }

    /// <summary>
    /// Script type index (-1 if not MonoBehaviour).
    /// </summary>
    public short ScriptTypeIndex { get; set; } = -1;

    /// <summary>
    /// Script identifier (MD5 hash, 16 bytes) for version >= 17.
    /// </summary>
    public byte[]? ScriptId { get; set; }

    /// <summary>
    /// Old type hash (16 bytes) for version >= 5.
    /// </summary>
    public byte[]? OldTypeHash { get; set; }

    /// <summary>
    /// Type dependency indices (version >= 21, regular types only).
    /// </summary>
    public int[]? TypeDependencies { get; set; }

    /// <summary>
    /// Class name (version >= 21, ref types only).
    /// Reference: UnityPy SerializedType.m_ClassName.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Namespace (version >= 21, ref types only).
    /// Reference: UnityPy SerializedType.m_NameSpace.
    /// </summary>
    public string? NameSpace { get; set; }

    /// <summary>
    /// Assembly name (version >= 21, ref types only).
    /// Reference: UnityPy SerializedType.m_AssemblyName.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Field tree nodes in flat list form (kept for compatibility).
    /// For new code, use <see cref="TreeRoot"/> instead.
    /// </summary>
    public IReadOnlyList<TypeTreeNode>? Nodes { get; set; }

    /// <summary>
    /// Root node of the hierarchical tree structure.
    /// Matches UnityPy's recursive tree architecture.
    /// This is the primary structure for traversal - prefer over <see cref="Nodes"/>.
    /// </summary>
    public TypeTreeNode? TreeRoot { get; set; }
}
