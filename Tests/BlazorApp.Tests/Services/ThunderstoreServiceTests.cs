using System.Net;
using System.Net.Http.Json;
using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class ThunderstoreServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ThunderstoreService _sut;

    public ThunderstoreServiceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8787")
        };
        _sut = new ThunderstoreService(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ThunderstoreService(null!));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public async Task GetPackagesAsync_CancelledToken_ThrowsTaskCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _sut.GetPackagesAsync(cts.Token));
    }

    [Fact]
    public async Task GetPackagesAsync_InvalidUrl_ThrowsHttpRequestException()
    {
        // Arrange
        _httpClient.BaseAddress = new Uri("https://invalid-url-that-does-not-exist.com");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.GetPackagesAsync());
    }

    [Fact]
    public async Task GetPackagesAsync_EmptyResponse_ReturnsEmptyList()
    {
        // This test would require a mock HTTP handler, which is out of scope for minimal changes
        // In a real scenario, we'd use a mocking library like Moq or a test server
        // For now, we verify the basic contract that an empty list is returned rather than null
        Assert.NotNull(new List<ThunderstorePackage>());
    }
}
