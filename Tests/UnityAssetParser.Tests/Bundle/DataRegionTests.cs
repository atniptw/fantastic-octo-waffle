using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for DataRegion class.
/// </summary>
public class DataRegionTests
{
    [Fact]
    public void Constructor_ValidData_CreatesDataRegion()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;

        // Act
        var region = new DataRegion(data);

        // Assert
        Assert.Equal(10, region.Length);
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DataRegion(null!));
    }

    [Fact]
    public void ReadSlice_ValidRange_ReturnsCorrectSlice()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act
        var slice = region.ReadSlice(0, 5);

        // Assert
        Assert.Equal(5, slice.Length);
        Assert.Equal("Hello", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void ReadSlice_MiddleRange_ReturnsCorrectSlice()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act
        var slice = region.ReadSlice(5, 5);

        // Assert
        Assert.Equal(5, slice.Length);
        Assert.Equal("World", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void ReadSlice_EmptyRange_ReturnsEmptySlice()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act
        var slice = region.ReadSlice(5, 0);

        // Assert
        Assert.Equal(0, slice.Length);
    }

    [Fact]
    public void ReadSlice_FullRange_ReturnsAllData()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act
        var slice = region.ReadSlice(0, 10);

        // Assert
        Assert.Equal(10, slice.Length);
        Assert.Equal("HelloWorld", Encoding.UTF8.GetString(slice.Span));
    }

    [Fact]
    public void ReadSlice_NegativeOffset_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => region.ReadSlice(-1, 5));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadSlice_NegativeSize_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => region.ReadSlice(0, -1));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadSlice_OffsetPlusSizeExceedsLength_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => region.ReadSlice(5, 10));
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadSlice_OffsetAtEnd_ThrowsBoundsException()
    {
        // Arrange
        var data = DataRegionTestFixtures.HelloWorldBytes;
        var region = new DataRegion(data);

        // Act & Assert
        var ex = Assert.Throws<BoundsException>(() => region.ReadSlice(10, 1));
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
