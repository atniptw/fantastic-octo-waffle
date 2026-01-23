namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when alignment calculation fails or padding validation detects misalignment.
/// </summary>
public class AlignmentException : BundleException
{
    public AlignmentException(string message) : base(message)
    {
    }

    public AlignmentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
