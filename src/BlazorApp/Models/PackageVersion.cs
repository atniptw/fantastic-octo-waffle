using System.Text.Json.Serialization;

namespace BlazorApp.Models;

public sealed class PackageVersion
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("version_number")] public string VersionNumber { get; init; } = string.Empty;
    [JsonPropertyName("download_url")] public Uri DownloadUrl { get; init; } = new("https://thunderstore.io/");
    [JsonPropertyName("file_size")] public long? FileSize { get; init; }
    [JsonPropertyName("dependencies")] public List<string> Dependencies { get; init; } = new();
}
