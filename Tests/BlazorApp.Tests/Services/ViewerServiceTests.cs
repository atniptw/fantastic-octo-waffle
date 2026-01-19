using BlazorApp.Models;
using BlazorApp.Services;
using Microsoft.JSInterop;
using Moq;

namespace BlazorApp.Tests.Services;

public class ViewerServiceTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly ViewerService _sut;

    public ViewerServiceTests()
    {
        _sut = new ViewerService(_jsRuntimeMock.Object);
    }

    [Fact]
    public void Constructor_NullJSRuntime_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ViewerService(null!));
        Assert.Equal("jsRuntime", exception.ParamName);
    }

    [Fact]
    public async Task InitializeAsync_NullCanvasId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InitializeAsync(null!));
        Assert.Equal("canvasId", exception.ParamName);
        Assert.Contains("Canvas ID cannot be empty or whitespace", exception.Message);
    }

    [Fact]
    public async Task InitializeAsync_EmptyCanvasId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InitializeAsync(""));
        Assert.Equal("canvasId", exception.ParamName);
    }

    [Fact]
    public async Task InitializeAsync_WhitespaceCanvasId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InitializeAsync("   "));
        Assert.Equal("canvasId", exception.ParamName);
    }

    [Fact]
    public async Task InitializeAsync_ValidCanvasId_CompletesSuccessfully()
    {
        // Act
        await _sut.InitializeAsync("viewer-canvas");

        // Assert - no exception thrown
    }

    [Fact]
    public async Task InitializeAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.InitializeAsync("viewer-canvas", cts.Token));
    }

    [Fact]
    public async Task ShowAsync_NullGeometry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ShowAsync(null!));
        Assert.Equal("geometry", exception.ParamName);
    }

    [Fact]
    public async Task ShowAsync_ValidGeometry_ReturnsStubMeshId()
    {
        // Arrange
        var geometry = CreateValidGeometry();

        // Act
        var result = await _sut.ShowAsync(geometry);

        // Assert
        Assert.StartsWith("stub-mesh-", result);
    }

    [Fact]
    public async Task ShowAsync_MultipleCalls_ReturnsUniqueIds()
    {
        // Arrange
        var geometry = CreateValidGeometry();

        // Act
        var result1 = await _sut.ShowAsync(geometry);
        var result2 = await _sut.ShowAsync(geometry);
        var result3 = await _sut.ShowAsync(geometry);

        // Assert
        Assert.Equal("stub-mesh-0", result1);
        Assert.Equal("stub-mesh-1", result2);
        Assert.Equal("stub-mesh-2", result3);
    }

    [Fact]
    public async Task ShowAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var geometry = CreateValidGeometry();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ShowAsync(geometry, cts.Token));
    }

    [Fact]
    public async Task UpdateMaterialAsync_NullMeshId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateMaterialAsync(null!));
        Assert.Equal("meshId", exception.ParamName);
        Assert.Contains("Mesh ID cannot be empty or whitespace", exception.Message);
    }

    [Fact]
    public async Task UpdateMaterialAsync_EmptyMeshId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateMaterialAsync(""));
        Assert.Equal("meshId", exception.ParamName);
    }

    [Fact]
    public async Task UpdateMaterialAsync_ValidMeshId_CompletesSuccessfully()
    {
        // Act
        await _sut.UpdateMaterialAsync("stub-mesh-0", "#FF0000", true);

        // Assert - no exception thrown
    }

    [Fact]
    public async Task UpdateMaterialAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.UpdateMaterialAsync("stub-mesh-0", ct: cts.Token));
    }

    [Fact]
    public async Task ClearAsync_NoParameters_CompletesSuccessfully()
    {
        // Act
        await _sut.ClearAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task ClearAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ClearAsync(cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_NoParameters_CompletesSuccessfully()
    {
        // Act
        await _sut.DisposeAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task DisposeAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.DisposeAsync(cts.Token));
    }

    private static ThreeJsGeometry CreateValidGeometry()
    {
        return new ThreeJsGeometry
        {
            Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f },
            Indices = new uint[] { 0, 1, 2 },
            VertexCount = 3,
            TriangleCount = 1
        };
    }
}
