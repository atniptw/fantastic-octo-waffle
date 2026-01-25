using BlazorApp.Models;

namespace BlazorApp.Tests.Models;

public class FileIndexTests
{
    [Fact]
    public void Constructor_WithItems_CreatesFileIndex()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("file1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("file2.txt", 50, FileType.Unknown, false)
        };

        // Act
        var fileIndex = new FileIndex(items);

        // Assert
        Assert.NotNull(fileIndex.Items);
        Assert.Equal(2, fileIndex.Items.Count);
    }

    [Fact]
    public void Constructor_WithNullItems_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FileIndex(null!));
    }

    [Fact]
    public void GetRenderableFiles_WithRenderableItems_ReturnsOnlyRenderable()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true),
            new FileIndexItem("readme.txt", 10, FileType.Unknown, false)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var renderableFiles = fileIndex.GetRenderableFiles().ToList();

        // Assert
        Assert.Equal(2, renderableFiles.Count);
        Assert.All(renderableFiles, item => Assert.True(item.Renderable));
        Assert.Contains(renderableFiles, item => item.FileName == "mesh1.hhh");
        Assert.Contains(renderableFiles, item => item.FileName == "mesh2.hhh");
    }

    [Fact]
    public void GetRenderableFiles_WithNoRenderableItems_ReturnsEmpty()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("readme.txt", 10, FileType.Unknown, false)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var renderableFiles = fileIndex.GetRenderableFiles().ToList();

        // Assert
        Assert.Empty(renderableFiles);
    }

    [Fact]
    public void GetNonRenderableFiles_WithNonRenderableItems_ReturnsOnlyNonRenderable()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true),
            new FileIndexItem("readme.txt", 10, FileType.Unknown, false)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var nonRenderableFiles = fileIndex.GetNonRenderableFiles().ToList();

        // Assert
        Assert.Equal(2, nonRenderableFiles.Count);
        Assert.All(nonRenderableFiles, item => Assert.False(item.Renderable));
        Assert.Contains(nonRenderableFiles, item => item.FileName == "texture.resS");
        Assert.Contains(nonRenderableFiles, item => item.FileName == "readme.txt");
    }

    [Fact]
    public void GetNonRenderableFiles_WithAllRenderableItems_ReturnsEmpty()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var nonRenderableFiles = fileIndex.GetNonRenderableFiles().ToList();

        // Assert
        Assert.Empty(nonRenderableFiles);
    }

    [Fact]
    public void RenderableCount_WithMixedItems_ReturnsCorrectCount()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true),
            new FileIndexItem("readme.txt", 10, FileType.Unknown, false)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var count = fileIndex.RenderableCount;

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void RenderableCount_WithNoRenderableItems_ReturnsZero()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("readme.txt", 10, FileType.Unknown, false)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var count = fileIndex.RenderableCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void RenderableCount_WithAllRenderableItems_ReturnsCorrectCount()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true),
            new FileIndexItem("mesh3.hhh", 300, FileType.UnityFS, true)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var count = fileIndex.RenderableCount;

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void TotalCount_WithItems_ReturnsCorrectCount()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("mesh1.hhh", 100, FileType.UnityFS, true),
            new FileIndexItem("texture.resS", 50, FileType.Resource, false),
            new FileIndexItem("mesh2.hhh", 200, FileType.UnityFS, true)
        };
        var fileIndex = new FileIndex(items);

        // Act
        var count = fileIndex.TotalCount;

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void TotalCount_WithEmptyItems_ReturnsZero()
    {
        // Arrange
        var items = Array.Empty<FileIndexItem>();
        var fileIndex = new FileIndex(items);

        // Act
        var count = fileIndex.TotalCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void Items_IsReadOnly_CannotBeModified()
    {
        // Arrange
        var items = new[]
        {
            new FileIndexItem("file1.hhh", 100, FileType.UnityFS, true)
        };
        var fileIndex = new FileIndex(items);

        // Act & Assert
        Assert.IsAssignableFrom<IReadOnlyList<FileIndexItem>>(fileIndex.Items);
    }

    [Fact]
    public void PerformanceBaseline_10Files_CompletesQuickly()
    {
        // Arrange - Create 10 file items (typical mod scenario)
        var items = Enumerable.Range(0, 10).Select(i => new FileIndexItem(
            $"file{i}.hhh",
            1000 + i,
            FileType.UnityFS,
            i % 2 == 0 // Half renderable, half not
        )).ToArray();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileIndex = new FileIndex(items);
        var renderableCount = fileIndex.RenderableCount;
        var renderableFiles = fileIndex.GetRenderableFiles().ToList();
        var nonRenderableFiles = fileIndex.GetNonRenderableFiles().ToList();
        sw.Stop();

        // Assert - Operations should complete in well under 500ms (aim for <10ms)
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"FileIndex operations took {sw.ElapsedMilliseconds}ms, should be <100ms");
        Assert.Equal(5, renderableCount);
        Assert.Equal(5, renderableFiles.Count);
        Assert.Equal(5, nonRenderableFiles.Count);
    }
}
