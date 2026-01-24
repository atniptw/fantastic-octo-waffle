namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when string reading fails (length validation, null terminator not found).
/// </summary>
public class StringReadException : BundleException
{
    public StringReadException(string message) : base(message)
    {
    }

    public StringReadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
