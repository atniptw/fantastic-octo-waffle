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
    /// The uncompressed size is provided separately as a parameter, NOT encoded in the stream.
    /// Unity uses raw LZMA format without the 8-byte uncompressed size header.
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
            // SharpCompress expects this exact format, so we can use the first 5 bytes as-is
            byte[] properties = new byte[5];
            Array.Copy(compressedData, 0, properties, 0, 5);

            // Create input stream with compressed payload (skip 5-byte header)
            using var inputStream = new MemoryStream(compressedData, 5, compressedData.Length - 5);
            using var outputStream = new MemoryStream(expectedSize);

            // Decompress using SharpCompress
            // Try with both inputSize and outputSize parameters
            try
            {
                using var decoder = new LzmaStream(properties, inputStream, compressedData.Length - 5, expectedSize);
                decoder.CopyTo(outputStream);
            }
            catch (NullReferenceException nre)
            {
                // If NullReferenceException occurs, try simpler constructor (no output size)
                inputStream.Position = 0;
                outputStream.Position = 0;
                using var decoder = new LzmaStream(properties, inputStream);
                decoder.CopyTo(outputStream);
            }

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
