using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class ZipDownloaderTests : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly ZipDownloader _sut;

    public ZipDownloaderTests()
    {
        _sut = new ZipDownloader(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ZipDownloader(null!));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public async Task GetMetaAsync_NullVersion_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetMetaAsync(null!));
        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public async Task GetMetaAsync_ValidVersion_ReturnsNull()
    {
        // Arrange
        var version = new PackageVersion { Name = "TestMod" };

        // Act
        var result = await _sut.GetMetaAsync(version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMetaAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var version = new PackageVersion { Name = "TestMod" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.GetMetaAsync(version, cts.Token));
    }

    [Fact]
    public async Task StreamZipAsync_NullVersion_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _sut.StreamZipAsync(null!))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public async Task StreamZipAsync_ValidVersion_ReturnsEmptyStream()
    {
        // Arrange
        var version = new PackageVersion { Name = "TestMod" };
        var chunks = new List<byte[]>();

        // Act
        await foreach (var chunk in _sut.StreamZipAsync(version))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task StreamZipAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var version = new PackageVersion { Name = "TestMod" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _sut.StreamZipAsync(version, cts.Token))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
    }
}
