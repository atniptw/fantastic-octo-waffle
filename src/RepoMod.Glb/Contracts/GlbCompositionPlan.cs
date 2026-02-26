namespace RepoMod.Glb.Contracts;

public sealed record GlbCompositionPlan(
    string AvatarSceneId,
    IReadOnlyList<GlbAttachmentDecision> Attachments,
    IReadOnlyList<string> Warnings);
