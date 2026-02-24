namespace RepoMod.Parser.Contracts;

public sealed record ParsedAssetRecord(
    string AssetId,
    string ContainerId,
    string Pathname,
    string FileName,
    string Extension,
    string AssetKind,
    long SizeBytes,
    string? PackageGuid,
    string? MetaGuid,
    IReadOnlyList<string> ReferencedGuids,
    bool IsAvatarCandidate,
    bool IsCosmeticCandidate,
    string? SlotTag);
