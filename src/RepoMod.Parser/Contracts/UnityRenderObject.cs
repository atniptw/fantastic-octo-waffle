namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderObject(
    string ObjectId,
    string AssetId,
    int? ClassId,
    string Kind,
    string Name,
    string? ParentObjectId,
    IReadOnlyList<string> ChildObjectIds,
    string? MeshObjectId,
    IReadOnlyList<string> MaterialObjectIds,
    IReadOnlyList<float>? LocalPosition,
    IReadOnlyList<float>? LocalRotation,
    IReadOnlyList<float>? LocalScale);
