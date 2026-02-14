namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when StreamingInfo resolution fails (missing path or out of bounds slice).
/// </summary>
public class StreamingInfoException : BundleException
{
    public StreamingInfoException(string message) : base(message)
    {
    }
}
