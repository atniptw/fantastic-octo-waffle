namespace UnityAssetParser.Bundle;

/// <summary>
/// Interface for parsing UnityFS bundle headers and calculating BlocksInfo locations.
/// </summary>
public interface IUnityFSHeaderParser
{
    /// <summary>
    /// Parses the UnityFS header from the beginning of a stream.
    /// </summary>
    /// <param name="stream">Binary stream positioned at start of bundle.</param>
    /// <returns>Parsed header with computed properties.</returns>
    /// <exception cref="InvalidBundleSignatureException">Signature is not "UnityFS".</exception>
    /// <exception cref="UnsupportedVersionException">Version not in {6, 7}.</exception>
    /// <exception cref="HeaderParseException">Malformed header data.</exception>
    UnityFSHeader Parse(Stream stream);

    /// <summary>
    /// Calculates BlocksInfo position and data offset based on header.
    /// </summary>
    /// <param name="header">Parsed header.</param>
    /// <param name="fileLength">Total file size for streamed layout.</param>
    /// <returns>Location information for BlocksInfo and data region.</returns>
    BlocksInfoLocation CalculateBlocksInfoLocation(UnityFSHeader header, long fileLength);
}
