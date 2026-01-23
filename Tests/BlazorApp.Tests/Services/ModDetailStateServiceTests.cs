using Xunit;
using BlazorApp.Services;
using BlazorApp.Models;

namespace BlazorApp.Tests.Services;

public class ModDetailStateServiceTests
{
    [Fact]
    public async Task SetCurrentModAsync_StoresState()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId = "TestAuthor_Cigar";
        var fileIndex = new List<FileIndexItem>
        {
            new("test.hhh", 1024, FileType.UnityFS, true)
        };
        var metadata = new ThunderstorePackage
        {
            Name = "Cigar",
            Owner = "TestAuthor",
            FullName = "TestAuthor-Cigar",
            Categories = new List<string> { "Cosmetics" },
            Versions = new List<PackageVersion>()
        };

        // Act
        await service.SetCurrentModAsync(modId, fileIndex, metadata);
        var state = await service.GetCurrentModAsync();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(modId, state.ModId);
        Assert.Equal(fileIndex.Count, state.FileIndex.Count);
        Assert.Equal(metadata.Name, state.Metadata.Name);
    }

    [Fact]
    public async Task GetCurrentModAsync_ReturnsNull_WhenNoStateSet()
    {
        // Arrange
        var service = new ModDetailStateService();

        // Act
        var state = await service.GetCurrentModAsync();

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task ClearAsync_RemovesState()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId = "TestAuthor_Cigar";
        var fileIndex = new List<FileIndexItem>();
        var metadata = new ThunderstorePackage
        {
            Name = "Cigar",
            Owner = "TestAuthor",
            FullName = "TestAuthor-Cigar",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };

        await service.SetCurrentModAsync(modId, fileIndex, metadata);

        // Act
        await service.ClearAsync();
        var state = await service.GetCurrentModAsync();

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task SetCurrentModAsync_ThrowsArgumentNullException_WhenModIdIsNull()
    {
        // Arrange
        var service = new ModDetailStateService();
        var fileIndex = new List<FileIndexItem>();
        var metadata = new ThunderstorePackage
        {
            Name = "Cigar",
            Owner = "TestAuthor",
            FullName = "TestAuthor-Cigar",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.SetCurrentModAsync(null!, fileIndex, metadata)
        );
    }

    [Fact]
    public async Task SetCurrentModAsync_ThrowsArgumentNullException_WhenFileIndexIsNull()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId = "TestAuthor_Cigar";
        var metadata = new ThunderstorePackage
        {
            Name = "Cigar",
            Owner = "TestAuthor",
            FullName = "TestAuthor-Cigar",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.SetCurrentModAsync(modId, null!, metadata)
        );
    }

    [Fact]
    public async Task SetCurrentModAsync_ThrowsArgumentNullException_WhenMetadataIsNull()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId = "TestAuthor_Cigar";
        var fileIndex = new List<FileIndexItem>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.SetCurrentModAsync(modId, fileIndex, null!)
        );
    }

    [Fact]
    public async Task SetCurrentModAsync_OverwritesPreviousState()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId1 = "Author1_Mod1";
        var modId2 = "Author2_Mod2";
        var fileIndex = new List<FileIndexItem>();
        var metadata1 = new ThunderstorePackage
        {
            Name = "Mod1",
            Owner = "Author1",
            FullName = "Author1-Mod1",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };
        var metadata2 = new ThunderstorePackage
        {
            Name = "Mod2",
            Owner = "Author2",
            FullName = "Author2-Mod2",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };

        // Act
        await service.SetCurrentModAsync(modId1, fileIndex, metadata1);
        await service.SetCurrentModAsync(modId2, fileIndex, metadata2);
        var state = await service.GetCurrentModAsync();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(modId2, state.ModId);
        Assert.Equal("Mod2", state.Metadata.Name);
    }

    [Fact]
    public async Task SetCurrentModAsync_SetsCreatedAtTimestamp()
    {
        // Arrange
        var service = new ModDetailStateService();
        var modId = "TestAuthor_Cigar";
        var fileIndex = new List<FileIndexItem>();
        var metadata = new ThunderstorePackage
        {
            Name = "Cigar",
            Owner = "TestAuthor",
            FullName = "TestAuthor-Cigar",
            Categories = new List<string>(),
            Versions = new List<PackageVersion>()
        };
        var beforeTime = DateTime.UtcNow;

        // Act
        await service.SetCurrentModAsync(modId, fileIndex, metadata);
        var state = await service.GetCurrentModAsync();
        var afterTime = DateTime.UtcNow;

        // Assert
        Assert.NotNull(state);
        Assert.True(state.CreatedAt >= beforeTime);
        Assert.True(state.CreatedAt <= afterTime);
    }
}
