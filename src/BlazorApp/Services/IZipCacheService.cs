using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Caches ZIP archives in browser storage and exposes entry listing/extraction.
/// </summary>
public interface IZipCacheService
{
    /// <summary>
    /// Download ZIP and store it in the browser cache by key.
    /// </summary>
    Task CacheZipAsync(
        PackageVersion version,
        string cacheKey,
        Action<long, long>? onProgress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Check if a ZIP cache entry exists.
    /// </summary>
    Task<bool> HasZipAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// List .hhh files from cached ZIP.
    /// </summary>
    Task<IReadOnlyList<FileIndexItem>> ListHhhFilesAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Extract a single file by path from cached ZIP.
    /// </summary>
    Task<byte[]> GetFileBytesAsync(string cacheKey, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Remove cached ZIP entry by key.
    /// </summary>
    Task ClearAsync(string cacheKey, CancellationToken ct = default);
}
