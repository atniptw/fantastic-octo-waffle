using System.Text.Json.Serialization;

namespace BlazorApp.Models;

public sealed class PackageVersion
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
    [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = string.Empty;
    [JsonPropertyName("download_url")] public Uri DownloadUrl { get; set; } = new("https://thunderstore.io/");
    [JsonPropertyName("file_size")] public long? FileSize { get; set; }
    [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
}
