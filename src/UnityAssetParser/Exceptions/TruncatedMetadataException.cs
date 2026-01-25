namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when metadata region is shorter than expected.
/// </summary>
public class TruncatedMetadataException : SerializedFileException
{
    /// <summary>
    /// Expected metadata size.
    /// </summary>
    public uint ExpectedSize { get; }

    /// <summary>
    /// Actual available size.
    /// </summary>
    public long ActualSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TruncatedMetadataException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="expectedSize">Expected metadata size.</param>
    /// <param name="actualSize">Actual available size.</param>
    public TruncatedMetadataException(string message, uint expectedSize, long actualSize)
        : base(message)
    {
        ExpectedSize = expectedSize;
        ActualSize = actualSize;
    }
}
