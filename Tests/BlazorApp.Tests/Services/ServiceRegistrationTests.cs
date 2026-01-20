using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

/// <summary>
/// Tests for DI service registration and resolution.
/// Validates that all services can be resolved from the container without errors.
/// </summary>
public class ServiceRegistrationTests
{
    [Fact]
    public void AllServices_RegisteredAsScoped_CanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IZipDownloader, ZipDownloader>();
        services.AddScoped<IZipIndexer, ZipIndexer>();
        services.AddScoped<IAssetScanner, AssetScanner>();
        services.AddScoped<IAssetRenderer, AssetRenderer>();
        services.AddScoped<IViewerService, ViewerService>();
        services.AddHttpClient();
        
        // ViewerService requires IJSRuntime
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddSingleton(jsRuntimeMock.Object);
        
        var provider = services.BuildServiceProvider();

        // Act & Assert - All services should resolve without throwing
        Assert.NotNull(provider.GetRequiredService<IZipDownloader>());
        Assert.NotNull(provider.GetRequiredService<IZipIndexer>());
        Assert.NotNull(provider.GetRequiredService<IAssetScanner>());
        Assert.NotNull(provider.GetRequiredService<IAssetRenderer>());
        Assert.NotNull(provider.GetRequiredService<IViewerService>());
    }

    [Fact]
    public void HttpClient_RegisteredWithCorrectConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("WorkerAPI", client =>
        {
            client.BaseAddress = new Uri("https://api.worker.dev");
            client.DefaultRequestHeaders.Add("User-Agent", "RepoModViewer/0.1 (+https://atniptw.github.io)");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        var client = factory.CreateClient("WorkerAPI");

        // Assert
        Assert.Equal("https://api.worker.dev/", client.BaseAddress?.ToString());
        Assert.Contains(client.DefaultRequestHeaders, h => 
            h.Key == "User-Agent" && h.Value.Contains("RepoModViewer/0.1"));
        Assert.Contains(client.DefaultRequestHeaders, h => 
            h.Key == "Accept" && h.Value.Contains("application/json"));
    }

    [Fact]
    public void Services_HaveScopedLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IZipDownloader, ZipDownloader>();
        services.AddScoped<IZipIndexer, ZipIndexer>();
        services.AddScoped<IAssetScanner, AssetScanner>();
        services.AddScoped<IAssetRenderer, AssetRenderer>();
        services.AddScoped<IViewerService, ViewerService>();
        services.AddHttpClient();
        
        // ViewerService requires IJSRuntime
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddSingleton(jsRuntimeMock.Object);
        
        var provider = services.BuildServiceProvider();

        // Act - Create two scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        
        var service1 = scope1.ServiceProvider.GetRequiredService<IZipDownloader>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IZipDownloader>();

        // Assert - Different scopes should have different instances
        Assert.NotSame(service1, service2);
    }
}
