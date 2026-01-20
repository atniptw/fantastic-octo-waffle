using System.Runtime.CompilerServices;
using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Stub implementation of ZIP downloader. Returns empty/null for all operations.
/// </summary>
/// <remarks>
/// Temporary stub for development. Real implementation will be added in future issue.
/// </remarks>
public class ZipDownloader : IZipDownloader
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipDownloader"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for downloading files.</param>
    /// <exception cref="ArgumentNullException">Thrown when httpClient is null.</exception>
    public ZipDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc/>
    public async Task<DownloadMeta?> GetMetaAsync(PackageVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ct.ThrowIfCancellationRequested();

        // TODO: [Future] Implement HTTP HEAD request to Cloudflare Worker proxy
        // Expected: Extract Content-Length and Content-Disposition from headers
        await Task.CompletedTask;
        return null;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<byte[]> StreamZipAsync(
        PackageVersion version,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ct.ThrowIfCancellationRequested();

        // TODO: [Future] Implement HTTP GET streaming through Cloudflare Worker proxy
        // Expected: Stream response in 64KB-256KB chunks for efficient memory usage
        await Task.CompletedTask;
        yield break;
    }
}
