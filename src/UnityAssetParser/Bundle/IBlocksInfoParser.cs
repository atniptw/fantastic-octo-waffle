namespace UnityAssetParser.Bundle;

/// <summary>
/// Interface for parsing UnityFS BlocksInfo structures.
/// </summary>
public interface IBlocksInfoParser
{
    /// <summary>
    /// Parses BlocksInfo from compressed data with hash verification and validation.
    /// </summary>
    /// <param name="compressedData">Compressed BlocksInfo blob.</param>
    /// <param name="expectedUncompressedSize">Expected size after decompression.</param>
    /// <param name="compressionType">Compression method to use.</param>
    /// <param name="dataOffset">Base offset for node addressing (from Stream B).</param>
    /// <returns>Parsed and validated BlocksInfo structure.</returns>
    BlocksInfo Parse(ReadOnlySpan<byte> compressedData, int expectedUncompressedSize, CompressionType compressionType, long dataOffset);
}
