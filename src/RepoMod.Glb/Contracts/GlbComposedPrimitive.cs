using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Contracts;

public sealed record GlbComposedPrimitive(
    string SelectionId,
    string RequestedSlot,
    string ResolvedSlot,
    string TargetAnchorPath,
    ConverterPrimitive Primitive);
