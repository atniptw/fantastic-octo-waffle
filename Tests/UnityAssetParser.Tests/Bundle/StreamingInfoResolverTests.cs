using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for StreamingInfoResolver class.
/// </summary>
public class StreamingInfoResolverTests
{
    [Fact]
    public void Resolve_ValidReference_ReturnsCorrectSlice()
    {
        // Arrange
        var data = "0123456789ABCDEFGHIJ"u8.ToArray();  // 20 bytes
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[]
        {
            DataRegionTestFixtures.ResSNode  // offset=0, size=20
        };

        var info = DataRegionTestFixtures.ResSSlice(0, 10);  // First 10 bytes

        // Act
        var result = resolver.Resolve(nodes, region, info);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.Equal("0123456789", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void Resolve_MiddleSlice_ReturnsCorrectData()
    {
        // Arrange
        var data = "0123456789ABCDEFGHIJ"u8.ToArray();  // 20 bytes
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[]
        {
            DataRegionTestFixtures.ResSNode  // offset=0, size=20
        };

        var info = DataRegionTestFixtures.ResSSlice(10, 10);  // Last 10 bytes

        // Act
        var result = resolver.Resolve(nodes, region, info);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.Equal("ABCDEFGHIJ", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void Resolve_NodeWithOffset_CalculatesAbsoluteOffsetCorrectly()
    {
        // Arrange
        var data = "0123456789ABCDEFGHIJ"u8.ToArray();  // 20 bytes
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var node = new NodeInfo
        {
            Path = "archive:/CAB-test/test.resS",
            Offset = 5,   // Node starts at offset 5 in data region
            Size = 10,
            Flags = 0
        };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-test/test.resS",
            Offset = 2,   // 2 bytes into the node
            Size = 5
        };

        // Act
        var result = resolver.Resolve(new[] { node }, region, info);

        // Assert - should read from absolute offset 7 (5 + 2)
        Assert.Equal(5, result.Length);
        Assert.Equal("789AB", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void Resolve_PathNotFound_ThrowsStreamingInfoException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[]
        {
            new NodeInfo { Path = "different/path.resS", Offset = 0, Size = 10, Flags = 0 }
        };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-test/test.resS",
            Offset = 0,
            Size = 5
        };

        // Act & Assert
        var ex = Assert.Throws<StreamingInfoException>(() => 
            resolver.Resolve(nodes, region, info));
        Assert.Contains("does not match", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archive:/CAB-test/test.resS", ex.Message);
    }

    [Fact]
    public void Resolve_PathMatchingIsCaseSensitive()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[]
        {
            new NodeInfo { Path = "archive:/cab-test/test.ress", Offset = 0, Size = 10, Flags = 0 }
        };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-test/test.resS",  // Different case
            Offset = 0,
            Size = 5
        };

        // Act & Assert
        var ex = Assert.Throws<StreamingInfoException>(() => 
            resolver.Resolve(nodes, region, info));
        Assert.Contains("does not match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NegativeOffset_ThrowsStreamingInfoException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[] { DataRegionTestFixtures.ResSNode };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-abc123/CAB-abc123.resS",
            Offset = -1,
            Size = 5
        };

        // Act & Assert
        var ex = Assert.Throws<StreamingInfoException>(() => 
            resolver.Resolve(nodes, region, info));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NegativeSize_ThrowsStreamingInfoException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[] { DataRegionTestFixtures.ResSNode };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-abc123/CAB-abc123.resS",
            Offset = 0,
            Size = -1
        };

        // Act & Assert
        var ex = Assert.Throws<StreamingInfoException>(() => 
            resolver.Resolve(nodes, region, info));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_SliceExceedsNodeBounds_ThrowsStreamingInfoException()
    {
        // Arrange
        var data = "0123456789ABCDEFGHIJ"u8.ToArray();
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[] { DataRegionTestFixtures.ResSNode };  // size=20

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-abc123/CAB-abc123.resS",
            Offset = 15,
            Size = 10  // 15 + 10 = 25 > 20
        };

        // Act & Assert
        var ex = Assert.Throws<StreamingInfoException>(() => 
            resolver.Resolve(nodes, region, info));
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_EmptySlice_ReturnsEmptyResult()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[] { DataRegionTestFixtures.ResSNode };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-abc123/CAB-abc123.resS",
            Offset = 5,
            Size = 0
        };

        // Act
        var result = resolver.Resolve(nodes, region, info);

        // Assert
        Assert.Equal(0, result.Length);
    }

    [Fact]
    public void Resolve_MultipleNodesWithMatchingPath_UsesFirstMatch()
    {
        // Arrange
        var data = "0123456789ABCDEFGHIJ"u8.ToArray();
        var region = new DataRegion(data);
        var resolver = new StreamingInfoResolver();
        
        var nodes = new[]
        {
            new NodeInfo
            {
                Path = "archive:/CAB-test/test.resS",
                Offset = 0,
                Size = 10,
                Flags = 0
            },
            new NodeInfo
            {
                Path = "archive:/CAB-test/test.resS",  // Duplicate path
                Offset = 10,
                Size = 10,
                Flags = 0
            }
        };

        var info = new StreamingInfo
        {
            Path = "archive:/CAB-test/test.resS",
            Offset = 0,
            Size = 5
        };

        // Act
        var result = resolver.Resolve(nodes, region, info);

        // Assert - should use first matching node at offset 0
        Assert.Equal("01234", Encoding.UTF8.GetString(result.Span));
    }
}
