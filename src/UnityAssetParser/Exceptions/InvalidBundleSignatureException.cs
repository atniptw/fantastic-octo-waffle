namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a file does not have the expected "UnityFS" signature.
/// </summary>
public class InvalidBundleSignatureException : BundleException
{
    /// <summary>
    /// The actual signature found in the file.
    /// </summary>
    public string ActualSignature { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidBundleSignatureException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="actualSignature">The actual signature that was found.</param>
    public InvalidBundleSignatureException(string message, string actualSignature)
        : base(message)
    {
        ActualSignature = actualSignature;
    }
}
