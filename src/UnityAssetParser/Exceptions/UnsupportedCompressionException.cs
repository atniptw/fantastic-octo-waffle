namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when an unsupported compression type is encountered.
/// </summary>
public class UnsupportedCompressionException : CompressionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCompressionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnsupportedCompressionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCompressionException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnsupportedCompressionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
