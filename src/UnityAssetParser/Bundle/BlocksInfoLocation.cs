namespace UnityAssetParser.Bundle;

/// <summary>
/// Contains calculated location information for BlocksInfo and data region in a UnityFS bundle.
/// Used to navigate between header, BlocksInfo, and data blocks.
/// </summary>
public sealed class BlocksInfoLocation
{
    /// <summary>
    /// Absolute file position where BlocksInfo data starts.
    /// For embedded layout: position after aligned header.
    /// For streamed layout: position at end of file minus CompressedBlocksInfoSize.
    /// </summary>
    public required long BlocksInfoPosition { get; init; }

    /// <summary>
    /// Base offset for node-relative addressing.
    /// All node offsets in BlocksInfo are relative to this position.
    /// For embedded layout: BlocksInfoPosition + CompressedBlocksInfoSize.
    /// For streamed layout: position after aligned header.
    /// </summary>
    public required long DataOffset { get; init; }

    /// <summary>
    /// Number of padding bytes applied between header end and BlocksInfo/data region.
    /// Depends on alignment requirements (4-byte or 16-byte).
    /// </summary>
    public required int AlignmentPadding { get; init; }
}
