using Xunit;
using Bunit;
using BlazorApp.Pages;
using BlazorApp.Services;
using BlazorApp.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Runtime.CompilerServices;

namespace BlazorApp.Tests.Pages;

public class ModDetailPageTests : Bunit.TestContext
{
    private readonly Mock<IThunderstoreService> _mockThunderstoreService;
    private readonly Mock<IZipDownloader> _mockZipDownloader;
    private readonly Mock<IZipIndexer> _mockZipIndexer;
    private readonly Mock<IModDetailStateService> _mockStateService;

    public ModDetailPageTests()
    {
        _mockThunderstoreService = new Mock<IThunderstoreService>();
        _mockZipDownloader = new Mock<IZipDownloader>();
        _mockZipIndexer = new Mock<IZipIndexer>();
        _mockStateService = new Mock<IModDetailStateService>();

        // Register mocked services
        Services.AddScoped(_ => _mockThunderstoreService.Object);
        Services.AddScoped(_ => _mockZipDownloader.Object);
        Services.AddScoped(_ => _mockZipIndexer.Object);
        Services.AddScoped(_ => _mockStateService.Object);
    }

    [Fact]
    public async Task ModDetail_RendersPackageMetadata_WithValidMod()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100); // Allow async initialization

        // Assert
        var heading = cut.Find("h1");
        Assert.Equal("Cigar", heading.TextContent);
        Assert.Contains("TestAuthor", cut.Markup);
    }

    [Fact]
    public async Task ModDetail_ShowsErrorMessage_WhenModNotFound()
    {
        // Arrange
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage>());

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "NonExistent")
            .Add(p => p.Name, "Mod"));

        await Task.Delay(100);

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("Mod not found", errorAlert.TextContent);
    }

    [Fact]
    public async Task ModDetail_ShowsDownloadButton_InIdleState()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Assert
        var downloadButton = cut.Find("button.btn-primary");
        Assert.Contains("Download", downloadButton.TextContent);
    }

    [Fact]
    public async Task ModDetail_DisplaysBreadcrumbNavigation()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Assert
        var breadcrumb = cut.Find(".breadcrumb");
        Assert.NotNull(breadcrumb);
        var homeLink = cut.Find("a[href='/']");
        Assert.Contains("Cosmetics", homeLink.TextContent);
    }

    [Fact]
    public async Task ModDetail_DisplaysRatingBadge()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Assert
        Assert.Contains("⭐", cut.Markup);
    }

    [Fact]
    public async Task ModDetail_DisplaysCategories()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Assert
        Assert.Contains("Cosmetics", cut.Markup);
    }

    [Fact]
    public async Task ModDetail_ShowsEmptyFileIndexMessage_WhenNoFiles()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        _mockZipDownloader
            .Setup(d => d.StreamZipAsync(It.IsAny<PackageVersion>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        _mockZipIndexer
            .Setup(i => i.IndexAsync(It.IsAny<IAsyncEnumerable<byte[]>>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyFileIndexEnumerable());

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Click download button
        var downloadButton = cut.Find("button.btn-primary");
        downloadButton.Click();

        await Task.Delay(200); // Wait for async download/index

        // Assert
        Assert.Contains("No files found", cut.Markup);
    }

    [Fact]
    public async Task ModDetail_ShowsNoRenderableMessage_WhenNoRenderableFiles()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        _mockZipDownloader
            .Setup(d => d.StreamZipAsync(It.IsAny<PackageVersion>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        var fileIndex = CreateFileIndexWithNonRenderableFiles();
        _mockZipIndexer
            .Setup(i => i.IndexAsync(It.IsAny<IAsyncEnumerable<byte[]>>(), It.IsAny<CancellationToken>()))
            .Returns(fileIndex);

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Click download button
        var downloadButton = cut.Find("button.btn-primary");
        downloadButton.Click();

        await Task.Delay(200);

        // Assert
        Assert.Contains("No 3D models", cut.Markup);
    }

    [Fact]
    public async Task ModDetail_DisplaysFileTable_WithRenderableFiles()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        _mockZipDownloader
            .Setup(d => d.StreamZipAsync(It.IsAny<PackageVersion>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        var fileIndex = CreateFileIndexWithRenderableFiles();
        _mockZipIndexer
            .Setup(i => i.IndexAsync(It.IsAny<IAsyncEnumerable<byte[]>>(), It.IsAny<CancellationToken>()))
            .Returns(fileIndex);

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Click download button
        var downloadButton = cut.Find("button.btn-primary");
        downloadButton.Click();

        await Task.Delay(200);

        // Assert
        Assert.Contains("test.hhh", cut.Markup);
        Assert.Contains("📦", cut.Markup); // Icon for renderable files
        var previewButtons = cut.FindAll("button.btn-sm.btn-primary");
        Assert.NotEmpty(previewButtons);
    }

    [Fact]
    public async Task ModDetail_HighlightsRenderableFiles_InTable()
    {
        // Arrange
        var testPackage = CreateTestPackage("TestAuthor", "Cigar");
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThunderstorePackage> { testPackage });

        _mockZipDownloader
            .Setup(d => d.StreamZipAsync(It.IsAny<PackageVersion>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        var fileIndex = CreateMixedFileIndex();
        _mockZipIndexer
            .Setup(i => i.IndexAsync(It.IsAny<IAsyncEnumerable<byte[]>>(), It.IsAny<CancellationToken>()))
            .Returns(fileIndex);

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        var downloadButton = cut.Find("button.btn-primary");
        downloadButton.Click();

        await Task.Delay(200);

        // Assert
        var highlightedRows = cut.FindAll("tr.table-light");
        Assert.NotEmpty(highlightedRows);
        
        var mutedRows = cut.FindAll("tr.text-muted");
        Assert.NotEmpty(mutedRows);
    }

    [Fact]
    public async Task ModDetail_ShowsErrorAlert_OnNetworkFailure()
    {
        // Arrange
        _mockThunderstoreService
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var cut = Render<ModDetail>(parameters => parameters
            .Add(p => p.OwnerName, "TestAuthor")
            .Add(p => p.Name, "Cigar"));

        await Task.Delay(100);

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("Failed to load mod details", errorAlert.TextContent);
    }

    // Helper methods
    private static ThunderstorePackage CreateTestPackage(string owner, string name)
    {
        return new ThunderstorePackage
        {
            Name = name,
            Owner = owner,
            FullName = $"{owner}-{name}",
            Categories = new List<string> { "Cosmetics" },
            RatingScore = 5,
            Icon = new Uri("https://example.com/icon.png"),
            Versions = new List<PackageVersion>
            {
                new()
                {
                    Name = $"{owner}-{name}",
                    VersionNumber = "1.0.0",
                    Description = "Test mod description",
                    DownloadUrl = new Uri("https://example.com/mod.zip"),
                    FileSize = 1024000,
                    Downloads = 100
                }
            }
        };
    }

    private static async IAsyncEnumerable<byte[]> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<FileIndexItem> EmptyFileIndexEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<FileIndexItem> CreateFileIndexWithNonRenderableFiles()
    {
        await Task.CompletedTask;
        yield return new FileIndexItem("readme.txt", 100, FileType.Unknown, false);
        yield return new FileIndexItem("manifest.json", 200, FileType.Unknown, false);
    }

    private static async IAsyncEnumerable<FileIndexItem> CreateFileIndexWithRenderableFiles()
    {
        await Task.CompletedTask;
        yield return new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);
    }

    private static async IAsyncEnumerable<FileIndexItem> CreateMixedFileIndex()
    {
        await Task.CompletedTask;
        yield return new FileIndexItem("test.hhh", 1024, FileType.UnityFS, true);
        yield return new FileIndexItem("readme.txt", 100, FileType.Unknown, false);
        yield return new FileIndexItem("model2.hhh", 2048, FileType.UnityFS, true);
    }
}
