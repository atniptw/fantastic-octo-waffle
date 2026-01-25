namespace UnityAssetParser.Exceptions;

/// <summary>
/// Exception thrown when the SHA1 hash verification of BlocksInfo payload fails.
/// This indicates data corruption or tampering.
/// </summary>
public class HashMismatchException : BundleException
{
    /// <summary>
    /// Expected hash value (from BlocksInfo header).
    /// </summary>
    public byte[]? ExpectedHash { get; init; }

    /// <summary>
    /// Computed hash value (from payload).
    /// </summary>
    public byte[]? ComputedHash { get; init; }

    public HashMismatchException(string message) : base(message)
    {
    }

    public HashMismatchException(string message, byte[] expectedHash, byte[] computedHash) : base(message)
    {
        ExpectedHash = expectedHash;
        ComputedHash = computedHash;
    }
}
