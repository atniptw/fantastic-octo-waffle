using Xunit;
using Bunit;
using IndexPage = BlazorApp.Pages.Index;

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
}
