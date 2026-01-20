using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class ZipIndexerTests
{
    private readonly ZipIndexer _sut = new();

    [Fact]
    public async Task IndexAsync_NullZipStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _sut.IndexAsync(null!))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
        Assert.Equal("zipStream", exception.ParamName);
    }

    [Fact]
    public async Task IndexAsync_ValidStream_ReturnsEmptyStream()
    {
        // Arrange
        var zipStream = EmptyAsyncEnumerable();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task IndexAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var zipStream = EmptyAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _sut.IndexAsync(zipStream, cts.Token))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
    }

    [Fact]
    public async Task IndexAsync_StreamWithData_ReturnsEmptyStream()
    {
        // Arrange
        var zipStream = StreamWithData();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert - stub implementation should still return empty
        Assert.Empty(items);
    }

    private static async IAsyncEnumerable<byte[]> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<byte[]> StreamWithData()
    {
        await Task.CompletedTask;
        yield return new byte[] { 0x50, 0x4b, 0x03, 0x04 }; // ZIP header
        yield return new byte[] { 0x00, 0x00 };
    }
}
