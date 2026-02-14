using Xunit;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using IndexPage = BlazorApp.Pages.Index;
using BlazorApp.Services;
using BlazorApp.Models;
using Moq;

namespace BlazorApp.Tests.Pages;

public class IndexPageTests : Bunit.TestContext
{
    [Fact]
    public async Task Index_RendersModList_ShowsPlaceholderMods()
    {
        // Arrange
        var mockThunderstoreService = new Mock<IThunderstoreService>();
        mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage>
            {
                new() { Name = "Mod1", Owner = "Author1", FullName = "Author1-Mod1", Categories = new List<string> { "Cosmetics" }, Versions = new List<PackageVersion> { new() { Description = "Test" } } },
                new() { Name = "Mod2", Owner = "Author2", FullName = "Author2-Mod2", Categories = new List<string> { "Cosmetics" }, Versions = new List<PackageVersion> { new() { Description = "Test" } } }
            });
        Services.AddScoped(_ => mockThunderstoreService.Object);

        // Act
        var cut = Render<IndexPage>();
        await Task.Delay(100); // Wait for async load

        // Assert
        var heading = cut.Find("h1");
        Assert.Equal("Cosmetic Mods", heading.TextContent);
        
        var modCards = cut.FindAll(".card");
        Assert.True(modCards.Count >= 2, "Should show at least 2 placeholder mods");
    }

    [Fact]
    public async Task Index_ModCard_HasCorrectNavigationLink()
    {
        // Arrange
        var mockThunderstoreService = new Mock<IThunderstoreService>();
        mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage>
            {
                new() { Name = "TestMod", Owner = "TestAuthor", FullName = "TestAuthor-TestMod", Categories = new List<string> { "Cosmetics" }, Versions = new List<PackageVersion> { new() { Description = "Test" } } }
            });
        Services.AddScoped(_ => mockThunderstoreService.Object);

        // Act
        var cut = Render<IndexPage>();
        await Task.Delay(100);

        // Assert
        var firstLink = cut.Find("a.btn-primary");
        Assert.Contains("mod/", firstLink.GetAttribute("href"));
    }

    [Fact]
    public void Index_WithInjectedServices_DoesNotThrowDIException()
    {
        // Arrange - register all services that might be needed
        var mockThunderstoreService = new Mock<IThunderstoreService>();
        mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage>());
        
        Services.AddScoped(_ => mockThunderstoreService.Object);
        Services.AddScoped<IZipDownloader, ZipDownloader>();
        Services.AddScoped<IZipIndexer, ZipIndexer>();
        Services.AddScoped<IAssetScanner, AssetScanner>();
        Services.AddScoped<IAssetRenderer, AssetRenderer>();
        Services.AddScoped<IViewerService, ViewerService>();
        Services.AddHttpClient();

        // Act & Assert - Should not throw DI resolution exception
        var cut = Render<IndexPage>();
        Assert.NotNull(cut);
    }
}
