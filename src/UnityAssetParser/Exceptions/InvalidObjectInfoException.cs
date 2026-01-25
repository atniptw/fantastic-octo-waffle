namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when an ObjectInfo entry is invalid.
/// </summary>
public class InvalidObjectInfoException : SerializedFileException
{
    /// <summary>
    /// The PathId of the invalid object, if available.
    /// </summary>
    public long? PathId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidObjectInfoException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidObjectInfoException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidObjectInfoException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="pathId">The PathId of the invalid object.</param>
    public InvalidObjectInfoException(string message, long pathId)
        : base(message)
    {
        PathId = pathId;
    }
}
