namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when BlocksInfo parsing fails due to malformed data,
/// truncated structures, or invalid field values.
/// </summary>
public class BlocksInfoParseException : BundleException
{
    public BlocksInfoParseException(string message) : base(message)
    {
    }

    public BlocksInfoParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
