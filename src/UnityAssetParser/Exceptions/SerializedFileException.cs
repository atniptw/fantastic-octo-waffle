namespace UnityAssetParser.Exceptions;

/// <summary>
/// Base exception for SerializedFile parsing errors.
/// </summary>
public class SerializedFileException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerializedFileException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SerializedFileException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializedFileException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SerializedFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
