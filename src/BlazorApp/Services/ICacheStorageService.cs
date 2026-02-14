using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Service for caching parsed 3D geometry data in browser IndexedDB.
/// Reduces parsing overhead by storing ThreeJsGeometry objects locally.
/// </summary>
public interface ICacheStorageService
{
    /// <summary>
    /// Store parsed geometry in IndexedDB.
    /// </summary>
    /// <param name="modName">Mod identifier (namespace_name).</param>
    /// <param name="fileName">Asset filename.</param>
    /// <param name="geometry">Parsed ThreeJsGeometry object.</param>
    Task SetGeometryAsync(string modName, string fileName, ThreeJsGeometry geometry);

    /// <summary>
    /// Retrieve cached geometry from IndexedDB.
    /// </summary>
    /// <param name="modName">Mod identifier (namespace_name).</param>
    /// <param name="fileName">Asset filename.</param>
    /// <returns>Cached geometry or null if not found or expired.</returns>
    Task<ThreeJsGeometry?> GetGeometryAsync(string modName, string fileName);

    /// <summary>
    /// Clear all cached geometries for a specific mod.
    /// </summary>
    /// <param name="modName">Mod identifier (namespace_name).</param>
    /// <returns>Number of entries deleted.</returns>
    Task<int> ClearModCacheAsync(string modName);

    /// <summary>
    /// Clear all cached geometries (full cache reset).
    /// </summary>
    Task ClearAllCacheAsync();

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    /// <returns>Cache stats (count, oldest/newest timestamps).</returns>
    Task<CacheStats> GetCacheStatsAsync();
}

/// <summary>
/// Cache statistics.
/// </summary>
public sealed record CacheStats(
    int Count,
    long? OldestTimestamp,
    long? NewestTimestamp
);
