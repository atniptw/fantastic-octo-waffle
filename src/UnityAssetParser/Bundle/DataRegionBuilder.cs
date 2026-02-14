using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Builds a DataRegion by decompressing and concatenating storage blocks from a bundle stream.
/// </summary>
public sealed class DataRegionBuilder
{
    private readonly IDecompressor _decompressor;

    public DataRegionBuilder(IDecompressor decompressor)
    {
        ArgumentNullException.ThrowIfNull(decompressor);
        _decompressor = decompressor;
    }

    /// <summary>
    /// Builds a DataRegion from storage blocks in the bundle stream.
    /// </summary>
    /// <param name="bundleStream">The bundle file stream</param>
    /// <param name="dataOffset">Absolute file position where data blocks begin</param>
    /// <param name="blocks">Storage blocks metadata from BlocksInfo</param>
    /// <returns>DataRegion containing the decompressed and concatenated blocks</returns>
    /// <exception cref="BlocksInfoParseException">Thrown if blocks list is empty</exception>
    /// <exception cref="BlockFlagsException">Thrown if block flags contain reserved bits</exception>
    /// <exception cref="BlockDecompressionFailedException">Thrown if block decompression fails</exception>
    public DataRegion Build(Stream bundleStream, long dataOffset, IReadOnlyList<StorageBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(bundleStream);
        ArgumentNullException.ThrowIfNull(blocks);

        if (blocks.Count == 0)
        {
            throw new BlocksInfoParseException("Storage blocks list cannot be empty");
        }

        // Calculate total uncompressed size with overflow check
        long totalUncompressedSize = 0;
        foreach (var block in blocks)
        {
            checked
            {
                totalUncompressedSize += block.UncompressedSize;
            }
        }

        // Validate total size fits in int for in-memory buffer
        if (totalUncompressedSize > int.MaxValue)
        {
            throw new BlockDecompressionFailedException(
                $"Total uncompressed size ({totalUncompressedSize} bytes) exceeds maximum buffer size ({int.MaxValue} bytes)");
        }

        // Allocate output buffer (cast is safe after validation above)
        int bufferSize = checked((int)totalUncompressedSize);
        byte[] dataRegionBuffer = new byte[bufferSize];
        int writeOffset = 0;

        // Seek to data region start
        bundleStream.Seek(dataOffset, SeekOrigin.Begin);

        // Process each block
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            // Validate and convert CompressedSize to int
            if (block.CompressedSize > int.MaxValue)
            {
                throw new BlockDecompressionFailedException(
                    $"Block {i}: compressed size {block.CompressedSize} exceeds maximum supported size {int.MaxValue} bytes");
            }

            int compressedSize = checked((int)block.CompressedSize);
            byte[] compressedData = new byte[compressedSize];
            
            // Read compressed block data - ensure all bytes are read
            int totalBytesRead = 0;
            while (totalBytesRead < compressedSize)
            {
                int bytesRead = bundleStream.Read(compressedData, totalBytesRead, compressedSize - totalBytesRead);
                if (bytesRead == 0)
                {
                    break; // EOF reached before expected size
                }
                totalBytesRead += bytesRead;
            }
            
            if (totalBytesRead != compressedSize)
            {
                throw new BlockDecompressionFailedException(
                    $"Block {i}: failed to read compressed data (expected {compressedSize} bytes, got {totalBytesRead} bytes)");
            }

            // Decompress block
            byte[] decompressedData;
            try
            {
                decompressedData = _decompressor.Decompress(
                    compressedData,
                    (int)block.UncompressedSize,
                    (byte)block.CompressionType);
            }
            catch (Exception ex) when (ex is not BlockDecompressionFailedException)
            {
                throw new BlockDecompressionFailedException(
                    $"Block {i}: decompression failed using {block.CompressionType} compression", ex);
            }

            // Validate decompressed size
            if (decompressedData.Length != block.UncompressedSize)
            {
                throw new BlockDecompressionFailedException(
                    $"Block {i}: decompressed size mismatch (expected {block.UncompressedSize} bytes, got {decompressedData.Length} bytes)");
            }

            // Copy to output buffer
            Array.Copy(decompressedData, 0, dataRegionBuffer, writeOffset, decompressedData.Length);
            writeOffset += decompressedData.Length;
        }

        return new DataRegion(dataRegionBuffer);
    }
}
