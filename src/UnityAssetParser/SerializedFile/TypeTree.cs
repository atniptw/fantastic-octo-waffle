namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Container for type metadata including type tree information.
/// </summary>
public sealed class TypeTree
{
    private readonly List<SerializedType> _types;
    private readonly bool _hasTypeTree;

    /// <summary>
    /// Gets the list of types in this type tree.
    /// </summary>
    public IReadOnlyList<SerializedType> Types => _types;

    /// <summary>
    /// Gets whether field trees are included for types.
    /// </summary>
    public bool HasTypeTree => _hasTypeTree;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeTree"/> class.
    /// </summary>
    /// <param name="types">List of serialized types.</param>
    /// <param name="hasTypeTree">Whether type tree field structures are present.</param>
    public TypeTree(List<SerializedType> types, bool hasTypeTree)
    {
        _types = types ?? throw new ArgumentNullException(nameof(types));
        _hasTypeTree = hasTypeTree;
    }

    /// <summary>
    /// Gets a type by its index.
    /// </summary>
    /// <param name="typeId">Type index.</param>
    /// <returns>SerializedType if found, null otherwise.</returns>
    public SerializedType? GetType(int typeId)
    {
        if (typeId < 0 || typeId >= _types.Count)
        {
            return null;
        }
        return _types[typeId];
    }

    /// <summary>
    /// Resolves a TypeId to its ClassId.
    /// </summary>
    /// <param name="typeId">Type index.</param>
    /// <returns>ClassId if found, null otherwise.</returns>
    public int? ResolveClassId(int typeId)
    {
        var type = GetType(typeId);
        return type?.ClassId;
    }
}
