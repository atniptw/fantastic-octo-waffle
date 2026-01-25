namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a reference to an external file (for cross-bundle dependencies).
/// </summary>
public sealed class FileIdentifier
{
    /// <summary>
    /// GUID of the external file (16 bytes, version >= 6).
    /// </summary>
    public Guid Guid { get; set; }

    /// <summary>
    /// Type identifier (0 = not serialized, other values per Unity internal).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Path name of the external file (e.g., "archive:/CAB-hash/CAB-hash.resS").
    /// </summary>
    public string PathName { get; set; } = string.Empty;
}
