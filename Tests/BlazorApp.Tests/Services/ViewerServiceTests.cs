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
    public async Task InitializeAsync_ValidCanvasId_CallsJSInterop()
    {
        // Act
        await _sut.InitializeAsync("viewer-canvas");

        // Assert - verify JS interop was called
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<object>(
                "meshRenderer.init",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
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
    public async Task ShowAsync_NotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        var geometry = CreateValidGeometry();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ShowAsync(geometry));
        Assert.Contains("not initialized", exception.Message);
    }

    [Fact]
    public async Task ShowAsync_ValidGeometry_CallsJSInteropAndReturnsMeshId()
    {
        // Arrange
        await _sut.InitializeAsync("viewer-canvas");
        var geometry = CreateValidGeometry();
        _jsRuntimeMock.Setup(js => js.InvokeAsync<string>(
                "meshRenderer.loadMesh",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync("mesh-1");

        // Act
        var result = await _sut.ShowAsync(geometry);

        // Assert
        Assert.Equal("mesh-1", result);
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<string>(
                "meshRenderer.loadMesh",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowAsync_GeometryWithGroups_PassesGroupsToJS()
    {
        // Arrange
        await _sut.InitializeAsync("viewer-canvas");
        var geometry = CreateGeometryWithGroups();
        _jsRuntimeMock.Setup(js => js.InvokeAsync<string>(
                "meshRenderer.loadMesh",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync("mesh-2");

        // Act
        await _sut.ShowAsync(geometry);

        // Assert
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<string>(
                "meshRenderer.loadMesh",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await _sut.InitializeAsync("viewer-canvas");
        var geometry = CreateValidGeometry();
        using var cts = new CancellationTokenSource();
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
    public async Task UpdateMaterialAsync_ValidMeshId_CallsJSInterop()
    {
        // Act
        await _sut.UpdateMaterialAsync("mesh-1", "#FF0000", true);

        // Assert
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<object>(
                "meshRenderer.updateMaterial",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateMaterialAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.UpdateMaterialAsync("mesh-1", ct: cts.Token));
    }

    [Fact]
    public async Task ClearAsync_NoParameters_CallsJSInterop()
    {
        // Act
        await _sut.ClearAsync();

        // Assert
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<object>(
                "meshRenderer.clear",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ClearAsync(cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_NoParameters_CallsJSInterop()
    {
        // Act
        await _sut.DisposeAsync();

        // Assert
        _jsRuntimeMock.Verify(
            js => js.InvokeAsync<object>(
                "meshRenderer.dispose",
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
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

    private static ThreeJsGeometry CreateGeometryWithGroups()
    {
        return new ThreeJsGeometry
        {
            Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f },
            Indices = new uint[] { 0, 1, 2, 1, 3, 2 },
            VertexCount = 4,
            TriangleCount = 2,
            Groups = new List<SubMeshGroup>
            {
                new() { Start = 0, Count = 3, MaterialIndex = 0 },
                new() { Start = 3, Count = 3, MaterialIndex = 1 }
            }
        };
    }
}
