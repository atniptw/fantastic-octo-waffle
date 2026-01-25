namespace UnityAssetParser.Bundle;

/// <summary>
/// Result wrapper for TryParse operations.
/// Contains the parsed bundle (if successful) and any warnings or errors.
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// The successfully parsed bundle, or null if parsing failed.
    /// </summary>
    public BundleFile? Bundle { get; init; }

    /// <summary>
    /// Collection of non-fatal warnings encountered during parsing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Collection of errors that caused parsing to fail.
    /// Empty if Bundle is not null.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Indicates whether parsing succeeded.
    /// </summary>
    public bool Success => Bundle != null && Errors.Count == 0;
}
