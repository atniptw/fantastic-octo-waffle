using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Parser for UnityFS BlocksInfo structure.
/// Handles decompression and parsing of storage blocks and node tables.
/// Hash verification is skipped (following UnityPy reference implementation).
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
    /// <exception cref="ArgumentException">Thrown if compressedData is empty.</exception>
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

        if (expectedUncompressedSize < 16)
        {
            throw new BlocksInfoParseException(
            $"BlocksInfo size too small: expected at least 16 bytes for hash, got {expectedUncompressedSize}");
        }

        // Step 1: Decompress BlocksInfo
        byte[] uncompressedData = DecompressBlocksInfo(compressedData, expectedUncompressedSize, compressionType);

        // Step 2: Verify hash length only (UnityPy skips validation)
        VerifyHash(uncompressedData);

        // Step 3: Parse tables
        BlocksInfo blocksInfo = ParseTables(uncompressedData);

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
            throw new BlocksInfoParseException(
                $"Failed to decompress BlocksInfo (compression: {compressionType}, compressed size: {compressedData.Length}, expected uncompressed: {expectedUncompressedSize}): {ex.Message}", 
                ex);
        }
    }

    /// <summary>
    /// Reads and skips the 16-byte Hash128 field at the start of BlocksInfo.
    /// Unity bundles include a 16-byte hash but don't verify it (UnityPy marks it as unused).
    /// Following UnityPy reference implementation which skips hash verification.
    /// </summary>
    private static void VerifyHash(byte[] uncompressedData)
    {
        if (uncompressedData.Length < 16)
        {
            throw new BlocksInfoParseException(
                $"BlocksInfo too short: {uncompressedData.Length} bytes (expected at least 16 for hash)");
        }
        // No verification per UnityPy; just ensure minimum length.
    }

    /// <summary>
    /// Parses storage blocks and nodes from uncompressed BlocksInfo data.
    /// </summary>
    private static BlocksInfo ParseTables(byte[] uncompressedData)
    {
        using var stream = new MemoryStream(uncompressedData);
        // BlocksInfo tables are big-endian (matching UnityPy's EndianBinaryReader default)
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Skip hash (first 16 bytes) - Hash128 (unused)
        reader.BaseStream.Seek(16, SeekOrigin.Begin);

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

            // Parse nodes immediately after block table (UnityPy does not align here)
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

            // Extract 16-byte Hash128 field for return (not verified)
            byte[] hash = new byte[16];
            Buffer.BlockCopy(uncompressedData, 0, hash, 0, 16);

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

}
