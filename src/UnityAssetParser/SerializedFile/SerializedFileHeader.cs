namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents the header of a SerializedFile.
/// Contains version, size, and format metadata.
/// </summary>
public sealed class SerializedFileHeader
{
    /// <summary>
    /// Size of metadata region (header + type tree + object table).
    /// </summary>
    public uint MetadataSize { get; set; }

    /// <summary>
    /// Total file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// SerializedFile format version (e.g., 22 for Unity 2022.3).
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Offset where object data region starts.
    /// </summary>
    public long DataOffset { get; set; }

    /// <summary>
    /// Endianness indicator: 0 = little-endian, 1 = big-endian.
    /// </summary>
    public byte Endianness { get; set; }

    /// <summary>
    /// Reserved padding bytes (3 bytes for version >= 22).
    /// </summary>
    public byte[]? Reserved { get; set; }

    /// <summary>
    /// Unity version string (for version &lt; 9).
    /// </summary>
    public string? UnityVersionString { get; set; }

    /// <summary>
    /// Unity version as uint (for version &lt; 14).
    /// </summary>
    public uint? UnityVersion { get; set; }

    /// <summary>
    /// Target platform (for version >= 9 &lt; 14).
    /// </summary>
    public uint? TargetPlatform { get; set; }

    /// <summary>
    /// Whether type tree is enabled (for version >= 7 &lt; 14).
    /// </summary>
    public bool? EnableTypeTree { get; set; }
}
