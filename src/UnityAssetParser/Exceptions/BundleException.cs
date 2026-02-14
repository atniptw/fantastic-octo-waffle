namespace UnityAssetParser.Exceptions;

/// <summary>
/// Base exception class for all Unity bundle parsing errors.
/// </summary>
public class BundleException : Exception
{
    public BundleException(string message) : base(message)
    {
    }

    public BundleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
