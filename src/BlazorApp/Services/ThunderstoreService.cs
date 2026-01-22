using System.Net.Http.Json;
using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Service for fetching package data from Thunderstore API via Cloudflare Worker.
/// </summary>
public sealed class ThunderstoreService : IThunderstoreService
{
    private readonly HttpClient _httpClient;

    public ThunderstoreService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetches all packages from the Thunderstore API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>List of packages from the API, or empty list if none found.</returns>
    public async Task<List<ThunderstorePackage>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/packages", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ThunderstorePackage>>(cancellationToken) ?? new List<ThunderstorePackage>();
    }
}
