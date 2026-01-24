namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when padding bytes are non-zero during alignment validation.
/// </summary>
public class AlignmentValidationException : AlignmentException
{
    public AlignmentValidationException(string message) : base(message)
    {
    }

    public AlignmentValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
