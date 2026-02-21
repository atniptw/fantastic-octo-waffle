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

public sealed record StoredAvatar(
    string Id,
    string FileName,
    byte[] Glb,
    long Size,
    long CreatedAt,
    long LastUsed
);

public sealed record StoredAvatarMetadata(
    string Id,
    string FileName,
    long Size,
    long CreatedAt,
    long LastUsed
);

public sealed record AvatarInventory(string Id);
