using System.IO.Compression;
using BlazorApp.Models;
using BlazorApp.Services;

namespace BlazorApp.Tests.Services;

public class ZipIndexerTests
{
    private readonly ZipIndexer _sut = new();

    [Fact]
    public async Task IndexAsync_NullZipStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _sut.IndexAsync(null!))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
        Assert.Equal("zipStream", exception.ParamName);
    }

    [Fact]
    public async Task IndexAsync_EmptyStream_ReturnsEmptyStream()
    {
        // Arrange
        var zipStream = EmptyAsyncEnumerable();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task IndexAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var zipStream = EmptyAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _sut.IndexAsync(zipStream, cts.Token))
            {
                // Intentionally left blank; should throw before enumeration.
            }
        });
    }

    [Fact]
    public async Task IndexAsync_ZipWithTextFile_DetectsUnknownType()
    {
        // Arrange
        var zipStream = CreateZipWithTextFile("test.txt", "Hello World");
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Single(items);
        Assert.Equal("test.txt", items[0].FileName);
        Assert.Equal(FileType.Unknown, items[0].Type);
        Assert.False(items[0].Renderable);
    }

    [Fact]
    public async Task IndexAsync_ZipWithUnityFSFile_DetectsUnityFSType()
    {
        // Arrange
        var unityFSContent = CreateUnityFSFileContent();
        var zipStream = CreateZipWithBinaryFile("asset.hhh", unityFSContent);
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Single(items);
        Assert.Equal("asset.hhh", items[0].FileName);
        Assert.Equal(FileType.UnityFS, items[0].Type);
        // Mock UnityFS files that fail to parse are marked as non-renderable (conservative default)
        Assert.False(items[0].Renderable);
    }

    [Fact]
    public async Task IndexAsync_ZipWithResSFile_DetectsResourceType()
    {
        // Arrange
        var zipStream = CreateZipWithTextFile("data.resS", "resource data");
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Single(items);
        Assert.Equal("data.resS", items[0].FileName);
        Assert.Equal(FileType.Resource, items[0].Type);
        Assert.False(items[0].Renderable); // Resource files are not renderable
    }

    [Fact]
    public async Task IndexAsync_ZipWithMultipleFiles_IndexesAll()
    {
        // Arrange
        var zipStream = CreateZipWithMultipleFiles();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
        
        // Check that all files are indexed
        var fileNames = items.Select(i => i.FileName).ToList();
        Assert.Contains("readme.txt", fileNames);
        Assert.Contains("asset.hhh", fileNames);
        Assert.Contains("data.resS", fileNames);

        // Check types
        var readmeItem = items.First(i => i.FileName == "readme.txt");
        Assert.Equal(FileType.Unknown, readmeItem.Type);

        var assetItem = items.First(i => i.FileName == "asset.hhh");
        Assert.Equal(FileType.UnityFS, assetItem.Type);
        // Mock UnityFS files that fail to parse are marked as non-renderable
        Assert.False(assetItem.Renderable);

        var resSItem = items.First(i => i.FileName == "data.resS");
        Assert.Equal(FileType.Resource, resSItem.Type);
        Assert.False(resSItem.Renderable);
    }

    [Fact]
    public async Task IndexAsync_InvalidZipData_ThrowsInvalidDataException()
    {
        // Arrange
        var zipStream = CreateInvalidZipStream();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in _sut.IndexAsync(zipStream))
            {
                // Should throw before yielding any items
            }
        });
    }

    [Fact]
    public async Task IndexAsync_MultiChunkZipStream_IndexesCorrectly()
    {
        // Arrange - Split ZIP into multiple chunks
        var zipStream = CreateZipWithMultipleChunks();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert - Should successfully parse despite chunk boundaries
        Assert.Single(items);
        Assert.Equal("asset.hhh", items[0].FileName);
        Assert.Equal(FileType.UnityFS, items[0].Type);
        // Mock UnityFS files that fail to parse are marked as non-renderable
        Assert.False(items[0].Renderable);
    }

    [Fact]
    public async Task IndexAsync_UnityWebVariant_DetectsAsUnityFS()
    {
        // Arrange
        var unityWebContent = CreateUnityWebFileContent();
        var zipStream = CreateZipWithBinaryFile("asset.unity3d", unityWebContent);
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Single(items);
        Assert.Equal(FileType.UnityFS, items[0].Type);
        // Mock UnityWeb files that fail to parse are marked as non-renderable
        Assert.False(items[0].Renderable);
    }

    [Fact]
    public async Task IndexAsync_AssetsFile_DetectsAsSerializedFile()
    {
        // Arrange
        var assetsContent = CreateSerializedFileContent();
        var zipStream = CreateZipWithBinaryFile("sharedassets0.assets", assetsContent);
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert
        Assert.Single(items);
        Assert.Equal("sharedassets0.assets", items[0].FileName);
        Assert.Equal(FileType.SerializedFile, items[0].Type);
        Assert.False(items[0].Renderable); // SerializedFile is not renderable on its own
    }

    [Fact]
    public async Task IndexAsync_ZeroLengthFile_IsIndexed()
    {
        // Arrange - ZIP with zero-length file (not a directory)
        var zipStream = CreateZipWithZeroLengthFile();
        var items = new List<FileIndexItem>();

        // Act
        await foreach (var item in _sut.IndexAsync(zipStream))
        {
            items.Add(item);
        }

        // Assert - Zero-length files should be indexed (not skipped as directories)
        Assert.Single(items);
        Assert.Equal("empty.txt", items[0].FileName);
        Assert.Equal(0, items[0].SizeBytes);
    }

    private static async IAsyncEnumerable<byte[]> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<byte[]> CreateZipWithTextFile(string fileName, string content)
    {
        var zipBytes = CreateZipArchive(zip =>
        {
            var entry = zip.CreateEntry(fileName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        });

        await Task.CompletedTask;
        yield return zipBytes;
    }

    private static async IAsyncEnumerable<byte[]> CreateZipWithBinaryFile(string fileName, byte[] content)
    {
        var zipBytes = CreateZipArchive(zip =>
        {
            var entry = zip.CreateEntry(fileName);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        });

        await Task.CompletedTask;
        yield return zipBytes;
    }

    private static async IAsyncEnumerable<byte[]> CreateZipWithMultipleFiles()
    {
        var zipBytes = CreateZipArchive(zip =>
        {
            // Add text file
            var readmeEntry = zip.CreateEntry("readme.txt");
            using (var writer = new StreamWriter(readmeEntry.Open()))
            {
                writer.Write("This is a readme");
            }

            // Add UnityFS file
            var assetEntry = zip.CreateEntry("asset.hhh");
            using (var stream = assetEntry.Open())
            {
                var unityFSContent = CreateUnityFSFileContent();
                stream.Write(unityFSContent, 0, unityFSContent.Length);
            }

            // Add resource file
            var resEntry = zip.CreateEntry("data.resS");
            using (var writer = new StreamWriter(resEntry.Open()))
            {
                writer.Write("resource data");
            }
        });

        await Task.CompletedTask;
        yield return zipBytes;
    }

    private static async IAsyncEnumerable<byte[]> CreateInvalidZipStream()
    {
        await Task.CompletedTask;
        yield return new byte[] { 0x50, 0x4b, 0x03, 0x04 }; // ZIP header
        yield return new byte[] { 0x00, 0x00 }; // Incomplete/corrupt data
    }

    private static async IAsyncEnumerable<byte[]> CreateZipWithMultipleChunks()
    {
        var zipBytes = CreateZipArchive(zip =>
        {
            var entry = zip.CreateEntry("asset.hhh");
            using var stream = entry.Open();
            var unityFSContent = CreateUnityFSFileContent();
            stream.Write(unityFSContent, 0, unityFSContent.Length);
        });

        // Split into multiple chunks to test streaming
        await Task.CompletedTask;
        const int chunkSize = 512;
        for (int i = 0; i < zipBytes.Length; i += chunkSize)
        {
            var remaining = Math.Min(chunkSize, zipBytes.Length - i);
            var chunk = new byte[remaining];
            Array.Copy(zipBytes, i, chunk, 0, remaining);
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<byte[]> CreateZipWithZeroLengthFile()
    {
        var zipBytes = CreateZipArchive(zip =>
        {
            // Create a zero-length file (not a directory)
            var entry = zip.CreateEntry("empty.txt");
            // Don't write anything to it
        });

        await Task.CompletedTask;
        yield return zipBytes;
    }

    private static byte[] CreateZipArchive(Action<ZipArchive> populateArchive)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            populateArchive(archive);
        }
        return ms.ToArray();
    }

    private static byte[] CreateUnityFSFileContent()
    {
        // Create a minimal UnityFS file header for testing
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Magic signature "UnityFS"
        writer.Write("UnityFS\0"u8.ToArray());

        // Format version (example: 6)
        writer.Write(6);

        // Version string (null-terminated)
        writer.Write("2021.3.0f1\0"u8.ToArray());

        // Generation version (null-terminated)
        writer.Write("2021.3.0f1\0"u8.ToArray());

        // File size (8 bytes) - just use current size as placeholder
        writer.Write((long)100);

        return ms.ToArray();
    }

    private static byte[] CreateUnityWebFileContent()
    {
        // Create a minimal UnityWeb file header for testing
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Magic signature "UnityWeb"
        writer.Write("UnityWeb"u8.ToArray());

        // Format version
        writer.Write(6);

        // Version string (null-terminated)
        writer.Write("2021.3.0f1\0"u8.ToArray());

        return ms.ToArray();
    }

    private static byte[] CreateSerializedFileContent()
    {
        // Create a minimal SerializedFile content for testing
        // SerializedFiles have complex structure, but we just need enough
        // bytes to pass the MinSerializedFileSize check
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write some dummy data to meet minimum size requirement
        writer.Write(new byte[20]);

        return ms.ToArray();
    }
}
