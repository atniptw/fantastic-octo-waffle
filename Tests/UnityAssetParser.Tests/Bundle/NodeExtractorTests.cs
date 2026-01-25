using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for NodeExtractor class.
/// </summary>
public class NodeExtractorTests
{
    [Fact]
    public void ReadNode_ValidNode_ReturnsCorrectData()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = DataRegionTestFixtures.HelloNodeFull;

        // Act
        var result = extractor.ReadNode(region, node);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void ReadNode_PartialNode_ReturnsCorrectSlice()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = DataRegionTestFixtures.PartialNode;

        // Act
        var result = extractor.ReadNode(region, node);

        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal("World", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void ReadNode_NodeAtEnd_ReturnsCorrectData()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = new NodeInfo
        {
            Path = "test",
            Offset = 9,
            Size = 1,
            Flags = 0
        };

        // Act
        var result = extractor.ReadNode(region, node);

        // Assert
        Assert.Equal(1, result.Length);
        Assert.Equal("d", Encoding.UTF8.GetString(result.Span));
    }

    [Fact]
    public void ReadNode_NegativeOffset_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = new NodeInfo
        {
            Path = "test",
            Offset = -1,
            Size = 5,
            Flags = 0
        };

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => extractor.ReadNode(region, node));
        Assert.Contains("negative offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadNode_NegativeSize_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = new NodeInfo
        {
            Path = "test",
            Offset = 0,
            Size = -1,
            Flags = 0
        };

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => extractor.ReadNode(region, node));
        Assert.Contains("negative size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadNode_NodeExceedsRegion_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);
        var extractor = new NodeExtractor(detectOverlaps: false);
        var node = new NodeInfo
        {
            Path = "test",
            Offset = 5,
            Size = 10,
            Flags = 0
        };

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => extractor.ReadNode(region, node));
        Assert.Contains("exceed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNoOverlaps_NonOverlappingNodes_DoesNotThrow()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 5, Flags = 0 },
            new NodeInfo { Path = "node2", Offset = 5, Size = 5, Flags = 0 },
            new NodeInfo { Path = "node3", Offset = 10, Size = 5, Flags = 0 }
        };

        // Act & Assert - should not throw
        extractor.ValidateNoOverlaps(nodes);
    }

    [Fact]
    public void ValidateNoOverlaps_OverlappingNodes_ThrowsNodeOverlapException()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 10, Flags = 0 },
            new NodeInfo { Path = "node2", Offset = 5, Size = 5, Flags = 0 }  // Overlaps with node1
        };

        // Act & Assert
        var ex = Assert.Throws<NodeOverlapException>(() => extractor.ValidateNoOverlaps(nodes));
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("node1", ex.Message);
        Assert.Contains("node2", ex.Message);
    }

    [Fact]
    public void ValidateNoOverlaps_AdjacentNodes_DoesNotThrow()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 5, Flags = 0 },
            new NodeInfo { Path = "node2", Offset = 5, Size = 5, Flags = 0 }  // Adjacent, not overlapping
        };

        // Act & Assert - should not throw
        extractor.ValidateNoOverlaps(nodes);
    }

    [Fact]
    public void ValidateNoOverlaps_UnorderedNodes_DetectsOverlaps()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = new[]
        {
            new NodeInfo { Path = "node2", Offset = 5, Size = 10, Flags = 0 },
            new NodeInfo { Path = "node1", Offset = 0, Size = 10, Flags = 0 }  // Out of order, overlaps
        };

        // Act & Assert
        var ex = Assert.Throws<NodeOverlapException>(() => extractor.ValidateNoOverlaps(nodes));
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNoOverlaps_DisabledDetection_DoesNotThrow()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: false);
        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 10, Flags = 0 },
            new NodeInfo { Path = "node2", Offset = 5, Size = 5, Flags = 0 }  // Overlaps, but detection disabled
        };

        // Act & Assert - should not throw
        extractor.ValidateNoOverlaps(nodes);
    }

    [Fact]
    public void ValidateNoOverlaps_SingleNode_DoesNotThrow()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = new[]
        {
            new NodeInfo { Path = "node1", Offset = 0, Size = 10, Flags = 0 }
        };

        // Act & Assert - should not throw
        extractor.ValidateNoOverlaps(nodes);
    }

    [Fact]
    public void ValidateNoOverlaps_EmptyList_DoesNotThrow()
    {
        // Arrange
        var extractor = new NodeExtractor(detectOverlaps: true);
        var nodes = Array.Empty<NodeInfo>();

        // Act & Assert - should not throw
        extractor.ValidateNoOverlaps(nodes);
    }
}
