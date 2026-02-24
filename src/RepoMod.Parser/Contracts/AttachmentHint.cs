namespace RepoMod.Parser.Contracts;

public sealed record AttachmentHint(
    string SourceAssetId,
    string SlotTag,
    IReadOnlyList<string> CandidateBoneNames,
    IReadOnlyList<string> CandidateNodePaths,
    IReadOnlyList<string> ExternalReferenceGuids);
