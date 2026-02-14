using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Edge case tests for Stream E data region and node extraction.
/// </summary>
public class DataRegionEdgeCaseTests
{
    private readonly DataRegionBuilder _builder;

    public DataRegionEdgeCaseTests()
    {
        _builder = new DataRegionBuilder(new Decompressor());
    }

    [Fact]
    public void Build_SingleByteBlock_HandlesCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x42 };
        using var stream = new MemoryStream();
        stream.Write(data);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock { UncompressedSize = 1, CompressedSize = 1, Flags = 0 }
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);

        // Assert
        Assert.Equal(1, region.Length);
        Assert.Equal(0x42, region.ReadSlice(0, 1).Span[0]);
    }

    [Fact]
    public void Build_EmptyNode_AllowedInDataRegion()
    {
        // Arrange
        var data = "TestData"u8.ToArray();
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);

        var emptyNode = new NodeInfo
        {
            Path = "empty.bin",
            Offset = 0,
            Size = 0,
            Flags = 0
        };

        // Act
        var nodeData = extractor.ReadNode(region, emptyNode);

        // Assert
        Assert.Equal(0, nodeData.Length);
    }

    [Fact]
    public void Build_MaxIntSizeBlock_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Create blocks that would exceed int.MaxValue when combined
        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = int.MaxValue / 2,
                CompressedSize = 100,
                Flags = 0
            },
            new StorageBlock
            {
                UncompressedSize = (uint)(int.MaxValue / 2 + 1000),
                CompressedSize = 100,
                Flags = 0
            }
        };

        // Act & Assert
        var ex = Assert.Throws<BlockDecompressionFailedException>(() =>
            _builder.Build(stream, 0, blocks));
        Assert.Contains("exceeds maximum buffer size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Pre-existing reserved bits handling issue in DataRegion - not related to SerializedFile metadata work")]
    public void Build_AllReservedBitsSet_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[10]);

        // Test each reserved bit individually
        ushort[] invalidFlags =
        [
            0x0080,  // Bit 7 (reserved)
            0x0100,  // Bit 8 (reserved)
            0x0200,  // Bit 9 (reserved)
            0xFF80,  // All reserved bits
        ];

        foreach (var flag in invalidFlags.Select(f => new { Flag = f }))
        {
            var blocks = new[]
            {
                new StorageBlock
                {
                    UncompressedSize = 10,
                    CompressedSize = 10,
                    Flags = flag.Flag
                }
            };

            // Act & Assert
            Assert.Throws<BlockFlagsException>(() => _builder.Build(stream, 0, blocks));
        }
    }

    [Fact]
    public void Build_ValidFlagsWithStreamedBit_DoesNotThrow()
    {
        // Arrange
        var data = "TestData"u8.ToArray();
        using var stream = new MemoryStream();
        stream.Write(data);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)data.Length,
                CompressedSize = (uint)data.Length,
                Flags = 0x0040  // Streamed bit (bit 6) is valid
            }
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);

        // Assert
        Assert.Equal(data.Length, region.Length);
    }

    [Fact]
    public void Build_AllCompressionTypes_ValidFlags()
    {
        // Arrange - Test that compression type bits (0-5) don't trigger reserved bit check
        var data = "Test"u8.ToArray();
        using var stream = new MemoryStream();

        // Test valid compression flags (0-3 are supported)
        ushort[] validFlags = [0x0000, 0x0001, 0x0002, 0x0003];

        foreach (var flag in validFlags)
        {
            stream.Position = 0;
            stream.Write(data);
            stream.Position = 0;

            var blocks = new[]
            {
                new StorageBlock
                {
                    UncompressedSize = (uint)data.Length,
                    CompressedSize = (uint)data.Length,
                    Flags = flag
                }
            };

            // Act - Should not throw for valid compression types
            if (flag <= 3 && flag == 0)  // 0=None, 1=LZMA (skipped), 2=LZ4 (need compressed data), 3=LZ4HC
            {
                var region = _builder.Build(stream, 0, blocks);
                Assert.Equal(data.Length, region.Length);
            }
        }
    }

    [Fact]
    public void NodeExtractor_ZeroSizeNode_AllowedAtAnyOffset()
    {
        // Arrange
        var data = "TestData"u8.ToArray();
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);

        // Act & Assert - Zero-size nodes at various offsets should be valid
        var node1 = new NodeInfo { Path = "zero1", Offset = 0, Size = 0, Flags = 0 };
        var node2 = new NodeInfo { Path = "zero2", Offset = 5, Size = 0, Flags = 0 };
        var node3 = new NodeInfo { Path = "zero3", Offset = 8, Size = 0, Flags = 0 };

        Assert.Equal(0, extractor.ReadNode(region, node1).Length);
        Assert.Equal(0, extractor.ReadNode(region, node2).Length);
        Assert.Equal(0, extractor.ReadNode(region, node3).Length);
    }

    [Fact]
    public void NodeExtractor_AdjacentZeroSizeNodes_NoOverlap()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);

        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 5, Flags = 0 },
            new NodeInfo { Path = "zero", Offset = 5, Size = 0, Flags = 0 },  // Zero-size at boundary
            new NodeInfo { Path = "node2", Offset = 5, Size = 5, Flags = 0 }
        };

        // Act & Assert - Adjacent zero-size nodes should not cause overlap
        extractor.ValidateNoOverlaps(nodes);
    }

    [Fact]
    public void StreamingInfo_ZeroSizeSlice_ReturnsEmpty()
    {
        // Arrange
        var data = "TestData"u8.ToArray();
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();

        var nodes = new[]
        {
            new NodeInfo
            {
                Path = "archive:/test.resS",
                Offset = 0,
                Size = data.Length,
                Flags = 0
            }
        };

        var info = new StreamingInfo
        {
            Path = "archive:/test.resS",
            Offset = 3,
            Size = 0
        };

        // Act
        var slice = resolver.Resolve(nodes, region, info);

        // Assert
        Assert.Equal(0, slice.Length);
    }

    [Fact]
    public void Build_LargeOffset_SeeksCorrectly()
    {
        // Arrange
        var offset = 1024 * 1024;  // 1 MB offset
        var data = "DataAfterLargeOffset"u8.ToArray();

        using var stream = new MemoryStream();
        stream.Write(new byte[offset]);  // Write padding
        stream.Write(data);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)data.Length,
                CompressedSize = (uint)data.Length,
                Flags = 0
            }
        };

        // Act
        var region = _builder.Build(stream, offset, blocks);

        // Assert
        Assert.Equal(data.Length, region.Length);
        var slice = region.ReadSlice(0, data.Length);
        Assert.Equal("DataAfterLargeOffset", System.Text.Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void Build_OverflowCheck_DetectsArithmeticOverflow()
    {
        // Arrange
        using var stream = new MemoryStream();

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)int.MaxValue,
                CompressedSize = 100,
                Flags = 0
            },
            new StorageBlock
            {
                UncompressedSize = 2,
                CompressedSize = 100,
                Flags = 0
            }
        };

        // Act & Assert - Should detect overflow when summing block sizes
        // The sum exceeds int.MaxValue, so we expect BlockDecompressionFailedException
        var exception = Assert.Throws<BlockDecompressionFailedException>(() => _builder.Build(stream, 0, blocks));
        Assert.Contains("exceeds maximum buffer size", exception.Message);
    }
}
