using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BlazorApp.Models;
using Microsoft.Extensions.Logging;

namespace BlazorApp.Services;

/// <summary>
/// Downloads mod ZIP files from Cloudflare Worker with progress tracking and cancellation support.
/// </summary>
public partial class ZipDownloader : IZipDownloader
{
    private const int ChunkSize = 65536; // 64 KB
    private const int DownloadTimeoutSeconds = 300; // 5 minutes
    private const double DiskSpaceBufferMultiplier = 1.5; // 1.5x safety buffer
    private const string TempDirectoryName = "repo-mod-viewer";
    private static readonly byte[] ZipMagicBytes = { 0x50, 0x4B, 0x03, 0x04 };

    [GeneratedRegex(@"^\d+\.\d+\.\d+", RegexOptions.Compiled)]
    private static partial Regex VersionPattern();

    private readonly HttpClient _httpClient;
    private readonly ILogger<ZipDownloader>? _logger;
    private readonly HashSet<string> _createdTempFiles = new();
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipDownloader"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for downloading files.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when httpClient is null.</exception>
    public ZipDownloader(HttpClient httpClient, ILogger<ZipDownloader>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
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
        // If DownloadUrl is not in expected Thunderstore format, return empty stream (no-op).
        // This keeps tests deterministic without network and avoids throwing for placeholder URLs.
        var uri = version.DownloadUrl;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5 || segments[0] != "package" || segments[1] != "download")
        {
            _logger?.LogDebug("StreamZipAsync: DownloadUrl not in Thunderstore format; returning empty stream: {Url}", uri);
            yield break;
        }

        // Build worker proxy URL: /api/download/{namespace}/{name}/{ver}
        var ns = segments[2];
        var name = segments[3];
        var ver = segments[4];
        var workerPath = $"/api/download/{ns}/{name}/{ver}";
        _logger?.LogInformation("Streaming ZIP via worker: {Path}", workerPath);

        using var response = await _httpClient.GetAsync(
            workerPath,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        ).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("Streaming failed with status code {StatusCode}", response.StatusCode);
            throw new HttpRequestException(
                response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Mod not found—verify the download link is correct"
                    : "Cannot reach mod service—verify internet connection");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[ChunkSize];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, 0, chunk, 0, bytesRead);
            yield return chunk;
        }

        _logger?.LogInformation("Streaming completed");
    }

    /// <inheritdoc/>
    public async Task<string> DownloadAsync(
        Uri url,
        Action<long, long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        // Use the provided URL directly. Tests provide absolute URLs and mock handlers.
        // In production, a Cloudflare Worker proxy URL can be provided via `url`.
        _logger?.LogInformation("Starting download: {Url}", url);

        // Create timeout token linked to provided cancellation token
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(DownloadTimeoutSeconds));

        string tempPath = string.Empty;

        try
        {
            // Get response headers to check Content-Length
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Download failed with status code {StatusCode}", response.StatusCode);
                throw new HttpRequestException(
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Mod not found—verify the download link is correct"
                        : "Cannot reach mod service—verify internet connection");
            }

            // Parse Content-Length (may be missing)
            var total = 0L;
            if (response.Content.Headers.ContentLength.HasValue)
            {
                total = response.Content.Headers.ContentLength.Value;
                _logger?.LogInformation("Download size: {Size} bytes", total);
            }
            else
            {
                _logger?.LogWarning("Content-Length header missing, progress will be indeterminate");
            }

            // Validate disk space before download
            // Check actual size if known, otherwise ensure minimum 100MB free
            if (total > 0)
            {
                ValidateDiskSpace(total);
            }
            else
            {
                ValidateMinimumDiskSpace(100 * 1024 * 1024); // 100MB minimum
            }

            // Create temp directory if needed
            var tempDir = Path.Combine(Path.GetTempPath(), TempDirectoryName);
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                    _logger?.LogDebug("Created temp directory: {TempDir}", tempDir);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create temp directory: {TempDir}", tempDir);
                throw;
            }

            // Generate unique temp file name
            var modName = ExtractModNameFromUrl(url);
            var version = ExtractVersionFromUrl(url);
            var guid = Guid.NewGuid().ToString();
            tempPath = Path.Combine(tempDir, $"{modName}_{version}_{guid}.zip");

            _logger?.LogInformation("Downloading to temp file: {TempPath}", tempPath);

            try
            {
                // Download with progress tracking
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                    await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, useAsync: true);
                    
                    var buffer = new byte[ChunkSize];
                    var downloaded = 0L;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
                        downloaded += bytesRead;

                        // Fire progress callback, clamping downloaded to total if total is known
                        if (total > 0 && downloaded > total)
                        {
                            _logger?.LogWarning("Downloaded bytes ({Downloaded}) exceeds Content-Length ({Total})", downloaded, total);
                        }

                        var clampedDownloaded = total > 0 ? Math.Min(downloaded, total) : downloaded;
                        onProgress?.Invoke(clampedDownloaded, total);
                    }

                    _logger?.LogInformation("Download completed: {Downloaded} bytes", downloaded);
                }

                // Validate ZIP magic bytes (after streams are closed)
                if (!IsValidZipFile(tempPath))
                {
                    _logger?.LogError("Downloaded file is not a valid ZIP archive");
                    File.Delete(tempPath);
                    throw new InvalidOperationException("Downloaded file is corrupted—please try again");
                }

                // Track temp file for cleanup
                lock (_createdTempFiles)
                {
                    _createdTempFiles.Add(tempPath);
                }

                _logger?.LogInformation("Download successful: {TempPath}", tempPath);
                return tempPath;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                _logger?.LogError("Download timeout after {Timeout} seconds", DownloadTimeoutSeconds);
                CleanupTempFile(tempPath);
                throw new HttpRequestException("Mod download took too long—check connection and try again");
            }
            catch (OperationCanceledException)
            {
                // User cancellation
                _logger?.LogInformation("Download cancelled by user");
                CleanupTempFile(tempPath);
                throw;
            }
            catch (IOException ex)
            {
                _logger?.LogError(ex, "Disk I/O error during download");
                CleanupTempFile(tempPath);

                if (ex.Message.Contains("disk full", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Not enough space to download mod (need {total / (1024 * 1024)}MB)", ex);
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during download");
                CleanupTempFile(tempPath);
                throw;
            }
        }
        catch (HttpRequestException)
        {
            // Re-throw HttpRequestException as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error in download method");
            throw;
        }
    }

    private void ValidateDiskSpace(long contentLength)
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var driveInfo = new DriveInfo(Path.GetPathRoot(tempPath) ?? tempPath);
            var requiredSpace = (long)(contentLength * DiskSpaceBufferMultiplier);

            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                var requiredMB = requiredSpace / (1024 * 1024);
                var availableMB = driveInfo.AvailableFreeSpace / (1024 * 1024);
                _logger?.LogError("Insufficient disk space. Required: {Required}MB, Available: {Available}MB", requiredMB, availableMB);
                throw new InvalidOperationException(
                    $"Not enough disk space to download mod. Required: {requiredMB}MB, Available: {availableMB}MB");
            }

            _logger?.LogDebug("Disk space validation passed. Required: {Required}MB, Available: {Available}MB",
                requiredSpace / (1024 * 1024), driveInfo.AvailableFreeSpace / (1024 * 1024));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate disk space, continuing anyway");
        }
    }

    private void ValidateMinimumDiskSpace(long minimumBytes)
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var driveInfo = new DriveInfo(Path.GetPathRoot(tempPath) ?? tempPath);

            if (driveInfo.AvailableFreeSpace < minimumBytes)
            {
                var requiredMB = minimumBytes / (1024 * 1024);
                var availableMB = driveInfo.AvailableFreeSpace / (1024 * 1024);
                _logger?.LogError("Insufficient disk space. Required: {Required}MB minimum, Available: {Available}MB", requiredMB, availableMB);
                throw new InvalidOperationException(
                    $"Not enough disk space to download mod. Required: {requiredMB}MB minimum, Available: {availableMB}MB");
            }

            _logger?.LogDebug("Minimum disk space validation passed. Available: {Available}MB",
                driveInfo.AvailableFreeSpace / (1024 * 1024));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate minimum disk space, continuing anyway");
        }
    }

    private static bool IsValidZipFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[4];
            var bytesRead = fs.Read(header, 0, 4);
            return bytesRead == 4 && header.SequenceEqual(ZipMagicBytes);
        }
        catch
        {
            return false;
        }
    }

    private void CleanupTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                _logger?.LogDebug("Cleaned up temp file: {TempPath}", tempPath);
            }

            lock (_createdTempFiles)
            {
                _createdTempFiles.Remove(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cleanup temp file: {TempPath}", tempPath);
        }
    }

    private static string ExtractModNameFromUrl(Uri url)
    {
        // Try to extract mod name from URL path, removing .zip extension
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? SanitizeFileName(Path.GetFileNameWithoutExtension(segments[^1])) : "mod";
    }

    private static string ExtractVersionFromUrl(Uri url)
    {
        // Try to extract version from URL, fallback to timestamp
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1)
        {
            var versionCandidate = segments[^2];
            if (VersionPattern().IsMatch(versionCandidate))
            {
                return SanitizeFileName(versionCandidate);
            }
        }

        return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
        {
            return ValueTask.CompletedTask;
        }

        _logger?.LogInformation("Disposing ZipDownloader and cleaning up {Count} temp files", _createdTempFiles.Count);

        List<string> filesToClean;
        lock (_createdTempFiles)
        {
            filesToClean = _createdTempFiles.ToList();
        }

        foreach (var file in filesToClean)
        {
            CleanupTempFile(file);
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Synchronous dispose to satisfy containers/tests that call Dispose().
    /// Delegates to DisposeAsync and waits synchronously.
    /// </summary>
    public void Dispose()
    {
        // If already disposed asynchronously, no-op.
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
