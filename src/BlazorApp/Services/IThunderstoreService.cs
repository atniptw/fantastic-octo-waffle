using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Service for fetching package data from Thunderstore API via Cloudflare Worker.
/// </summary>
public interface IThunderstoreService
{
    /// <summary>
    /// Fetches all packages from the Thunderstore API.
    /// Uses in-memory caching to avoid redundant network calls.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>List of packages from the API, or empty list if none found.</returns>
    Task<List<ThunderstorePackage>> GetPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a specific package by namespace and name.
    /// Uses cached package list to avoid redundant API calls.
    /// </summary>
    /// <param name="namespace">The package namespace (owner).</param>
    /// <param name="name">The package name.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The package if found, or null.</returns>
    Task<ThunderstorePackage?> GetPackageByNameAsync(string @namespace, string name, CancellationToken cancellationToken = default);
}
