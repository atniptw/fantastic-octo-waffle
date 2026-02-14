namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a node or slice access is out of bounds.
/// </summary>
public class BoundsException : BundleException
{
    public BoundsException(string message) : base(message)
    {
    }
}
