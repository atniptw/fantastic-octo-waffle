using System.Collections.Generic;

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

public sealed record UnityPackageInventory(
    string Id,
    string Name,
    long CreatedAt,
    long LastUsed,
    IReadOnlyList<UnityPackageAnchor> Anchors,
    IReadOnlyList<string> ResolvedPaths
);

public sealed record UnityPackageInventoryMetadata(
    string Id,
    string Name,
    long CreatedAt,
    long LastUsed
);

public sealed record UnityPackageAnchor(
    string Tag,
    string Name,
    long GameObjectPathId,
    float PositionX,
    float PositionY,
    float PositionZ
);
