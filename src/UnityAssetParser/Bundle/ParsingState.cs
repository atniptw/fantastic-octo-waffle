namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents the current state in the bundle parsing state machine.
/// Used to track parsing progress and ensure correct sequencing.
/// </summary>
public enum ParsingState
{
    /// <summary>
    /// Initial state before parsing begins.
    /// </summary>
    Start,

    /// <summary>
    /// Header has been successfully parsed and validated.
    /// </summary>
    HeaderValid,

    /// <summary>
    /// BlocksInfo has been decompressed but not yet verified or parsed.
    /// </summary>
    BlocksInfoDecompressed,

    /// <summary>
    /// BlocksInfo hash has been verified (or verification was skipped).
    /// </summary>
    HashVerified,

    /// <summary>
    /// Nodes have been parsed and validated (bounds, uniqueness, overlaps).
    /// </summary>
    NodesValidated,

    /// <summary>
    /// Data region has been reconstructed from storage blocks.
    /// </summary>
    DataRegionReady,

    /// <summary>
    /// Parsing completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Parsing failed due to an error.
    /// </summary>
    Failed
}
