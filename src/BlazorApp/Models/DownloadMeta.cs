namespace BlazorApp.Models;

/// <summary>Metadata for a downloadable mod package.</summary>
public sealed record DownloadMeta(long SizeBytes, string Filename);
