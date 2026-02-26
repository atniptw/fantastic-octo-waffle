namespace RepoMod.Glb.Contracts;

public sealed record GlbAnchorComposition(
    string AnchorPath,
    IReadOnlyList<GlbComposedPrimitive> Primitives);
