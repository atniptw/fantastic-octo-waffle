namespace BlazorApp.Services;

public sealed record StoredAsset(
    string Id,
    string ModId,
    string Name,
    byte[] Glb,
    byte[]? Thumbnail,
    long Size,
    long CreatedAt,
    long LastUsed,
    long? ProcessedAt,
    string SourcePath
);

public sealed record StoredAssetMetadata(
    string Id,
    string ModId,
    string Name,
    long Size,
    long CreatedAt,
    long LastUsed,
    long? ProcessedAt,
    string SourcePath
);
