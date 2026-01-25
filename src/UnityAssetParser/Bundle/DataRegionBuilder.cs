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

        // Allocate output buffer
        byte[] dataRegionBuffer = new byte[totalUncompressedSize];
        int writeOffset = 0;

        // Seek to data region start
        bundleStream.Seek(dataOffset, SeekOrigin.Begin);

        // Process each block
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            // Validate block flags - reject non-zero reserved bits (bits 7-15)
            if ((block.Flags & 0xFF80) != 0)
            {
                throw new BlockFlagsException(
                    $"Block {i} has non-zero reserved flag bits: 0x{block.Flags:X4} (reserved bits: 0x{block.Flags & 0xFF80:X4})");
            }

            // Read compressed block data
            byte[] compressedData = new byte[block.CompressedSize];
            int bytesRead = bundleStream.Read(compressedData, 0, compressedData.Length);
            
            if (bytesRead != block.CompressedSize)
            {
                throw new BlockDecompressionFailedException(
                    $"Block {i}: failed to read compressed data (expected {block.CompressedSize} bytes, got {bytesRead} bytes)");
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
