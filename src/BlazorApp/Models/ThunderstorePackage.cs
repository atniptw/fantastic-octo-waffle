using System.Text.Json.Serialization;

namespace BlazorApp.Models;

/// <summary>
/// Represents a package from the Thunderstore API v1.
/// Matches the response schema from /c/repo/api/v1/package/ endpoint.
/// </summary>
public sealed class ThunderstorePackage
{
    [JsonPropertyName("name")] 
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("full_name")] 
    public string FullName { get; init; } = string.Empty;
    
    [JsonPropertyName("owner")] 
    public string Owner { get; init; } = string.Empty;
    
    [JsonPropertyName("categories")] 
    public List<string> Categories { get; init; } = new();
    
    [JsonPropertyName("icon")] 
    public Uri? Icon { get; init; }
    
    [JsonPropertyName("rating_score")] 
    public int RatingScore { get; init; }
    
    [JsonPropertyName("is_deprecated")] 
    public bool IsDeprecated { get; init; }
    
    [JsonPropertyName("has_nsfw_content")] 
    public bool HasNsfwContent { get; init; }
    
    [JsonPropertyName("date_updated")] 
    public DateTime DateUpdated { get; init; }
    
    [JsonPropertyName("versions")] 
    public List<PackageVersion> Versions { get; init; } = new();
    
    /// <summary>
    /// Gets the latest version (first element in Versions array).
    /// </summary>
    public PackageVersion? LatestVersion => Versions.Count > 0 ? Versions[0] : null;
}
