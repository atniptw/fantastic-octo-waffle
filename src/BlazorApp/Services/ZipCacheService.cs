using System.Text.Json;
using BlazorApp.Models;
using Microsoft.JSInterop;

namespace BlazorApp.Services;

/// <summary>
/// Stores ZIP archives in IndexedDB via JS interop and enables entry listing/extraction.
/// </summary>
internal sealed class ZipCacheService : IZipCacheService
{
    private const int CacheChunkSize = 256 * 1024; // 256KB chunks for IndexedDB storage

    private readonly IZipDownloader _zipDownloader;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ZipCacheService>? _logger;

    public ZipCacheService(
        IZipDownloader zipDownloader,
        IJSRuntime jsRuntime,
        ILogger<ZipCacheService>? logger = null)
    {
        _zipDownloader = zipDownloader ?? throw new ArgumentNullException(nameof(zipDownloader));
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger;
    }

    public async Task CacheZipAsync(
        PackageVersion version,
        string cacheKey,
        Action<long, long>? onProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ct.ThrowIfCancellationRequested();

        var totalBytes = version.FileSize ?? 0;
        var downloadedBytes = 0L;
        var chunkIndex = 0;
        var buffer = new byte[CacheChunkSize];
        var bufferOffset = 0;

        await _jsRuntime.InvokeVoidAsync("zipCache.start", ct, cacheKey);

        await foreach (var chunk in _zipDownloader.StreamZipAsync(version, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var chunkOffset = 0;
            while (chunkOffset < chunk.Length)
            {
                var remaining = CacheChunkSize - bufferOffset;
                var toCopy = Math.Min(remaining, chunk.Length - chunkOffset);
                Array.Copy(chunk, chunkOffset, buffer, bufferOffset, toCopy);
                bufferOffset += toCopy;
                chunkOffset += toCopy;

                if (bufferOffset == CacheChunkSize)
                {
                    var chunkToStore = new byte[bufferOffset];
                    Array.Copy(buffer, 0, chunkToStore, 0, bufferOffset);
                    await _jsRuntime.InvokeVoidAsync("zipCache.appendChunk", ct, cacheKey, chunkIndex, chunkToStore);
                    downloadedBytes += bufferOffset;
                    bufferOffset = 0;
                    chunkIndex++;
                    onProgress?.Invoke(downloadedBytes, totalBytes);
                }
            }
        }

        if (bufferOffset > 0)
        {
            var chunkToStore = new byte[bufferOffset];
            Array.Copy(buffer, 0, chunkToStore, 0, bufferOffset);
            await _jsRuntime.InvokeVoidAsync("zipCache.appendChunk", ct, cacheKey, chunkIndex, chunkToStore);
            downloadedBytes += bufferOffset;
            chunkIndex++;
            onProgress?.Invoke(downloadedBytes, totalBytes);
        }

        await _jsRuntime.InvokeVoidAsync("zipCache.finalize", ct, cacheKey, downloadedBytes, CacheChunkSize, chunkIndex);
        _logger?.LogInformation("ZIP cached: {CacheKey} ({Bytes} bytes, {Chunks} chunks)", cacheKey, downloadedBytes, chunkIndex);
    }

    public async Task<bool> HasZipAsync(string cacheKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        return await _jsRuntime.InvokeAsync<bool>("zipCache.hasZip", ct, cacheKey);
    }

    public async Task<IReadOnlyList<FileIndexItem>> ListHhhFilesAsync(string cacheKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var data = await _jsRuntime.InvokeAsync<JsonElement>("zipCache.listHhhFiles", ct, cacheKey);
        if (data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FileIndexItem>();
        }

        var results = new List<FileIndexItem>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var path = pathProp.GetString();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var sizeBytes = 0L;
            if (item.TryGetProperty("sizeBytes", out var sizeProp) && sizeProp.TryGetInt64(out var sizeValue))
            {
                sizeBytes = sizeValue;
            }

            results.Add(new FileIndexItem(path, sizeBytes, FileType.UnityFS, true));
        }

        return results;
    }

    public async Task<byte[]> GetFileBytesAsync(string cacheKey, string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return await _jsRuntime.InvokeAsync<byte[]>("zipCache.getFileBytes", ct, cacheKey, filePath);
    }

    public async Task ClearAsync(string cacheKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        await _jsRuntime.InvokeVoidAsync("zipCache.clear", ct, cacheKey);
    }
}
