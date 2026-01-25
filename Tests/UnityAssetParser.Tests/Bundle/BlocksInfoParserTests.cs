using System.Security.Cryptography;
using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for BlocksInfoParser.
/// Tests parsing, hash verification, and validation of BlocksInfo structures.
/// </summary>
public class BlocksInfoParserTests
{
    private readonly IDecompressor _decompressor;
    private readonly BlocksInfoParser _parser;

    public BlocksInfoParserTests()
    {
        _decompressor = new Decompressor();
        _parser = new BlocksInfoParser(_decompressor);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a valid uncompressed BlocksInfo blob with given blocks and nodes.
    /// </summary>
    private static byte[] CreateBlocksInfoBlob(List<(uint uncompSize, uint compSize, ushort flags)> blocks, List<(long offset, long size, int flags, string path)> nodes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Reserve 20 bytes for hash (will be filled later)
        writer.Write(new byte[20]);

        // Write block count
        writer.Write(blocks.Count);

        // Write blocks
        foreach (var (uncompSize, compSize, flags) in blocks)
        {
            writer.Write(uncompSize);
            writer.Write(compSize);
            writer.Write(flags);
        }

        // Align to 4-byte boundary
        long position = stream.Position;
        int padding = (int)((4 - (position % 4)) % 4);
        for (int i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }

        // Write node count
        writer.Write(nodes.Count);

        // Write nodes
        foreach (var (offset, size, flags, path) in nodes)
        {
            writer.Write(offset);
            writer.Write(size);
            writer.Write(flags);
            writer.Write(Encoding.UTF8.GetBytes(path));
            writer.Write((byte)0); // null terminator
        }

        // Compute hash over payload (bytes 20..end)
        byte[] fullData = stream.ToArray();
        byte[] payload = new byte[fullData.Length - 20];
        Array.Copy(fullData, 20, payload, 0, payload.Length);
        byte[] hash = SHA1.HashData(payload);

        // Write hash at the beginning
        Array.Copy(hash, 0, fullData, 0, 20);

        return fullData;
    }

    /// <summary>
    /// Corrupts the hash in a BlocksInfo blob.
    /// </summary>
    private static byte[] CorruptHash(byte[] validBlob)
    {
        byte[] corrupted = (byte[])validBlob.Clone();
        corrupted[0] ^= 0xFF; // Flip bits in first hash byte
        return corrupted;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public void Parse_ValidUncompressedBlocksInfo_SingleBlockSingleNode_ParsesCorrectly()
    {
        // Arrange
        var blocks = new List<(uint, uint, ushort)>
        {
            (1024, 1024, 0) // uncompressed block
        };

        var nodes = new List<(long, long, int, string)>
        {
            (0, 512, 0, "CAB-test123/test.resS")
        };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act
        var result = _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 1000);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(20, result.UncompressedDataHash.Length);
        Assert.Single(result.Blocks);
        Assert.Single(result.Nodes);

        var block = result.Blocks[0];
        Assert.Equal(1024u, block.UncompressedSize);
        Assert.Equal(1024u, block.CompressedSize);
        Assert.Equal((ushort)0, block.Flags);
        Assert.Equal(CompressionType.None, block.CompressionType);

        var node = result.Nodes[0];
        Assert.Equal(0, node.Offset);
        Assert.Equal(512, node.Size);
        Assert.Equal(0, node.Flags);
        Assert.Equal("CAB-test123/test.resS", node.Path);
    }

    [Fact]
    public void Parse_MultiBlockMultiNode_WithAlignment_ParsesCorrectly()
    {
        // Arrange: 3 blocks, 3 nodes
        var blocks = new List<(uint, uint, ushort)>
        {
            (2048, 2048, 0),  // Block 0: uncompressed
            (4096, 1024, 2),  // Block 1: LZ4 compressed
            (1024, 512, 1)    // Block 2: LZMA compressed
        };

        var nodes = new List<(long, long, int, string)>
        {
            (0, 1024, 0, "node1.data"),
            (1024, 2048, 0, "node2.data"),
            (3072, 512, 0, "node3.resS")
        };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act
        var result = _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 2000);

        // Assert
        Assert.Equal(3, result.Blocks.Count);
        Assert.Equal(3, result.Nodes.Count);

        // Verify block details
        Assert.Equal(2048u, result.Blocks[0].UncompressedSize);
        Assert.Equal(4096u, result.Blocks[1].UncompressedSize);
        Assert.Equal(1024u, result.Blocks[2].UncompressedSize);

        // Verify total uncompressed size
        Assert.Equal(2048 + 4096 + 1024, result.TotalUncompressedDataSize);

        // Verify node paths are unique and correct
        Assert.Equal("node1.data", result.Nodes[0].Path);
        Assert.Equal("node2.data", result.Nodes[1].Path);
        Assert.Equal("node3.resS", result.Nodes[2].Path);
    }

    [Fact]
    public void Parse_EmptyBlocks_EmptyNodes_ParsesCorrectly()
    {
        // Arrange: no blocks, no nodes
        var blocks = new List<(uint, uint, ushort)>();
        var nodes = new List<(long, long, int, string)>();

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act
        var result = _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0);

        // Assert
        Assert.Empty(result.Blocks);
        Assert.Empty(result.Nodes);
        Assert.Equal(0, result.TotalUncompressedDataSize);
    }

    #endregion

    #region Hash Verification Tests

    [Fact]
    public void Parse_CorruptedHash_ThrowsHashMismatchException()
    {
        // Arrange
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) };
        var nodes = new List<(long, long, int, string)> { (0, 512, 0, "test.data") };

        byte[] validBlob = CreateBlocksInfoBlob(blocks, nodes);
        byte[] corruptedBlob = CorruptHash(validBlob);

        // Act & Assert
        var ex = Assert.Throws<HashMismatchException>(() =>
            _parser.Parse(corruptedBlob, corruptedBlob.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("hash mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.ExpectedHash);
        Assert.NotNull(ex.ComputedHash);
        Assert.NotEqual(ex.ExpectedHash, ex.ComputedHash);
    }

    [Fact]
    public void Parse_TooSmallForHash_ThrowsBlocksInfoParseException()
    {
        // Arrange: only 10 bytes (less than 20 for hash)
        byte[] tooSmall = new byte[10];

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(tooSmall, tooSmall.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("too small", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hash", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Duplicate Node Tests

    [Fact]
    public void Parse_DuplicateNodePaths_ThrowsDuplicateNodeException()
    {
        // Arrange: two nodes with the same path
        var blocks = new List<(uint, uint, ushort)> { (2048, 2048, 0) };
        var nodes = new List<(long, long, int, string)>
        {
            (0, 512, 0, "duplicate.data"),
            (512, 512, 0, "duplicate.data") // duplicate path
        };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert
        var ex = Assert.Throws<DuplicateNodeException>(() =>
            _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("duplicate.data", ex.DuplicatePath);
    }

    #endregion

    #region Node Validation Tests

    [Fact]
    public void Parse_NegativeNodeOffset_ThrowsBlocksInfoParseException()
    {
        // Arrange: node with negative offset
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) };
        var nodes = new List<(long, long, int, string)> { (-100, 512, 0, "test.data") };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("negative offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NegativeNodeSize_ThrowsBlocksInfoParseException()
    {
        // Arrange: node with negative size
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) };
        var nodes = new List<(long, long, int, string)> { (0, -512, 0, "test.data") };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("negative size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NodeExceedsBounds_ThrowsBlocksInfoParseException()
    {
        // Arrange: node extends beyond total uncompressed size
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) }; // total: 1024 bytes
        var nodes = new List<(long, long, int, string)> { (512, 1024, 0, "test.data") }; // 512 + 1024 = 1536 > 1024

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("exceeds data region bounds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OverlappingNodes_ThrowsBlocksInfoParseException()
    {
        // Arrange: nodes that overlap
        var blocks = new List<(uint, uint, ushort)> { (2048, 2048, 0) };
        var nodes = new List<(long, long, int, string)>
        {
            (0, 1024, 0, "node1.data"),  // 0-1024
            (512, 1024, 0, "node2.data") // 512-1536 (overlaps with node1)
        };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("overlapping", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Compression Tests

    [Fact(Skip = "LZMA compression test requires valid LZMA-compressed BlocksInfo fixture")]
    public void Parse_LzmaCompressedBlocksInfo_DecompressesAndParses()
    {
        // This test requires creating actual LZMA-compressed BlocksInfo data
        // Skipped for now; can be implemented with proper test fixtures
        Assert.True(true);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyCompressedData_ThrowsArgumentException()
    {
        // Arrange
        byte[] empty = Array.Empty<byte>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _parser.Parse(empty, 100, CompressionType.None, dataOffset: 0));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NegativeBlockCount_ThrowsBlocksInfoParseException()
    {
        // Arrange: manually craft blob with negative block count
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write hash placeholder
        writer.Write(new byte[20]);

        // Write negative block count
        writer.Write(-5);

        byte[] blob = stream.ToArray();

        // Compute and set hash
        byte[] payload = new byte[blob.Length - 20];
        Array.Copy(blob, 20, payload, 0, payload.Length);
        byte[] hash = SHA1.HashData(payload);
        Array.Copy(hash, 0, blob, 0, 20);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blob, blob.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("invalid block count", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NegativeNodeCount_ThrowsBlocksInfoParseException()
    {
        // Arrange: manually craft blob with negative node count
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write hash placeholder
        writer.Write(new byte[20]);

        // Write block count (0 blocks)
        writer.Write(0);

        // Align to 4-byte boundary (already aligned)
        
        // Write negative node count
        writer.Write(-3);

        byte[] blob = stream.ToArray();

        // Compute and set hash
        byte[] payload = new byte[blob.Length - 20];
        Array.Copy(blob, 20, payload, 0, payload.Length);
        byte[] hash = SHA1.HashData(payload);
        Array.Copy(hash, 0, blob, 0, 20);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blob, blob.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("invalid node count", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TruncatedBlocksInfo_ThrowsBlocksInfoParseException()
    {
        // Arrange: create valid blob but truncate it AFTER hash but DURING table parsing
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) };
        var nodes = new List<(long, long, int, string)> { (0, 512, 0, "test.data") };

        byte[] fullBlob = CreateBlocksInfoBlob(blocks, nodes);
        
        // Truncate after hash (20 bytes) and block count (4 bytes) but before all blocks are read
        // Hash: 20 bytes
        // Block count: 4 bytes
        // Single block: 10 bytes (uint32 + uint32 + uint16)
        // We want to truncate in the middle of the block data
        byte[] truncated = new byte[20 + 4 + 5]; // Only 5 bytes of the 10-byte block
        Array.Copy(fullBlob, 0, truncated, 0, truncated.Length);

        // Recompute hash for the truncated payload
        byte[] payload = new byte[truncated.Length - 20];
        Array.Copy(truncated, 20, payload, 0, payload.Length);
        byte[] hash = SHA1.HashData(payload);
        Array.Copy(hash, 0, truncated, 0, 20);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(truncated, truncated.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NonZeroPaddingBytes_ThrowsBlocksInfoParseException()
    {
        // Arrange: manually craft blob with non-zero padding
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write hash placeholder
        writer.Write(new byte[20]);

        // Write 2 blocks to ensure padding is needed (total: 20 bytes)
        writer.Write((uint)1024); // block 0 uncompressed size
        writer.Write((uint)1024); // block 0 compressed size  
        writer.Write((ushort)0);  // block 0 flags
        writer.Write((uint)2048); // block 1 uncompressed size
        writer.Write((uint)2048); // block 1 compressed size
        writer.Write((ushort)0);  // block 1 flags
        // Current position: 20 (hash) + 4 (count) + 20 (2 blocks) = 44
        // Position 44 is already 4-byte aligned, so NO padding needed!
        
        // Let's make it so we need padding by writing an odd number of blocks
        // Actually, let me use a different approach - write 1 block

        stream.SetLength(0); // Reset
        stream.Position = 0;

        // Write hash placeholder
        writer.Write(new byte[20]);

        // Write block count
        writer.Write(1);

        // Write single block (10 bytes: uint32 + uint32 + uint16)
        writer.Write((uint)1024);
        writer.Write((uint)1024);
        writer.Write((ushort)0);
        // Current position: 20 + 4 + 10 = 34
        // Need 2 bytes padding to reach 36 (next 4-byte boundary)
        
        // Write NON-ZERO padding (should fail validation)
        writer.Write((byte)0xFF);
        writer.Write((byte)0xFF);
        // Now at position 36

        // Write node count
        writer.Write(0); // No nodes to avoid needing to write valid node data
        // Position: 40

        byte[] blob = stream.ToArray();

        // Compute and set hash
        byte[] payload = new byte[blob.Length - 20];
        Array.Copy(blob, 20, payload, 0, payload.Length);
        byte[] hash = SHA1.HashData(payload);
        Array.Copy(hash, 0, blob, 0, 20);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(blob, blob.Length, CompressionType.None, dataOffset: 0));

        // The inner exception should be AlignmentValidationException with non-zero padding message
        Assert.NotNull(ex.InnerException);
        Assert.IsType<AlignmentValidationException>(ex.InnerException);
        Assert.Contains("non-zero padding", ex.InnerException.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Decompression Size Mismatch Tests

    [Fact]
    public void Parse_DecompressionSizeMismatch_ThrowsDecompressionSizeMismatchException()
    {
        // Arrange: provide compressed data with wrong expected size
        var blocks = new List<(uint, uint, ushort)> { (1024, 1024, 0) };
        var nodes = new List<(long, long, int, string)> { (0, 512, 0, "test.data") };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act & Assert: expect 1000 bytes but actual is blocksInfoData.Length
        var ex = Assert.Throws<DecompressionSizeMismatchException>(() =>
            _parser.Parse(blocksInfoData, 1000, CompressionType.None, dataOffset: 0));

        Assert.Contains("size mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
