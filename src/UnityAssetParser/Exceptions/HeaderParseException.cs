namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a UnityFS header cannot be parsed due to malformed data.
/// </summary>
public class HeaderParseException : BundleException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public HeaderParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderParseException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HeaderParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
