using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Indexes contents of ZIP archives to identify asset files.</summary>
public interface IZipIndexer
{
    /// <summary>
    /// Index ZIP stream and identify file types (UnityFS, SerializedFile, Resource).
    /// </summary>
    /// <param name="zipStream">Streamed ZIP file chunks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of indexed file entries.</returns>
    /// <exception cref="InvalidDataException">Corrupt or invalid ZIP format.</exception>
    IAsyncEnumerable<FileIndexItem> IndexAsync(
        IAsyncEnumerable<byte[]> zipStream,
        CancellationToken ct = default);
}
