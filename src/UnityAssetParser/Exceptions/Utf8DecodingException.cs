namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when UTF-8 decoding fails (orphaned surrogates, invalid sequences).
/// </summary>
public class Utf8DecodingException : StringReadException
{
    public Utf8DecodingException(string message) : base(message)
    {
    }

    public Utf8DecodingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
