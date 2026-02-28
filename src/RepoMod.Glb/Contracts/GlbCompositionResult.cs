using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Contracts;

public sealed record GlbCompositionResult(
    string AvatarSceneId,
    IReadOnlyList<GlbAnchorComposition> Anchors,
    IReadOnlyList<GlbAttachmentDecision> Attachments,
    IReadOnlyList<UnityRenderMaterial> RenderMaterials,
    IReadOnlyList<UnityRenderTexture> RenderTextures,
    IReadOnlyList<string> Warnings);
