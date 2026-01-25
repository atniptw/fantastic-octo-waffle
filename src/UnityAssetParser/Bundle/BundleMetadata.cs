using System.Text.Json.Serialization;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Metadata representation of a bundle for JSON serialization and validation.
/// Includes header, storage blocks, and node metadata without binary data.
/// </summary>
public sealed class BundleMetadata
{
    /// <summary>
    /// Header metadata.
    /// </summary>
    [JsonPropertyName("header")]
    public required HeaderMetadata Header { get; init; }

    /// <summary>
    /// Storage blocks metadata.
    /// </summary>
    [JsonPropertyName("storage_blocks")]
    public required IReadOnlyList<StorageBlockMetadata> StorageBlocks { get; init; }

    /// <summary>
    /// Node metadata.
    /// </summary>
    [JsonPropertyName("nodes")]
    public required IReadOnlyList<NodeMetadata> Nodes { get; init; }

    /// <summary>
    /// Absolute file offset where data region begins.
    /// </summary>
    [JsonPropertyName("data_offset")]
    public required long DataOffset { get; init; }
}

/// <summary>
/// Header metadata for JSON serialization.
/// </summary>
public sealed class HeaderMetadata
{
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }

    [JsonPropertyName("version")]
    public required uint Version { get; init; }

    [JsonPropertyName("unity_version")]
    public required string UnityVersion { get; init; }

    [JsonPropertyName("unity_revision")]
    public required string UnityRevision { get; init; }

    [JsonPropertyName("size")]
    public required long Size { get; init; }

    [JsonPropertyName("compressed_blocks_info_size")]
    public required uint CompressedBlocksInfoSize { get; init; }

    [JsonPropertyName("uncompressed_blocks_info_size")]
    public required uint UncompressedBlocksInfoSize { get; init; }

    [JsonPropertyName("flags")]
    public required uint Flags { get; init; }
}

/// <summary>
/// Storage block metadata for JSON serialization.
/// </summary>
public sealed class StorageBlockMetadata
{
    [JsonPropertyName("uncompressed_size")]
    public required uint UncompressedSize { get; init; }

    [JsonPropertyName("compressed_size")]
    public required uint CompressedSize { get; init; }

    [JsonPropertyName("flags")]
    public required ushort Flags { get; init; }
}

/// <summary>
/// Node metadata for JSON serialization.
/// </summary>
public sealed class NodeMetadata
{
    [JsonPropertyName("offset")]
    public required long Offset { get; init; }

    [JsonPropertyName("size")]
    public required long Size { get; init; }

    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
