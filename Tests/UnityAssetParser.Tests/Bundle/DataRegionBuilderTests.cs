using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for DataRegionBuilder class.
/// </summary>
public class DataRegionBuilderTests
{
    private readonly DataRegionBuilder _builder;

    public DataRegionBuilderTests()
    {
        _builder = new DataRegionBuilder(new Decompressor());
    }

    [Fact]
    public void Build_SingleUncompressedBlock_CreatesDataRegion()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        using var stream = new MemoryStream();
        stream.Write(data);
        stream.Position = 0;

        var blocks = new[] { DataRegionTestFixtures.UncompressedBlock(10) };

        // Act
        var region = _builder.Build(stream, 0, blocks);

        // Assert
        Assert.Equal(10, region.Length);
        var slice = region.ReadSlice(0, 10);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void Build_MultipleUncompressedBlocks_ConcatenatesCorrectly()
    {
        // Arrange
        var data1 = DataRegionTestFixtures.HelloWorldBytes;
        var data2 = DataRegionTestFixtures.Test123Bytes;
        
        using var stream = new MemoryStream();
        stream.Write(data1);
        stream.Write(data2);
        stream.Position = 0;

        var blocks = new[]
        {
            DataRegionTestFixtures.UncompressedBlock(10),
            DataRegionTestFixtures.UncompressedBlock(7)
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);

        // Assert
        Assert.Equal(17, region.Length);
        var slice1 = region.ReadSlice(0, 10);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice1.Span));
        var slice2 = region.ReadSlice(10, 7);
        Assert.Equal("Test123", Encoding.UTF8.GetString(slice2.Span));
    }

    [Fact]
    public void Build_WithDataOffset_SeeksCorrectly()
    {
        // Arrange
        var padding = new byte[100];
        var data = DataRegionTestFixtures.HelloWorldBytes;
        
        using var stream = new MemoryStream();
        stream.Write(padding);  // Write 100 bytes of padding
        stream.Write(data);     // Write data at offset 100
        stream.Position = 0;

        var blocks = new[] { DataRegionTestFixtures.UncompressedBlock(10) };

        // Act
        var region = _builder.Build(stream, 100, blocks);

        // Assert
        Assert.Equal(10, region.Length);
        var slice = region.ReadSlice(0, 10);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void Build_Lz4CompressedBlock_DecompressesCorrectly()
    {
        // Arrange
        var compressedData = Helpers.DecompressionTestFixtures.Lz4HelloWorld;
        
        using var stream = new MemoryStream();
        stream.Write(compressedData);
        stream.Position = 0;

        var blocks = new[]
        {
            DataRegionTestFixtures.Lz4Block(10, (uint)compressedData.Length)
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);

        // Assert
        Assert.Equal(10, region.Length);
        var slice = region.ReadSlice(0, 10);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void Build_EmptyBlocksList_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var blocks = Array.Empty<StorageBlock>();

        // Act & Assert
        var ex = Assert.Throws<BlocksInfoParseException>(() =>
            _builder.Build(stream, 0, blocks));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_BlockWithReservedBits_ThrowsBlockFlagsException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[10]);
        var blocks = new[] { DataRegionTestFixtures.InvalidReservedBitsBlock };

        // Act & Assert
        var ex = Assert.Throws<BlockFlagsException>(() =>
            _builder.Build(stream, 0, blocks));
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0xFF80", ex.Message);
    }

    [Fact]
    public void Build_TruncatedStream_ThrowsBlockDecompressionFailedException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[5]); // Only 5 bytes, but block expects 10
        var blocks = new[] { DataRegionTestFixtures.UncompressedBlock(10) };

        // Act & Assert
        var ex = Assert.Throws<BlockDecompressionFailedException>(() =>
            _builder.Build(stream, 0, blocks));
        Assert.Contains("failed to read", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_DecompressionSizeMismatch_ThrowsException()
    {
        // Arrange
        var compressedData = Helpers.DecompressionTestFixtures.Lz4HelloWorld;
        
        using var stream = new MemoryStream();
        stream.Write(compressedData);
        stream.Position = 0;

        // Create block with wrong uncompressed size
        var blocks = new[]
        {
            DataRegionTestFixtures.Lz4Block(5, (uint)compressedData.Length)  // Wrong size: 5 instead of 10
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => _builder.Build(stream, 0, blocks));
    }

    [Fact]
    public void Build_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var blocks = new[] { DataRegionTestFixtures.UncompressedBlock(10) };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _builder.Build(null!, 0, blocks));
        Assert.Throws<ArgumentNullException>(() => _builder.Build(stream, 0, null!));
    }

    [Fact]
    public void Build_StreamedBlock_ProcessesCorrectly()
    {
        // Arrange - block with streamed flag set (bit 6)
        var data = DataRegionTestFixtures.HelloWorldBytes;
        using var stream = new MemoryStream();
        stream.Write(data);
        stream.Position = 0;

        var block = new StorageBlock
        {
            UncompressedSize = 10,
            CompressedSize = 10,
            Flags = 0x40  // Streamed flag set, no compression
        };

        // Act
        var region = _builder.Build(stream, 0, new[] { block });

        // Assert - should still process correctly (streamed flag is informational)
        Assert.Equal(10, region.Length);
        var slice = region.ReadSlice(0, 10);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice.Span));
    }
}
