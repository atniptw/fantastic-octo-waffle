using System.Text.Json;
using BlazorApp.Models;
using Microsoft.JSInterop;

namespace BlazorApp.Services;

/// <summary>
/// Service for caching parsed 3D geometry data in browser IndexedDB.
/// Uses JavaScript interop to interact with IndexedDB API.
/// </summary>
public sealed class CacheStorageService : ICacheStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<CacheStorageService> _logger;

    public CacheStorageService(IJSRuntime jsRuntime, ILogger<CacheStorageService> logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Store parsed geometry in IndexedDB.
    /// </summary>
    public async Task SetGeometryAsync(string modName, string fileName, ThreeJsGeometry geometry)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("cacheStorage.setGeometry", modName, fileName, geometry);
            _logger.LogDebug("Cached geometry for {ModName}:{FileName}", modName, fileName);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to cache geometry for {ModName}:{FileName}", modName, fileName);
            // Don't throw - caching is optional, failures shouldn't break the app
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error caching geometry for {ModName}:{FileName}", modName, fileName);
        }
    }

    /// <summary>
    /// Retrieve cached geometry from IndexedDB.
    /// </summary>
    public async Task<ThreeJsGeometry?> GetGeometryAsync(string modName, string fileName)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<JsonElement?>("cacheStorage.getGeometry", modName, fileName);
            
            if (result == null || result.Value.ValueKind == JsonValueKind.Null)
            {
                _logger.LogDebug("No cached geometry found for {ModName}:{FileName}", modName, fileName);
                return null;
            }

            var geometry = JsonSerializer.Deserialize<ThreeJsGeometry>(result.Value.GetRawText());
            _logger.LogDebug("Retrieved cached geometry for {ModName}:{FileName}", modName, fileName);
            return geometry;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached geometry for {ModName}:{FileName}", modName, fileName);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached geometry for {ModName}:{FileName}", modName, fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error retrieving cached geometry for {ModName}:{FileName}", modName, fileName);
            return null;
        }
    }

    /// <summary>
    /// Clear all cached geometries for a specific mod.
    /// </summary>
    public async Task<int> ClearModCacheAsync(string modName)
    {
        try
        {
            var count = await _jsRuntime.InvokeAsync<int>("cacheStorage.clearModCache", modName);
            _logger.LogInformation("Cleared {Count} cached geometries for mod {ModName}", count, modName);
            return count;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache for mod {ModName}", modName);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error clearing cache for mod {ModName}", modName);
            return 0;
        }
    }

    /// <summary>
    /// Clear all cached geometries (full cache reset).
    /// </summary>
    public async Task ClearAllCacheAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("cacheStorage.clearAllCache");
            _logger.LogInformation("Cleared all cached geometries");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to clear all cache");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error clearing all cache");
        }
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public async Task<CacheStats> GetCacheStatsAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<JsonElement>("cacheStorage.getCacheStats");
            var count = result.GetProperty("count").GetInt32();
            var oldest = result.TryGetProperty("oldestTimestamp", out var oldestProp) && oldestProp.ValueKind != JsonValueKind.Null
                ? oldestProp.GetInt64()
                : (long?)null;
            var newest = result.TryGetProperty("newestTimestamp", out var newestProp) && newestProp.ValueKind != JsonValueKind.Null
                ? newestProp.GetInt64()
                : (long?)null;

            return new CacheStats(count, oldest, newest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache stats");
            return new CacheStats(0, null, null);
        }
    }
}
