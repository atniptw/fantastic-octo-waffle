using System.Net.Http.Json;
using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Service for fetching package data from Thunderstore API via Cloudflare Worker.
/// Implements in-memory caching to reduce redundant network calls.
/// </summary>
public sealed class ThunderstoreService : IThunderstoreService
{
    private readonly HttpClient _httpClient;
    private List<ThunderstorePackage>? _cachedPackages;
    private DateTime? _cacheTimestamp;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public ThunderstoreService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Fetches all packages from the Thunderstore API.
    /// Uses in-memory caching to avoid redundant network calls.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>List of packages from the API, or empty list if none found.</returns>
    public async Task<List<ThunderstorePackage>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        // Check if cache is valid
        if (_cachedPackages != null && _cacheTimestamp != null)
        {
            var age = DateTime.UtcNow - _cacheTimestamp.Value;
            if (age < _cacheLifetime)
            {
                return _cachedPackages;
            }
        }

        // Acquire lock to prevent concurrent fetches
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock (another thread might have fetched)
            if (_cachedPackages != null && _cacheTimestamp != null)
            {
                var age = DateTime.UtcNow - _cacheTimestamp.Value;
                if (age < _cacheLifetime)
                {
                    return _cachedPackages;
                }
            }

            // Fetch from API
            var response = await _httpClient.GetAsync("/api/packages", cancellationToken);
            response.EnsureSuccessStatusCode();
            var packages = await response.Content.ReadFromJsonAsync<List<ThunderstorePackage>>(cancellationToken) ?? new List<ThunderstorePackage>();
            
            // Update cache
            _cachedPackages = packages;
            _cacheTimestamp = DateTime.UtcNow;
            
            return packages;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Fetches a specific package by namespace and name.
    /// Uses cached package list to avoid redundant API calls.
    /// </summary>
    /// <param name="namespace">The package namespace (owner).</param>
    /// <param name="name">The package name.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The package if found, or null.</returns>
    public async Task<ThunderstorePackage?> GetPackageByNameAsync(string @namespace, string name, CancellationToken cancellationToken = default)
    {
        var packages = await GetPackagesAsync(cancellationToken);
        return packages.FirstOrDefault(p => 
            string.Equals(p.Owner, @namespace, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
