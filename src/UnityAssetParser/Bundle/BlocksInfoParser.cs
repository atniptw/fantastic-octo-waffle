using System.Security.Cryptography;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Parser for UnityFS BlocksInfo structure.
/// Handles decompression, hash verification, and parsing of storage blocks and node tables.
/// </summary>
public class BlocksInfoParser : IBlocksInfoParser
{
    private readonly IDecompressor _decompressor;

    public BlocksInfoParser(IDecompressor decompressor)
    {
        _decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Parses BlocksInfo from compressed data with hash verification and validation.
    /// </summary>
    /// <param name="compressedData">Compressed BlocksInfo blob.</param>
    /// <param name="expectedUncompressedSize">Expected size after decompression.</param>
    /// <param name="compressionType">Compression method to use.</param>
    /// <param name="dataOffset">Base offset for node addressing (from Stream B).</param>
    /// <returns>Parsed and validated BlocksInfo structure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if compressedData is null.</exception>
    /// <exception cref="DecompressionSizeMismatchException">Thrown if decompressed size doesn't match expected.</exception>
    /// <exception cref="BlocksInfoParseException">Thrown if parsing fails or data is malformed.</exception>
    /// <exception cref="HashMismatchException">Thrown if SHA1 verification fails.</exception>
    /// <exception cref="DuplicateNodeException">Thrown if duplicate node paths are detected.</exception>
    public BlocksInfo Parse(ReadOnlySpan<byte> compressedData, int expectedUncompressedSize, CompressionType compressionType, long dataOffset)
    {
        if (compressedData.IsEmpty)
        {
            throw new ArgumentException("Compressed data cannot be empty", nameof(compressedData));
        }

        if (expectedUncompressedSize < 20)
        {
            throw new BlocksInfoParseException(
                $"BlocksInfo size too small: expected at least 20 bytes for hash, got {expectedUncompressedSize}");
        }

        // Step 1: Decompress BlocksInfo
        byte[] uncompressedData = DecompressBlocksInfo(compressedData, expectedUncompressedSize, compressionType);

        // Step 2: Verify SHA1 hash
        VerifyHash(uncompressedData);

        // Step 3: Parse tables
        BlocksInfo blocksInfo = ParseTables(uncompressedData);

        // Step 4: Validate nodes
        ValidateNodes(blocksInfo, dataOffset);

        return blocksInfo;
    }

    /// <summary>
    /// Decompresses BlocksInfo using the specified compression type.
    /// </summary>
    private byte[] DecompressBlocksInfo(ReadOnlySpan<byte> compressedData, int expectedUncompressedSize, CompressionType compressionType)
    {
        try
        {
            byte[] compressedArray = compressedData.ToArray();
            byte[] uncompressedData = _decompressor.Decompress(compressedArray, expectedUncompressedSize, (byte)compressionType);

            if (uncompressedData.Length != expectedUncompressedSize)
            {
                throw new DecompressionSizeMismatchException(
                    $"BlocksInfo decompression size mismatch: expected {expectedUncompressedSize} bytes, got {uncompressedData.Length} bytes");
            }

            return uncompressedData;
        }
        catch (UnsupportedCompressionException)
        {
            throw;
        }
        catch (DecompressionSizeMismatchException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BlocksInfoParseException("Failed to decompress BlocksInfo", ex);
        }
    }

    /// <summary>
    /// Verifies SHA1 hash of BlocksInfo payload.
    /// Hash is computed over bytes [20..end], excluding the hash field itself.
    /// </summary>
    private static void VerifyHash(byte[] uncompressedData)
    {
        if (uncompressedData.Length < 20)
        {
            throw new BlocksInfoParseException(
                $"BlocksInfo too short for hash verification: {uncompressedData.Length} bytes (expected at least 20)");
        }

        // Extract expected hash (first 20 bytes)
        byte[] expectedHash = new byte[20];
        Array.Copy(uncompressedData, 0, expectedHash, 0, 20);

        // Compute hash over payload (bytes 20..end)
        byte[] payload = new byte[uncompressedData.Length - 20];
        Array.Copy(uncompressedData, 20, payload, 0, payload.Length);

        byte[] computedHash = SHA1.HashData(payload);

        // Compare hashes
        if (!expectedHash.SequenceEqual(computedHash))
        {
            throw new HashMismatchException(
                $"BlocksInfo hash mismatch. Expected: {Convert.ToHexString(expectedHash)}, Computed: {Convert.ToHexString(computedHash)}",
                expectedHash,
                computedHash);
        }
    }

    /// <summary>
    /// Parses storage blocks and nodes from uncompressed BlocksInfo data.
    /// </summary>
    private static BlocksInfo ParseTables(byte[] uncompressedData)
    {
        using var stream = new MemoryStream(uncompressedData);
        using var reader = new BinaryReader(stream);

        // Skip hash (first 20 bytes)
        reader.BaseStream.Seek(20, SeekOrigin.Begin);

        try
        {
            // Parse storage blocks
            int blockCount = reader.ReadInt32();
            if (blockCount < 0)
            {
                throw new BlocksInfoParseException($"Invalid block count: {blockCount}");
            }

            var blocks = new List<StorageBlock>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                var block = new StorageBlock
                {
                    UncompressedSize = reader.ReadUInt32(),
                    CompressedSize = reader.ReadUInt32(),
                    Flags = reader.ReadUInt16()
                };
                blocks.Add(block);
            }

            // Align to 4-byte boundary before node count
            reader.Align(alignment: 4, validatePadding: true);

            // Parse nodes
            int nodeCount = reader.ReadInt32();
            if (nodeCount < 0)
            {
                throw new BlocksInfoParseException($"Invalid node count: {nodeCount}");
            }

            var nodes = new List<NodeInfo>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new NodeInfo
                {
                    Offset = reader.ReadInt64(),
                    Size = reader.ReadInt64(),
                    Flags = reader.ReadInt32(),
                    Path = reader.ReadUtf8NullTerminated(maxLength: 65536) // 64 KiB limit
                };
                nodes.Add(node);
            }

            // Extract hash for return
            byte[] hash = new byte[20];
            Array.Copy(uncompressedData, 0, hash, 0, 20);

            return new BlocksInfo
            {
                UncompressedDataHash = hash,
                Blocks = blocks.AsReadOnly(),
                Nodes = nodes.AsReadOnly()
            };
        }
        catch (EndOfStreamException ex)
        {
            throw new BlocksInfoParseException("Truncated BlocksInfo: unexpected end of data", ex);
        }
        catch (BlocksInfoParseException)
        {
            throw;
        }
        catch (Exception ex) when (ex is StringReadException or Utf8DecodingException or StreamBoundsException or AlignmentValidationException)
        {
            throw new BlocksInfoParseException($"Failed to parse BlocksInfo: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates nodes for uniqueness, bounds, and overlaps.
    /// </summary>
    private static void ValidateNodes(BlocksInfo blocksInfo, long dataOffset)
    {
        // Calculate total uncompressed data span
        long totalUncompressedSpan = 0;
        foreach (var block in blocksInfo.Blocks)
        {
            // Check for overflow
            if (totalUncompressedSpan > long.MaxValue - block.UncompressedSize)
            {
                throw new BlocksInfoParseException("Total uncompressed size overflow");
            }
            totalUncompressedSpan += block.UncompressedSize;
        }

        // Track unique paths
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in blocksInfo.Nodes)
        {
            // Validate offset and size
            if (node.Offset < 0)
            {
                throw new BlocksInfoParseException($"Node '{node.Path}' has negative offset: {node.Offset}");
            }

            if (node.Size < 0)
            {
                throw new BlocksInfoParseException($"Node '{node.Path}' has negative size: {node.Size}");
            }

            // Check for overflow in offset + size calculation
            if (node.Offset > long.MaxValue - node.Size)
            {
                throw new BlocksInfoParseException($"Node '{node.Path}' offset+size overflow: offset={node.Offset}, size={node.Size}");
            }

            // Validate bounds
            if (node.Offset + node.Size > totalUncompressedSpan)
            {
                throw new BlocksInfoParseException(
                    $"Node '{node.Path}' exceeds data region bounds: offset={node.Offset}, size={node.Size}, total={totalUncompressedSpan}");
            }

            // Check path uniqueness
            if (!seenPaths.Add(node.Path))
            {
                throw new DuplicateNodeException($"Duplicate node path detected: '{node.Path}'", node.Path);
            }
        }

        // Check for overlapping nodes (optional but recommended per spec)
        if (blocksInfo.Nodes.Count > 1)
        {
            var sortedNodes = blocksInfo.Nodes.OrderBy(n => n.Offset).ToList();
            for (int i = 0; i < sortedNodes.Count - 1; i++)
            {
                var current = sortedNodes[i];
                var next = sortedNodes[i + 1];

                if (current.Offset + current.Size > next.Offset)
                {
                    throw new BlocksInfoParseException(
                        $"Overlapping nodes detected: '{current.Path}' (offset={current.Offset}, size={current.Size}) " +
                        $"overlaps with '{next.Path}' (offset={next.Offset})");
                }
            }
        }
    }
}
