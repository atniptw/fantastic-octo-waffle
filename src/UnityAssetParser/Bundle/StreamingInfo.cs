namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents streaming information that references a slice of data within a .resS node.
/// Used to resolve external resource references in SerializedFiles.
/// </summary>
public sealed class StreamingInfo
{
    /// <summary>
    /// Path to the node containing the resource (e.g., "archive:/CAB-{hash}/CAB-{hash}.resS").
    /// Must match a NodeInfo.Path exactly (case-sensitive, UTF-8).
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Offset within the node's payload (relative to node start, not data region).
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// Size of the slice in bytes.
    /// </summary>
    public required long Size { get; init; }
}
