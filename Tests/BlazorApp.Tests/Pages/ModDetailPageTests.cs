using Xunit;
using Bunit;
using BlazorApp.Pages;

namespace BlazorApp.Tests.Pages;

public class ModDetailPageTests : Bunit.TestContext
{
    [Fact]
    public void ModDetail_RendersModName_FromRouteParameters()
    {
        // Arrange & Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.Namespace, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        // Assert
        var heading = cut.Find("h1");
        Assert.Equal("Cigar", heading.TextContent);
    }

    [Fact]
    public void ModDetail_PreviewButton_Exists()
    {
        // Arrange & Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.Namespace, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        // Assert
        var previewButton = cut.Find("button.btn-success");
        Assert.Contains("Preview", previewButton.TextContent);
    }

    [Fact]
    public void ModDetail_BackLink_NavigatesToIndex()
    {
        // Arrange & Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.Namespace, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        // Assert
        var backLink = cut.Find("a[href='/']");
        Assert.NotNull(backLink);
    }
}
