namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a read operation would exceed stream length.
/// </summary>
public class StreamBoundsException : BundleException
{
    public StreamBoundsException(string message) : base(message)
    {
    }

    public StreamBoundsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
