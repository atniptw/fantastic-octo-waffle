namespace RepoMod.Parser.Contracts;

public sealed record RefLink(
    string SourceAssetId,
    string SourceContainerId,
    string TargetGuid,
    string? TargetAssetId,
    string? TargetObjectId,
    string? FileId,
    string Status);
