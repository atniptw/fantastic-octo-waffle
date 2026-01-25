namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when decompressed data size does not match the expected size.
/// </summary>
public class DecompressionSizeMismatchException : CompressionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecompressionSizeMismatchException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DecompressionSizeMismatchException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecompressionSizeMismatchException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DecompressionSizeMismatchException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
