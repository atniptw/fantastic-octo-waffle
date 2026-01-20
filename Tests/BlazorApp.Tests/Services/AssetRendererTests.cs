using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class AssetRendererTests
{
    private readonly AssetRenderer _sut = new();

    [Fact]
    public async Task RenderAsync_NullFile_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.RenderAsync(null!));
        Assert.Equal("file", exception.ParamName);
    }

    [Fact]
    public async Task RenderAsync_ValidFile_ThrowsNotImplementedException()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.RenderAsync(file));
        Assert.Contains("Asset rendering not yet implemented", exception.Message);
    }

    [Fact]
    public async Task RenderAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.RenderAsync(file, cts.Token));
    }
}
