using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for BlocksInfoParser.
/// Tests parsing and validation of BlocksInfo structures.
/// Hash verification is skipped per UnityPy behavior.
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
    /// Uses 16-byte Hash128 (zeroed) per UnityPy behavior.
    /// Writes fields in big-endian order to match UnityPy's EndianBinaryReader defaults.
    /// </summary>
    private static byte[] CreateBlocksInfoBlob(List<(uint uncompSize, uint compSize, ushort flags)> blocks, List<(long offset, long size, int flags, string path)> nodes)
    {
        // BlocksInfo must be big-endian (matching UnityPy's EndianBinaryReader)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write 16-byte Hash128 (zeroed, not verified per UnityPy)
        writer.Write(new byte[16]);

        // Write block count (big-endian int32)
        WriteInt32BE(writer, blocks.Count);

        // Write blocks (all big-endian)
        foreach (var (uncompSize, compSize, flags) in blocks)
        {
            WriteUInt32BE(writer, uncompSize);
            WriteUInt32BE(writer, compSize);
            WriteUInt16BE(writer, flags);
        }

        // Write node count (big-endian int32)
        WriteInt32BE(writer, nodes.Count);

        // Write nodes (all big-endian)
        foreach (var (offset, size, flags, path) in nodes)
        {
            WriteInt64BE(writer, offset);
            WriteInt64BE(writer, size);
            WriteInt32BE(writer, flags);
            writer.Write(Encoding.UTF8.GetBytes(path));
            writer.Write((byte)0); // null terminator
        }

        return stream.ToArray();
    }

    private static void WriteInt32BE(BinaryWriter writer, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    private static void WriteUInt32BE(BinaryWriter writer, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    private static void WriteUInt16BE(BinaryWriter writer, ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    private static void WriteInt64BE(BinaryWriter writer, long value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
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
        Assert.Equal(16, result.UncompressedDataHash.Length);
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

    #region Hash Field Tests

    [Fact]
    public void Parse_TooSmallForHash_ThrowsBlocksInfoParseException()
    {
        // Arrange: only 10 bytes (less than 16 for Hash128)
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
    public void Parse_DuplicateNodePaths_ParsesSuccessfully()
    {
        // Arrange: two nodes with the same path
        // Per UnityPy behavior, duplicate node paths are allowed and parsed without exception
        var blocks = new List<(uint, uint, ushort)> { (2048, 2048, 0) };
        var nodes = new List<(long, long, int, string)>
        {
            (0, 512, 0, "duplicate.data"),
            (512, 512, 0, "duplicate.data") // duplicate path is permitted
        };

        byte[] blocksInfoData = CreateBlocksInfoBlob(blocks, nodes);

        // Act
        var result = _parser.Parse(blocksInfoData, blocksInfoData.Length, CompressionType.None, dataOffset: 0);

        // Assert: both nodes are parsed successfully
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal("duplicate.data", result.Nodes[0].Path);
        Assert.Equal("duplicate.data", result.Nodes[1].Path);
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
    public void Parse_TruncatedBlockData_ThrowsBlocksInfoParseException()
    {
        // Arrange
        var blocks = new List<(uint uncompSize, uint compSize, ushort flags)> { (512, 512, 0x3f) };
        var nodes = new List<(long offset, long size, int flags, string path)> { (0, 512, 0, "test.data") };

        byte[] fullBlob = CreateBlocksInfoBlob(blocks, nodes);
        
        // Truncate after hash (16 bytes) and block count (4 bytes) but before all blocks are read
        // Hash: 16 bytes
        // Block count: 4 bytes
        // Single block: 10 bytes (uint32 + uint32 + uint16)
        // We want to truncate in the middle of the block data
        byte[] truncated = new byte[16 + 4 + 5]; // Only 5 bytes of the 10-byte block
        Array.Copy(fullBlob, 0, truncated, 0, truncated.Length);

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _parser.Parse(truncated, truncated.Length, CompressionType.None, dataOffset: 0));

        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Non-zero padding validation not implemented in UnityPy")]
    public void Parse_NonZeroPaddingBytes_ThrowsBlocksInfoParseException()
    {
        // Arrange: manually craft blob with non-zero padding
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write 16-byte Hash128
        writer.Write(new byte[16]);

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

        // Write 16-byte Hash128
        writer.Write(new byte[16]);

        // Write block count
        writer.Write(1);

        // Write single block (10 bytes: uint32 + uint32 + uint16)
        writer.Write((uint)1024);
        writer.Write((uint)1024);
        writer.Write((ushort)0);
        // Current position: 16 + 4 + 10 = 30
        // Need 2 bytes padding to reach 32 (next 4-byte boundary)
        
        // Write NON-ZERO padding (should fail validation)
        writer.Write((byte)0xFF);
        writer.Write((byte)0xFF);
        // Now at position 32

        // Write node count
        writer.Write(0); // No nodes to avoid needing to write valid node data
        // Position: 36

        byte[] blob = stream.ToArray();

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
