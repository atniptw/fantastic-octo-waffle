using Xunit;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using IndexPage = BlazorApp.Pages.Index;
using BlazorApp.Services;

namespace BlazorApp.Tests.Pages;

public class IndexPageTests : Bunit.TestContext
{
    [Fact]
    public void Index_RendersModList_ShowsPlaceholderMods()
    {
        // Arrange & Act
        var cut = Render<IndexPage>();

        // Assert
        var heading = cut.Find("h1");
        Assert.Equal("Cosmetic Mods", heading.TextContent);
        
        var modCards = cut.FindAll(".card");
        Assert.True(modCards.Count >= 2, "Should show at least 2 placeholder mods");
    }

    [Fact]
    public void Index_ModCard_HasCorrectNavigationLink()
    {
        // Arrange & Act
        var cut = Render<IndexPage>();

        // Assert
        var firstLink = cut.Find("a.btn-primary");
        Assert.Contains("/mod/", firstLink.GetAttribute("href"));
    }

    [Fact]
    public void Index_WithInjectedServices_DoesNotThrowDIException()
    {
        // Arrange - register all services that might be needed
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
