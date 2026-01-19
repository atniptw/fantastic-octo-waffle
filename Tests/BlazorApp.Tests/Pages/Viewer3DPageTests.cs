using Xunit;
using Bunit;
using BlazorApp.Pages;

namespace BlazorApp.Tests.Pages;

public class Viewer3DPageTests : Bunit.TestContext
{
    [Fact]
    public void Viewer3D_RendersCanvas_WithCorrectId()
    {
        // Arrange & Act
        var cut = Render<Viewer3D>();

        // Assert
        var canvas = cut.Find("canvas#threeJsCanvas");
        Assert.NotNull(canvas);
        Assert.Contains("width: 100%", canvas.GetAttribute("style"));
        Assert.Contains("height: 600px", canvas.GetAttribute("style"));
    }

    [Fact]
    public void Viewer3D_ShowsPlaceholderFiles()
    {
        // Arrange & Act
        var cut = Render<Viewer3D>();

        // Assert
        var fileList = cut.FindAll("li.list-group-item");
        Assert.True(fileList.Count >= 1, "Should show at least 1 placeholder file");
    }

    [Fact]
    public void Viewer3D_RenderableFiles_DisplayBadge()
    {
        // Arrange & Act
        var cut = Render<Viewer3D>();

        // Assert
        var badges = cut.FindAll(".badge.bg-success");
        Assert.True(badges.Count >= 1, "Should have at least one renderable file with badge");
        Assert.Contains("Renderable", badges[0].TextContent);
    }
}
