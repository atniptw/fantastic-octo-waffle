using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Service for fetching package data from Thunderstore API via Cloudflare Worker.
/// </summary>
public interface IThunderstoreService
{
    /// <summary>
    /// Fetches all packages from the Thunderstore API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>List of packages from the API, or empty list if none found.</returns>
    Task<List<ThunderstorePackage>> GetPackagesAsync(CancellationToken cancellationToken = default);
}
