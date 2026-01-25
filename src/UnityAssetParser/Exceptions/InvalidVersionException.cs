namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a SerializedFile has an invalid or unsupported format version.
/// </summary>
public class InvalidVersionException : SerializedFileException
{
    /// <summary>
    /// The invalid version number that was encountered.
    /// </summary>
    public uint Version { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidVersionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="version">The invalid version number.</param>
    public InvalidVersionException(string message, uint version)
        : base(message)
    {
        Version = version;
    }
}
