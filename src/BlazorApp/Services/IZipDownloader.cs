using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Downloads mod ZIP files from Cloudflare Worker.</summary>
public interface IZipDownloader
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
}
