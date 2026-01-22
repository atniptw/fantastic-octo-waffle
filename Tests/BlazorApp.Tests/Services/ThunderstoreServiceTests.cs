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
        // Arrange - Use a mocked handler that returns 404
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.NotFound, string.Empty);
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8787")
        };
        var service = new ThunderstoreService(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetPackagesAsync());
    }

    [Fact]
    public async Task GetPackagesAsync_EmptyResponse_ReturnsEmptyList()
    {
        // Arrange - Mock handler returns empty JSON array
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.OK, "[]");
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8787")
        };
        var service = new ThunderstoreService(httpClient);

        // Act
        var result = await service.GetPackagesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Mock HTTP message handler for testing without real network calls
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };

            if (!response.IsSuccessStatusCode)
            {
                response.Content = new StringContent(string.Empty);
            }

            return Task.FromResult(response);
        }
    }
}
