using Bunit;
using BlazorApp.Layout;

namespace BlazorApp.Tests.Layout;

/// <summary>
/// Tests for the NavMenu component.
/// Validates navigation links and menu structure.
/// </summary>
public class NavMenuTests : Bunit.TestContext
{
    [Fact]
    public void NavMenu_ContainsHomeLink()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();
        var homeLink = cut.Find("a.nav-link[href='']");

        // Assert
        Assert.Contains("Home", homeLink.TextContent);
    }

    [Fact]
    public void NavMenu_ContainsSampleModLink()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();
        var modLink = cut.Find("a[href='mod/TestAuthor/Cigar']");

        // Assert
        Assert.Contains("Sample Mod", modLink.TextContent);
    }

    [Fact]
    public void NavMenu_HasCorrectNumberOfLinks()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();
        var allLinks = cut.FindAll("a.nav-link");

        // Assert - Should have Home and Sample Mod links
        Assert.Equal(2, allLinks.Count);
    }

    [Fact]
    public void NavMenu_ToggleButton_Exists()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();
        var toggleButton = cut.Find("button.navbar-toggler");

        // Assert
        Assert.NotNull(toggleButton);
        Assert.Equal("Navigation menu", toggleButton.GetAttribute("title"));
    }
}
