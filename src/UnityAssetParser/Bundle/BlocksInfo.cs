namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents the parsed BlocksInfo structure from a UnityFS bundle.
/// Contains the storage blocks table, node directory, and hash for verification.
/// </summary>
public sealed class BlocksInfo
{
    /// <summary>
    /// SHA1 hash of the uncompressed BlocksInfo payload (20 bytes).
    /// This is the hash of all data following the hash field itself.
    /// Used for integrity verification.
    /// </summary>
    public required byte[] UncompressedDataHash { get; init; }

    /// <summary>
    /// List of storage blocks describing the compressed/uncompressed chunks
    /// in the data region.
    /// </summary>
    public required IReadOnlyList<StorageBlock> Blocks { get; init; }

    /// <summary>
    /// List of nodes (virtual files) contained in the bundle.
    /// Each node references a slice of the decompressed data region.
    /// </summary>
    public required IReadOnlyList<NodeInfo> Nodes { get; init; }

    /// <summary>
    /// Total size of the uncompressed data region (sum of all block uncompressed sizes).
    /// Computed for convenience and validation purposes.
    /// </summary>
    public long TotalUncompressedDataSize => Blocks.Sum(b => (long)b.UncompressedSize);
}
