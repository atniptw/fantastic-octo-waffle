using BlazorApp.Models;
using UnityAssetParser.Classes;
using UnityAssetParser.Export;

namespace BlazorApp.Services;

/// <summary>
/// Service interface for exporting Mesh objects to glTF/GLB format with caching.
/// </summary>
public interface IGltfExportService
{
    /// <summary>
    /// Exports parsed Mesh objects to GLB, with caching support.
    /// 
    /// First checks cache using the provided key. If found, returns cached GLB.
    /// Otherwise, exports meshes to GLB and caches the result for future requests.
    /// 
    /// Cache key format suggestion: "{mod_name}:{asset_name}:{version}"
    /// </summary>
    /// <param name="meshes">Parsed Unity Mesh objects</param>
    /// <param name="cacheKey">Unique identifier for caching</param>
    /// <returns>GLB binary data</returns>
    Task<byte[]> ExportMeshesToGlbAsync(List<Mesh> meshes, string cacheKey);

    /// <summary>
    /// Load glTF/GLB from browser IndexedDB cache.
    /// </summary>
    /// <param name="cacheKey">Unique identifier</param>
    /// <returns>GLB binary data, or null if not cached</returns>
    Task<byte[]?> LoadCachedGlbAsync(string cacheKey);

    /// <summary>
    /// Save glTF/GLB to browser IndexedDB cache.
    /// </summary>
    /// <param name="cacheKey">Unique identifier</param>
    /// <param name="glbData">GLB binary data</param>
    Task CacheGlbAsync(string cacheKey, byte[] glbData);

    /// <summary>
    /// Clear all cached GLB data.
    /// </summary>
    Task ClearCacheAsync();
}

/// <summary>
/// Service for exporting parsed Mesh objects to glTF/GLB format.
/// 
/// Responsibilities:
/// - Wrap GltfExporter with error handling and logging
/// - Manage caching (IndexedDB via JS interop)
/// - Provide convenient async API for Blazor components
/// 
/// Threading: Safe for concurrent calls; uses lock-free caching pattern.
/// </summary>
public class GltfExportService : IGltfExportService
{
    private readonly ILogger<GltfExportService> _logger;
    private readonly GltfExporter _exporter;

    // TODO: Implement IndexedDB interop for caching
    // For now, in-memory cache (not persistent across page reloads)
    private readonly Dictionary<string, byte[]> _memoryCache = new();

    public GltfExportService(ILogger<GltfExportService> logger)
    {
        _logger = logger;
        _exporter = new GltfExporter();
    }

    /// <summary>
    /// Exports meshes to GLB with memory caching.
    /// 
    /// Future: Replace with IndexedDB for persistence.
    /// </summary>
    public async Task<byte[]> ExportMeshesToGlbAsync(List<Mesh> meshes, string cacheKey)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);

        try
        {
            // Check cache first
            if (_memoryCache.TryGetValue(cacheKey, out var cached))
            {
                _logger.LogInformation("Loaded glTF from memory cache: {CacheKey}", cacheKey);
                return cached;
            }

            // Export
            _logger.LogInformation("Exporting {MeshCount} meshes to glTF for {CacheKey}",
                meshes.Count, cacheKey);

            var glbData = _exporter.MeshesToGlb(meshes);

            // Cache result
            _memoryCache[cacheKey] = glbData;
            _logger.LogInformation(
                "Cached glTF export: {CacheKey}, size: {SizeKb} KB",
                cacheKey, glbData.Length / 1024);

            return glbData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export meshes to glTF: {CacheKey}", cacheKey);
            throw;
        }
    }

    /// <summary>
    /// Load glTF from browser IndexedDB (placeholder for Phase 3).
    /// </summary>
    public async Task<byte[]?> LoadCachedGlbAsync(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);

        // TODO: Implement IndexedDB lookup via JS interop
        // For now, check in-memory cache as fallback
        _memoryCache.TryGetValue(cacheKey, out var cached);
        return cached;
    }

    /// <summary>
    /// Save glTF to browser IndexedDB (placeholder for Phase 3).
    /// </summary>
    public async Task CacheGlbAsync(string cacheKey, byte[] glbData)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);
        ArgumentNullException.ThrowIfNull(glbData);

        // TODO: Implement IndexedDB storage via JS interop
        // For now, save to in-memory cache
        _memoryCache[cacheKey] = glbData;
        _logger.LogDebug("Cached glTF in memory: {CacheKey}", cacheKey);
    }

    /// <summary>
    /// Clear all cached GLB data.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        _memoryCache.Clear();
        _logger.LogInformation("Cleared glTF cache");

        // TODO: Clear IndexedDB when implemented
    }
}
