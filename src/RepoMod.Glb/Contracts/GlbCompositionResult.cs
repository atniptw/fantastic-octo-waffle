namespace RepoMod.Glb.Contracts;

public sealed record GlbCompositionResult(
    string AvatarSceneId,
    IReadOnlyList<GlbAnchorComposition> Anchors,
    IReadOnlyList<GlbAttachmentDecision> Attachments,
    IReadOnlyList<string> Warnings);
