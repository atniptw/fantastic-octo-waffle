namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when SerializedFile header has inconsistent values.
/// </summary>
public class CorruptedHeaderException : SerializedFileException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorruptedHeaderException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CorruptedHeaderException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorruptedHeaderException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CorruptedHeaderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
