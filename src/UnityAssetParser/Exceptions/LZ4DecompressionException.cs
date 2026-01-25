namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when LZ4 or LZ4HC decompression fails.
/// </summary>
public class LZ4DecompressionException : CompressionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4DecompressionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public LZ4DecompressionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4DecompressionException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LZ4DecompressionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
