namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when decompression of a storage block fails.
/// </summary>
public class BlockDecompressionFailedException : BundleException
{
    public BlockDecompressionFailedException(string message) : base(message)
    {
    }

    public BlockDecompressionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
