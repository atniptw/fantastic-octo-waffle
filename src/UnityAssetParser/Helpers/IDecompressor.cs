using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Interface for decompressing data using various compression methods.
/// </summary>
public interface IDecompressor
{
    /// <summary>
    /// Decompresses data using the specified compression method.
    /// </summary>
    /// <param name="compressedData">Compressed input bytes</param>
    /// <param name="uncompressedSize">Expected uncompressed size (for validation)</param>
    /// <param name="compressionType">Compression method (0=None, 1=LZMA, 2=LZ4, 3=LZ4HC)</param>
    /// <returns>Decompressed byte array</returns>
    /// <exception cref="CompressionException">Decompression failed or size mismatch</exception>
    /// <exception cref="ArgumentNullException">Thrown when compressedData is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when sizes exceed limits</exception>
    byte[] Decompress(byte[] compressedData, int uncompressedSize, byte compressionType);
}
