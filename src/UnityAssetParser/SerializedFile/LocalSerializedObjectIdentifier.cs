namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a local serialized object identifier (script type reference).
/// </summary>
public sealed class LocalSerializedObjectIdentifier
{
    /// <summary>
    /// Index of the local serialized file.
    /// </summary>
    public int LocalSerializedFileIndex { get; set; }

    /// <summary>
    /// Identifier in the local file (int32 for v<14, int64 for v>=14).
    /// </summary>
    public long LocalIdentifierInFile { get; set; }
}
