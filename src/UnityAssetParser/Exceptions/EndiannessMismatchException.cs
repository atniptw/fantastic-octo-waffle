namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when endianness handling fails.
/// </summary>
public class EndiannessMismatchException : SerializedFileException
{
    /// <summary>
    /// The endianness byte value from the header (0 = little, 1 = big).
    /// </summary>
    public byte EndiannessValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EndiannessMismatchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="endiannessValue">The endianness byte value.</param>
    public EndiannessMismatchException(string message, byte endiannessValue)
        : base(message)
    {
        EndiannessValue = endiannessValue;
    }
}
