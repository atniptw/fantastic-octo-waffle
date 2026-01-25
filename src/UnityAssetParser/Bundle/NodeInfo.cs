namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents a node (virtual file) entry in the UnityFS BlocksInfo.
/// Each node describes a file contained within the bundle's data region.
/// </summary>
public sealed class NodeInfo
{
    /// <summary>
    /// Offset of the node's data relative to the data region start (in bytes).
    /// Absolute file offset = data_offset + Offset.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// Size of the node's data (in bytes).
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Node-level flags (reserved, meaning unknown).
    /// Must be preserved for round-trip fidelity.
    /// </summary>
    public required int Flags { get; init; }

    /// <summary>
    /// Path/name of the node (null-terminated UTF-8 in binary format).
    /// Must be unique within the bundle.
    /// Common format: "archive:/CAB-{hash}/CAB-{hash}.resS"
    /// </summary>
    public required string Path { get; init; }
}
