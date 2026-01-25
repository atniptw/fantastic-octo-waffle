using UnityAssetParser.Exceptions;
using SharpCompress.Compressors.LZMA;
using K4os.Compression.LZ4;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Provides decompression functionality for LZMA, LZ4, and LZ4HC formats.
/// Thread-safe for concurrent Decompress() calls.
/// </summary>
public class Decompressor : IDecompressor
{
    private const int MaxCompressedSize = 512 * 1024 * 1024; // 512 MB
    private const int MaxUncompressedSize = int.MaxValue; // 2 GB (int.MaxValue)

    /// <summary>
    /// Decompresses data using the specified compression method.
    /// </summary>
    /// <param name="compressedData">Compressed input bytes</param>
    /// <param name="uncompressedSize">Expected uncompressed size (for validation)</param>
    /// <param name="compressionType">Compression method (0=None, 1=LZMA, 2=LZ4, 3=LZ4HC)</param>
    /// <returns>Decompressed byte array</returns>
    /// <exception cref="ArgumentNullException">Thrown when compressedData is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when sizes are negative or exceed limits</exception>
    /// <exception cref="CompressionException">Decompression failed or size mismatch</exception>
    public byte[] Decompress(byte[] compressedData, int uncompressedSize, byte compressionType)
    {
        ArgumentNullException.ThrowIfNull(compressedData);

        if (uncompressedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uncompressedSize), "Uncompressed size cannot be negative");
        }

        // Validate size limits
        if (compressedData.Length > MaxCompressedSize)
        {
            throw new CompressionException($"Compressed data size ({compressedData.Length} bytes) exceeds maximum allowed size ({MaxCompressedSize} bytes)");
        }

        if (uncompressedSize > MaxUncompressedSize)
        {
            throw new CompressionException($"Uncompressed size ({uncompressedSize} bytes) exceeds maximum allowed size ({MaxUncompressedSize} bytes)");
        }

        return compressionType switch
        {
            0 => DecompressNone(compressedData, uncompressedSize),
            1 => DecompressLZMA(compressedData, uncompressedSize),
            2 => DecompressLZ4(compressedData, uncompressedSize),
            3 => DecompressLZ4HC(compressedData, uncompressedSize),
            _ => throw new UnsupportedCompressionException($"Compression type {compressionType} is not supported")
        };
    }

    /// <summary>
    /// Handles uncompressed data (passthrough with size validation).
    /// </summary>
    private static byte[] DecompressNone(byte[] data, int expectedSize)
    {
        if (data.Length != expectedSize)
        {
            throw new DecompressionSizeMismatchException(
                $"Uncompressed data size mismatch: expected {expectedSize} bytes, got {data.Length} bytes");
        }

        return data;
    }

    /// <summary>
    /// Decompresses LZMA-compressed data.
    /// Unity LZMA format: 1 byte properties + 4 bytes dictionary size (little-endian) + compressed payload.
    /// The uncompressed size is provided separately as a parameter.
    /// </summary>
    private static byte[] DecompressLZMA(byte[] compressedData, int expectedSize)
    {
        if (compressedData.Length < 5)
        {
            throw new LzmaDecompressionException("LZMA data too short: missing properties header (expected at least 5 bytes)");
        }

        try
        {
            // Unity LZMA format: 5-byte header (1 byte props + 4 bytes dict size)
            byte[] properties = new byte[5];
            Array.Copy(compressedData, 0, properties, 0, 5);

            // Validate LZMA properties byte (lc + lp * 9 < 45, where lc <= 8, lp <= 4)
            byte propsByte = properties[0];
            int lp = (propsByte / 5) % 9;
            int lc = (propsByte / 45);

            if (lc + lp > 4)
            {
                throw new LzmaDecompressionException(
                    $"Invalid LZMA properties: lc ({lc}) + lp ({lp}) must be <= 4");
            }

            // Extract dictionary size (bytes 1-4, little-endian)
            uint dictionarySize = BitConverter.ToUInt32(properties, 1);

            // Validate dictionary size (must not exceed 512 MB)
            if (dictionarySize > 512 * 1024 * 1024)
            {
                throw new LzmaDecompressionException(
                    $"LZMA dictionary size ({dictionarySize} bytes) exceeds maximum (536870912 bytes)");
            }

            // Create input stream with compressed payload (skip 5-byte header)
            using var inputStream = new MemoryStream(compressedData, 5, compressedData.Length - 5);
            using var outputStream = new MemoryStream(expectedSize);

            // Decompress using SharpCompress
            using var decoder = new LzmaStream(properties, inputStream, expectedSize);
            decoder.CopyTo(outputStream);

            byte[] result = outputStream.ToArray();

            // Validate output size
            if (result.Length != expectedSize)
            {
                throw new LzmaDecompressionException(
                    $"LZMA decompression size mismatch: expected {expectedSize} bytes, got {result.Length} bytes");
            }

            return result;
        }
        catch (LzmaDecompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LzmaDecompressionException("LZMA decompression failed", ex);
        }
    }

    /// <summary>
    /// Decompresses LZ4-compressed data (raw block format, no frame header).
    /// </summary>
    private static byte[] DecompressLZ4(byte[] compressedData, int expectedSize)
    {
        try
        {
            byte[] output = new byte[expectedSize];

            int decodedLength = LZ4Codec.Decode(
                compressedData, 0, compressedData.Length,
                output, 0, expectedSize);

            if (decodedLength < 0)
            {
                throw new LZ4DecompressionException(
                    $"LZ4 decompression failed: decoder returned error code {decodedLength}");
            }

            if (decodedLength != expectedSize)
            {
                throw new LZ4DecompressionException(
                    $"LZ4 decompression size mismatch: expected {expectedSize} bytes, got {decodedLength} bytes");
            }

            return output;
        }
        catch (LZ4DecompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LZ4DecompressionException("LZ4 decompression failed", ex);
        }
    }

    /// <summary>
    /// Decompresses LZ4HC-compressed data.
    /// LZ4HC uses the same decompression algorithm as LZ4 (only compression differs).
    /// </summary>
    private static byte[] DecompressLZ4HC(byte[] compressedData, int expectedSize)
    {
        // LZ4HC decompression is identical to LZ4 decompression
        return DecompressLZ4(compressedData, expectedSize);
    }
}
