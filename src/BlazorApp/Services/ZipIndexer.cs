using System.Runtime.CompilerServices;
using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Stub implementation of ZIP indexer. Returns empty stream for all operations.
/// </summary>
/// <remarks>
/// Temporary stub for development. Real implementation will be added in future issue.
/// </remarks>
public class ZipIndexer : IZipIndexer
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<FileIndexItem> IndexAsync(
        IAsyncEnumerable<byte[]> zipStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ct.ThrowIfCancellationRequested();

        // TODO: [Future] Implement ZIP parsing and file type detection
        // Expected: Parse ZIP central directory, identify .hhh (UnityFS) files,
        // classify files by type (UnityFS, SerializedFile, Resource), and mark
        // UnityFS files as potentially renderable
        await Task.CompletedTask;
        yield break;
    }
}
