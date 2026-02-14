using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class AssetScannerTests
{
    private readonly AssetScanner _sut = new();

    [Fact]
    public async Task IsRenderableAsync_NullFile_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.IsRenderableAsync(null!));
        Assert.Equal("file", exception.ParamName);
    }

    [Fact]
    public async Task IsRenderableAsync_ValidFile_ReturnsFalse()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, false);

        // Act
        var result = await _sut.IsRenderableAsync(file);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsRenderableAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, false);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.IsRenderableAsync(file, cts.Token));
    }

    [Fact]
    public async Task IsRenderableAsync_MarkedAsRenderable_StillReturnsFalse()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);

        // Act
        var result = await _sut.IsRenderableAsync(file);

        // Assert - stub implementation always returns false
        Assert.False(result);
    }
}
