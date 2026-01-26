using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Downloads mod ZIP files from Cloudflare Worker.</summary>
public interface IZipDownloader : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Get metadata (size, filename) for a mod version without downloading.
    /// </summary>
    /// <exception cref="HttpRequestException">Network error or 404.</exception>
    Task<DownloadMeta?> GetMetaAsync(PackageVersion version, CancellationToken ct = default);
    
    /// <summary>
    /// Stream ZIP file in chunks (recommended 64KB-256KB per chunk).
    /// </summary>
    /// <exception cref="HttpRequestException">Network error.</exception>
    /// <exception cref="OperationCanceledException">Download cancelled.</exception>
    IAsyncEnumerable<byte[]> StreamZipAsync(
        PackageVersion version, 
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a ZIP file from the Worker's /api/download endpoint.
    /// </summary>
    /// <param name="url">Download URL from Thunderstore package version</param>
    /// <param name="onProgress">Optional callback for progress (downloadedBytes, totalBytes). 
    /// If Content-Length header missing, totalBytes will be 0 (indeterminate progress)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Absolute path to downloaded ZIP file in temp directory.
    /// Caller is responsible for cleanup—delete file when done or call service Dispose()</returns>
    /// <exception cref="HttpRequestException">Network error or timeout</exception>
    /// <exception cref="InvalidOperationException">Invalid ZIP file or insufficient disk space</exception>
    /// <exception cref="IOException">Disk full during write</exception>
    /// <exception cref="OperationCanceledException">Download cancelled</exception>
    Task<string> DownloadAsync(
        Uri url, 
        Action<long, long>? onProgress = null,
        CancellationToken cancellationToken = default
    );
}
