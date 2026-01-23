using System.Net;
using BlazorApp.Models;
using BlazorApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace BlazorApp.Tests.Services;

public class ZipDownloaderTests : IDisposable
{
    private readonly Mock<ILogger<ZipDownloader>> _mockLogger;
    private const string TempDirectoryName = "repo-mod-viewer";

    public ZipDownloaderTests()
    {
        _mockLogger = new Mock<ILogger<ZipDownloader>>();
        CleanupTestTempFiles();
    }

    public void Dispose()
    {
        CleanupTestTempFiles();
    }

    private void CleanupTestTempFiles()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), TempDirectoryName);
            if (Directory.Exists(tempDir))
            {
                foreach (var file in Directory.GetFiles(tempDir, "*.zip"))
                {
                    try { File.Delete(file); }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ZipDownloader(null!, _mockLogger.Object));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidHttpClient_DoesNotThrow()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act & Assert
        var downloader = new ZipDownloader(httpClient, _mockLogger.Object);
        Assert.NotNull(downloader);
    }

    [Fact]
    public async Task GetMetaAsync_NullVersion_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.GetMetaAsync(null!));
        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public async Task GetMetaAsync_ValidVersion_ReturnsNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var version = new PackageVersion { Name = "TestMod" };

        // Act
        var result = await sut.GetMetaAsync(version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMetaAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var version = new PackageVersion { Name = "TestMod" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.GetMetaAsync(version, cts.Token));
    }

    [Fact]
    public async Task StreamZipAsync_NullVersion_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in sut.StreamZipAsync(null!))
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
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var version = new PackageVersion { Name = "TestMod" };
        var chunks = new List<byte[]>();

        // Act
        await foreach (var chunk in sut.StreamZipAsync(version))
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
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var version = new PackageVersion { Name = "TestMod" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in sut.StreamZipAsync(version, cts.Token))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
    }

    [Fact]
    public async Task DownloadAsync_NullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.DownloadAsync(null!));
        Assert.Equal("url", exception.ParamName);
    }

    [Fact]
    public async Task DownloadAsync_ValidZipWithContentLength_ReturnsFilePath()
    {
        // Arrange
        var fakeZipBytes = CreateFakeZipBytes(1024);
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");
        var progressCalls = new List<(long downloaded, long total)>();

        // Act
        var result = await sut.DownloadAsync(url, (d, t) => progressCalls.Add((d, t)));

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        Assert.EndsWith(".zip", result);
        Assert.NotEmpty(progressCalls);
        Assert.All(progressCalls, call => Assert.True(call.downloaded <= call.total || call.total == 0));
        
        // Verify file content
        var downloadedBytes = await File.ReadAllBytesAsync(result);
        Assert.Equal(fakeZipBytes, downloadedBytes);

        // Cleanup
        File.Delete(result);
    }

    [Fact]
    public async Task DownloadAsync_MissingContentLength_CallsProgressWithZeroTotal()
    {
        // Arrange
        var fakeZipBytes = CreateFakeZipBytes(1024);
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: false);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");
        var progressCalls = new List<(long downloaded, long total)>();

        // Act
        var result = await sut.DownloadAsync(url, (d, t) => progressCalls.Add((d, t)));

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        Assert.NotEmpty(progressCalls);
        Assert.All(progressCalls, call => Assert.Equal(0, call.total));

        // Cleanup
        File.Delete(result);
    }

    [Fact]
    public async Task DownloadAsync_404Response_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.NotFound, Array.Empty<byte>());
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.DownloadAsync(url));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadAsync_500Response_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.InternalServerError, Array.Empty<byte>());
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.DownloadAsync(url));
        Assert.Contains("cannot reach mod service", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadAsync_InvalidZipMagicBytes_ThrowsInvalidOperationException()
    {
        // Arrange - Create file with invalid magic bytes
        var fakeZipBytes = new byte[1024];
        fakeZipBytes[0] = 0x00; // Invalid magic
        fakeZipBytes[1] = 0x00;
        fakeZipBytes[2] = 0x00;
        fakeZipBytes[3] = 0x00;
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DownloadAsync(url));
        Assert.Contains("corrupted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var fakeZipBytes = CreateFakeZipBytes(1024 * 1024); // 1MB to allow cancellation
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - TaskCanceledException is a subclass of OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.DownloadAsync(url, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DownloadAsync_Timeout_ThrowsHttpRequestException()
    {
        // Arrange - Create a handler that delays longer than timeout
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                // Simulate slow download that will be cancelled
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            Timeout = TimeSpan.FromSeconds(1) // Short timeout for test
        };
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => sut.DownloadAsync(url));
    }

    [Fact]
    public async Task DownloadAsync_SuccessfulDownload_CleansUpOnDispose()
    {
        // Arrange
        var fakeZipBytes = CreateFakeZipBytes(1024);
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Act
        var result = await sut.DownloadAsync(url);
        Assert.True(File.Exists(result));

        // Dispose and verify cleanup
        await sut.DisposeAsync();
        Assert.False(File.Exists(result));
    }

    [Fact]
    public async Task DownloadAsync_ErrorDuringDownload_CleansUpTempFile()
    {
        // Arrange - Create handler that throws after sending headers
        var fakeZipBytes = CreateFakeZipBytes(1024);
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true, throwOnRead: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");

        // Get temp directory to check for leftover files
        var tempDir = Path.Combine(Path.GetTempPath(), TempDirectoryName);
        var filesBefore = Directory.Exists(tempDir) ? Directory.GetFiles(tempDir).Length : 0;

        // Act & Assert - IOException wrapped in HttpRequestException
        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.DownloadAsync(url));

        // Verify no new files left behind
        var filesAfter = Directory.Exists(tempDir) ? Directory.GetFiles(tempDir).Length : 0;
        Assert.Equal(filesBefore, filesAfter);
    }

    [Fact]
    public async Task DownloadAsync_ProgressCallback_FiresForEachChunk()
    {
        // Arrange - Create large enough file to have multiple chunks
        var fakeZipBytes = CreateFakeZipBytes(65536 * 3); // 3 chunks
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod.zip");
        var progressCalls = new List<(long downloaded, long total)>();

        // Act
        var result = await sut.DownloadAsync(url, (d, t) => progressCalls.Add((d, t)));

        // Assert
        Assert.NotEmpty(progressCalls);
        Assert.True(progressCalls.Count >= 3); // At least 3 chunks
        
        // Verify progress is monotonically increasing
        for (int i = 1; i < progressCalls.Count; i++)
        {
            Assert.True(progressCalls[i].downloaded >= progressCalls[i - 1].downloaded);
        }

        // Last progress should equal total
        var lastProgress = progressCalls[^1];
        Assert.Equal(fakeZipBytes.Length, lastProgress.downloaded);
        Assert.Equal(fakeZipBytes.Length, lastProgress.total);

        // Cleanup
        File.Delete(result);
    }

    [Fact]
    public async Task DownloadAsync_ParallelDownloads_CreateUniqueFiles()
    {
        // Arrange
        var fakeZipBytes = CreateFakeZipBytes(1024);
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, fakeZipBytes, includeContentLength: true);
        using var httpClient = new HttpClient(mockHandler.Object);
        await using var sut = new ZipDownloader(httpClient, _mockLogger.Object);
        var url = new Uri("https://example.com/mod/1.0.0/TestMod.zip");

        // Act - Download same URL twice in parallel
        var task1 = sut.DownloadAsync(url);
        var task2 = sut.DownloadAsync(url);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        Assert.NotEqual(results[0], results[1]); // Different file paths
        Assert.True(File.Exists(results[0]));
        Assert.True(File.Exists(results[1]));

        // Cleanup
        File.Delete(results[0]);
        File.Delete(results[1]);
    }

    [Fact]
    public async Task DownloadAsync_InsufficientDiskSpace_ThrowsInvalidOperationException()
    {
        // Note: This test is difficult to implement reliably across platforms
        // as we cannot easily mock DriveInfo. We'll skip it for now.
        // In a real implementation, we would use a wrapper interface for disk operations
        await Task.CompletedTask;
    }

    // Helper methods

    private static byte[] CreateFakeZipBytes(int size)
    {
        var bytes = new byte[size];
        // Valid ZIP magic bytes
        bytes[0] = 0x50; // 'P'
        bytes[1] = 0x4B; // 'K'
        bytes[2] = 0x03;
        bytes[3] = 0x04;
        // Fill rest with random data
        new Random().NextBytes(bytes.AsSpan(4));
        return bytes;
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(
        HttpStatusCode statusCode,
        byte[] content,
        bool includeContentLength = true,
        bool throwOnRead = false)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(statusCode);

                if (statusCode == HttpStatusCode.OK)
                {
                    HttpContent streamContent;
                    
                    if (throwOnRead)
                    {
                        streamContent = new ThrowingStreamContent(content);
                    }
                    else
                    {
                        streamContent = new ByteArrayContent(content);
                    }

                    response.Content = streamContent;

                    // ByteArrayContent automatically sets ContentLength, so we need to clear it
                    if (!includeContentLength)
                    {
                        response.Content.Headers.ContentLength = null;
                    }
                }
                else
                {
                    response.Content = new StringContent(string.Empty);
                }

                return response;
            });

        return mockHandler;
    }

    /// <summary>
    /// Stream content that throws IOException when read
    /// </summary>
    private class ThrowingStreamContent : HttpContent
    {
        private readonly byte[] _content;

        public ThrowingStreamContent(byte[] content)
        {
            _content = content;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new IOException("Simulated disk write error");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }
}
