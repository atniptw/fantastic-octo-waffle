namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when duplicate PathID is detected in object table.
/// </summary>
public class DuplicatePathIdException : SerializedFileException
{
    /// <summary>
    /// The duplicate PathId.
    /// </summary>
    public long PathId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicatePathIdException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="pathId">The duplicate PathId.</param>
    public DuplicatePathIdException(string message, long pathId)
        : base(message)
    {
        PathId = pathId;
    }
}
