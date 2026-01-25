namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when block flags contain non-zero reserved bits.
/// </summary>
public class BlockFlagsException : BundleException
{
    public BlockFlagsException(string message) : base(message)
    {
    }
}
