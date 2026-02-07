using Xunit;
using Bunit;
using BlazorApp.Pages;
using BlazorApp.Services;
using BlazorApp.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BlazorApp.Tests.Pages;

public class Viewer3DPageTests : Bunit.TestContext
{
    private readonly Mock<IModDetailStateService> _stateServiceMock;
    private readonly Mock<IAssetRenderer> _assetRendererMock;
    private readonly Mock<IViewerService> _viewerServiceMock;

    public Viewer3DPageTests()
    {
        _stateServiceMock = new Mock<IModDetailStateService>();
        _assetRendererMock = new Mock<IAssetRenderer>();
        _viewerServiceMock = new Mock<IViewerService>();

        Services.AddSingleton(_stateServiceMock.Object);
        Services.AddSingleton(_assetRendererMock.Object);
        Services.AddSingleton(_viewerServiceMock.Object);
    }

    [Fact]
    public void Viewer3D_WithoutState_ShowsError()
    {
        // Arrange
        _stateServiceMock.Setup(s => s.GetCurrentModAsync()).ReturnsAsync((ModDetailState?)null);
        
        // Act
        var cut = Render<Viewer3D>(parameters => parameters
            .Add(p => p.Owner, "TestAuthor")
            .Add(p => p.Name, "TestMod")
            .Add(p => p.FileName, "test.hhh"));

        // Wait for async initialization
        cut.WaitForState(() => cut.Markup.Contains("alert-danger"), TimeSpan.FromSeconds(2));

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.NotNull(errorAlert);
        Assert.Contains("Mod data not found", errorAlert.TextContent);
    }

    [Fact]
    public void Viewer3D_WithFileNotFound_ShowsError()
    {
        // Arrange
        var state = CreateMockState();
        _stateServiceMock.Setup(s => s.GetCurrentModAsync()).ReturnsAsync(state);

        // Act
        var cut = Render<Viewer3D>(parameters => parameters
            .Add(p => p.Owner, "TestAuthor")
            .Add(p => p.Name, "TestMod")
            .Add(p => p.FileName, "nonexistent.hhh"));

        // Wait for async initialization
        cut.WaitForState(() => cut.Markup.Contains("alert-danger"), TimeSpan.FromSeconds(2));

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.NotNull(errorAlert);
        Assert.Contains("not found in mod archive", errorAlert.TextContent);
    }

    private ModDetailState CreateMockState()
    {
        return new ModDetailState
        {
            ModId = "TestAuthor_TestMod",
            FileIndex = new List<FileIndexItem>
            {
                new("test.hhh", 1024, FileType.UnityFS, true)
            },
            Metadata = new ThunderstorePackage
            {
                Name = "TestMod",
                Owner = "TestAuthor",
                FullName = "TestAuthor-TestMod",
                Categories = new List<string> { "Cosmetics" },
                Versions = new List<PackageVersion>()
            },
            ZipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 } // ZIP magic bytes
        };
    }
}
