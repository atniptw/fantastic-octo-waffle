namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when a UnityFS bundle version is not supported.
/// </summary>
public class UnsupportedVersionException : BundleException
{
    /// <summary>
    /// The unsupported version number that was encountered.
    /// </summary>
    public uint Version { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedVersionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="version">The unsupported version number.</param>
    public UnsupportedVersionException(string message, uint version)
        : base(message)
    {
        Version = version;
    }
}
