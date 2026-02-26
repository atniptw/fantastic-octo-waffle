namespace RepoMod.Glb.Contracts;

public sealed record GlbAttachmentDecision(
    string SelectionId,
    string RequestedSlot,
    string ResolvedSlot,
    string TargetAnchorPath,
    bool ResolvedFromCandidate,
    IReadOnlyList<string> PrimitiveIds);
