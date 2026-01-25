namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when duplicate node paths are detected in BlocksInfo.
/// Each node path must be unique within a bundle.
/// </summary>
public class DuplicateNodeException : BundleException
{
    /// <summary>
    /// The duplicate node path that was detected.
    /// </summary>
    public string? DuplicatePath { get; init; }

    public DuplicateNodeException(string message) : base(message)
    {
    }

    public DuplicateNodeException(string message, string duplicatePath) : base(message)
    {
        DuplicatePath = duplicatePath;
    }
}
