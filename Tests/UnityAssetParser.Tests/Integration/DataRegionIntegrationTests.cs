using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// Integration tests for Stream E data region decompression and node extraction.
/// Tests the complete pipeline from storage blocks to node extraction and StreamingInfo resolution.
/// </summary>
public class DataRegionIntegrationTests
{
    private readonly DataRegionBuilder _builder;
    private readonly NodeExtractor _extractor;
    private readonly StreamingInfoResolver _resolver;

    public DataRegionIntegrationTests()
    {
        _builder = new DataRegionBuilder(new Decompressor());
        _extractor = new NodeExtractor(detectOverlaps: true);
        _resolver = new StreamingInfoResolver();
    }

    [Fact]
    public void EndToEnd_SingleUncompressedBlock_SingleNode()
    {
        // Arrange - Create a simple bundle with one uncompressed block
        var data = "HelloWorld_TestData_12345"u8.ToArray();
        using var stream = new MemoryStream();
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

        var nodes = new[]
        {
            new NodeInfo
            {
                Path = "archive:/CAB-test/file.bin",
                Offset = 0,
                Size = data.Length,
                Flags = 0
            }
        };

        // Act - Build data region and extract node
        var region = _builder.Build(stream, 0, blocks);
        var nodeData = _extractor.ReadNode(region, nodes[0]);

        // Assert
        Assert.Equal(data.Length, region.Length);
        Assert.Equal(data.Length, nodeData.Length);
        Assert.Equal("HelloWorld_TestData_12345", Encoding.UTF8.GetString(nodeData.Span));
    }

    [Fact]
    public void EndToEnd_MultipleBlocks_MultipleNodes()
    {
        // Arrange - Create bundle with 3 blocks, 3 nodes
        var block1Data = "Block1Data"u8.ToArray();  // 10 bytes
        var block2Data = "Block2"u8.ToArray();      // 6 bytes
        var block3Data = "Block3Data"u8.ToArray();  // 10 bytes
        
        using var stream = new MemoryStream();
        stream.Write(block1Data);
        stream.Write(block2Data);
        stream.Write(block3Data);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock { UncompressedSize = 10, CompressedSize = 10, Flags = 0 },
            new StorageBlock { UncompressedSize = 6, CompressedSize = 6, Flags = 0 },
            new StorageBlock { UncompressedSize = 10, CompressedSize = 10, Flags = 0 }
        };

        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 10, Flags = 0 },   // Block 1
            new NodeInfo { Path = "node2", Offset = 10, Size = 6, Flags = 0 },   // Block 2
            new NodeInfo { Path = "node3", Offset = 16, Size = 10, Flags = 0 }   // Block 3
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        _extractor.ValidateNoOverlaps(nodes);
        var node1Data = _extractor.ReadNode(region, nodes[0]);
        var node2Data = _extractor.ReadNode(region, nodes[1]);
        var node3Data = _extractor.ReadNode(region, nodes[2]);

        // Assert
        Assert.Equal(26, region.Length);
        Assert.Equal("Block1Data", Encoding.UTF8.GetString(node1Data.Span));
        Assert.Equal("Block2", Encoding.UTF8.GetString(node2Data.Span));
        Assert.Equal("Block3Data", Encoding.UTF8.GetString(node3Data.Span));
    }

    [Fact]
    public void EndToEnd_NodeSpanningMultipleBlocks()
    {
        // Arrange - Create a node that spans two blocks
        var block1Data = "FirstBlock"u8.ToArray();   // 10 bytes
        var block2Data = "SecondBlock"u8.ToArray();  // 11 bytes
        
        using var stream = new MemoryStream();
        stream.Write(block1Data);
        stream.Write(block2Data);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock { UncompressedSize = 10, CompressedSize = 10, Flags = 0 },
            new StorageBlock { UncompressedSize = 11, CompressedSize = 11, Flags = 0 }
        };

        // Node spans from byte 5 of block1 through byte 5 of block2
        var node = new NodeInfo
        {
            Path = "spanning-node",
            Offset = 5,
            Size = 11,  // "BlockSecond"
            Flags = 0
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        var nodeData = _extractor.ReadNode(region, node);

        // Assert
        Assert.Equal(21, region.Length);
        Assert.Equal(11, nodeData.Length);
        Assert.Equal("BlockSecond", Encoding.UTF8.GetString(nodeData.Span));
    }

    [Fact]
    public void EndToEnd_Lz4CompressedBlocks()
    {
        // Arrange - Use pre-compressed LZ4 test data
        var compressedBlock = Helpers.DecompressionTestFixtures.Lz4HelloWorld;
        
        using var stream = new MemoryStream();
        stream.Write(compressedBlock);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = 10,
                CompressedSize = (uint)compressedBlock.Length,
                Flags = 2  // LZ4
            }
        };

        var node = new NodeInfo
        {
            Path = "compressed.bin",
            Offset = 0,
            Size = 10,
            Flags = 0
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        var nodeData = _extractor.ReadNode(region, node);

        // Assert
        Assert.Equal(10, region.Length);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(nodeData.Span));
    }

    [Fact]
    public void EndToEnd_StreamingInfoResolution_WithResS()
    {
        // Arrange - Create .resS node with known data
        var resSData = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8.ToArray();  // 36 bytes
        
        using var stream = new MemoryStream();
        stream.Write(resSData);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)resSData.Length,
                CompressedSize = (uint)resSData.Length,
                Flags = 0
            }
        };

        var nodes = new[]
        {
            new NodeInfo
            {
                Path = "archive:/CAB-abc123/CAB-abc123.resS",
                Offset = 0,
                Size = resSData.Length,
                Flags = 0
            }
        };

        var streamingInfo = new StreamingInfo
        {
            Path = "archive:/CAB-abc123/CAB-abc123.resS",
            Offset = 10,  // Start at 'A'
            Size = 16     // "ABCDEFGHIJKLMNOP"
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        var slice = _resolver.Resolve(nodes, region, streamingInfo);

        // Assert
        Assert.Equal(16, slice.Length);
        Assert.Equal("ABCDEFGHIJKLMNOP", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void EndToEnd_StreamingInfoResolution_WithNodeOffset()
    {
        // Arrange - .resS node starts at offset 100 in data region
        var prefixData = new byte[100];
        var resSData = "ResourceData_0123456789"u8.ToArray();
        
        using var stream = new MemoryStream();
        stream.Write(prefixData);
        stream.Write(resSData);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)(prefixData.Length + resSData.Length),
                CompressedSize = (uint)(prefixData.Length + resSData.Length),
                Flags = 0
            }
        };

        var nodes = new[]
        {
            new NodeInfo
            {
                Path = "prefix.bin",
                Offset = 0,
                Size = 100,
                Flags = 0
            },
            new NodeInfo
            {
                Path = "archive:/CAB-test/test.resS",
                Offset = 100,
                Size = resSData.Length,
                Flags = 0
            }
        };

        var streamingInfo = new StreamingInfo
        {
            Path = "archive:/CAB-test/test.resS",
            Offset = 13,  // Start at '0'
            Size = 10     // "0123456789"
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        var slice = _resolver.Resolve(nodes, region, streamingInfo);

        // Assert
        Assert.Equal(10, slice.Length);
        Assert.Equal("0123456789", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void EndToEnd_MixedCompressionBlocks()
    {
        // Arrange - Mix of uncompressed and LZ4 blocks
        var uncompressedData = "Uncompressed"u8.ToArray();
        var compressedBlock = Helpers.DecompressionTestFixtures.Lz4HelloWorld;
        
        using var stream = new MemoryStream();
        stream.Write(uncompressedData);
        stream.Write(compressedBlock);
        stream.Position = 0;

        var blocks = new[]
        {
            new StorageBlock
            {
                UncompressedSize = (uint)uncompressedData.Length,
                CompressedSize = (uint)uncompressedData.Length,
                Flags = 0  // None
            },
            new StorageBlock
            {
                UncompressedSize = 10,
                CompressedSize = (uint)compressedBlock.Length,
                Flags = 2  // LZ4
            }
        };

        var nodes = new[]
        {
            new NodeInfo { Path = "file1.bin", Offset = 0, Size = 12, Flags = 0 },
            new NodeInfo { Path = "file2.bin", Offset = 12, Size = 10, Flags = 0 }
        };

        // Act
        var region = _builder.Build(stream, 0, blocks);
        var node1Data = _extractor.ReadNode(region, nodes[0]);
        var node2Data = _extractor.ReadNode(region, nodes[1]);

        // Assert
        Assert.Equal(22, region.Length);
        Assert.Equal("Uncompressed", Encoding.UTF8.GetString(node1Data.Span));
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(node2Data.Span));
    }

    [Fact]
    public void EndToEnd_OverlappingNodes_ThrowsException()
    {
        // Arrange
        var data = "TestDataForOverlapDetection"u8.ToArray();
        using var stream = new MemoryStream();
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

        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 15, Flags = 0 },
            new NodeInfo { Path = "node2", Offset = 10, Size = 10, Flags = 0 }  // Overlaps with node1
        };

        // Act
        _builder.Build(stream, 0, blocks);

        // Assert
        var ex = Assert.Throws<NodeOverlapException>(() => _extractor.ValidateNoOverlaps(nodes));
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
