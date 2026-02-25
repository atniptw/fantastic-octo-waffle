namespace RepoMod.Parser.Contracts;

public sealed record UnityObjectRef(
    string ObjectId,
    string AssetId,
    string ContainerId,
    string? PackageGuid,
    string? FileId,
    string? PathId,
    int? ClassId,
    string ObjectName,
    IReadOnlyList<UnityObjectPointer> OutboundObjectRefs);
