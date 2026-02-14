namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents the parsed header of a UnityFS bundle file.
/// Contains metadata about the bundle format, version, size, and compression settings.
/// </summary>
public sealed class UnityFSHeader
{
    /// <summary>
    /// Magic signature identifying the file format (should be "UnityFS").
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Bundle format version (typically 6 or 7).
    /// </summary>
    public required uint Version { get; init; }

    /// <summary>
    /// Unity engine version string (e.g., "2020.3.48f1").
    /// </summary>
    public required string UnityVersion { get; init; }

    /// <summary>
    /// Unity engine revision hash (e.g., "b805b124c6b7").
    /// </summary>
    public required string UnityRevision { get; init; }

    /// <summary>
    /// Total bundle file size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Size of the compressed BlocksInfo data in bytes.
    /// </summary>
    public required uint CompressedBlocksInfoSize { get; init; }

    /// <summary>
    /// Size of the uncompressed BlocksInfo data in bytes.
    /// </summary>
    public required uint UncompressedBlocksInfoSize { get; init; }

    /// <summary>
    /// Format and control flags.
    /// Bits 0-5: compression type
    /// Bit 7: BlocksInfo at end of file
    /// Bit 9: needs padding at start (version 7+)
    /// </summary>
    public required uint Flags { get; init; }

    /// <summary>
    /// File position immediately after the Flags field (before any alignment).
    /// </summary>
    public required long HeaderEndPosition { get; init; }

    /// <summary>
    /// Compression method used for BlocksInfo.
    /// Extracted from bits 0-5 of Flags.
    /// </summary>
    public CompressionType CompressionType => (CompressionType)(Flags & 0x3F);

    /// <summary>
    /// Indicates whether BlocksInfo is located at the end of the file (streamed layout)
    /// or immediately after the header (embedded layout).
    /// Corresponds to bit 7 of Flags.
    /// </summary>
    public bool BlocksInfoAtEnd => (Flags & 0x80) != 0;

    /// <summary>
    /// Indicates whether 16-byte alignment is required before BlocksInfo (version 7+ only).
    /// Corresponds to bit 9 of Flags.
    /// </summary>
    public bool NeedsPaddingAtStart => (Flags & 0x200) != 0;

    /// <summary>
    /// Required alignment size in bytes for BlocksInfo positioning.
    /// UnityPy aligns to 16 bytes for bundle version >= 7 regardless of the padding flag.
    /// For older versions, default to 4-byte alignment.
    /// </summary>
    public int AlignmentSize => Version >= 7 ? 16 : 4;

    public bool NeedsPaddingAtStartFlagSet => (Flags & 0x200) != 0;
}
