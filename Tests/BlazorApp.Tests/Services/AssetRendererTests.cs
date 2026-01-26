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
            () => _sut.RenderAsync(null!, Array.Empty<byte>()));
        Assert.Equal("file", exception.ParamName);
    }

    [Fact]
    public async Task RenderAsync_NonRenderableFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, false);
        var zipBytes = Array.Empty<byte>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RenderAsync(file, zipBytes));
        Assert.Contains("not marked as renderable", exception.Message);
    }

    [Fact]
    public async Task RenderAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var file = new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);
        var zipBytes = Array.Empty<byte>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.RenderAsync(file, zipBytes, cts.Token));
    }
}
