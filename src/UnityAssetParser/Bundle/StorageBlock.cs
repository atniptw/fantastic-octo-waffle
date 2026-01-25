namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents a storage block entry in the UnityFS BlocksInfo.
/// Each block describes a chunk of compressed/uncompressed data in the data region.
/// </summary>
public sealed class StorageBlock
{
    /// <summary>
    /// Size of the block after decompression (in bytes).
    /// </summary>
    public required uint UncompressedSize { get; init; }

    /// <summary>
    /// Size of the block in compressed form (in bytes).
    /// Equal to UncompressedSize if block is not compressed.
    /// </summary>
    public required uint CompressedSize { get; init; }

    /// <summary>
    /// Block-level flags.
    /// Bits 0-5: compression type (0=None, 1=LZMA, 2=LZ4, 3=LZ4HC, 4=LZHAM)
    /// Bit 6 (0x40): Streamed flag
    /// Other bits: reserved
    /// </summary>
    public required ushort Flags { get; init; }

    /// <summary>
    /// Compression type for this block, extracted from bits 0-5 of Flags.
    /// </summary>
    public CompressionType CompressionType => (CompressionType)(Flags & 0x3F);

    /// <summary>
    /// Indicates whether this block is streamed (loaded on-demand).
    /// Corresponds to bit 6 (0x40) of Flags.
    /// </summary>
    public bool IsStreamed => (Flags & 0x40) != 0;
}
