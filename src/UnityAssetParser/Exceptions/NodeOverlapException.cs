namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when nodes overlap in the data region.
/// </summary>
public class NodeOverlapException : BundleException
{
    public NodeOverlapException(string message) : base(message)
    {
    }
}
