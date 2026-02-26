using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Contracts;

public sealed record GlbCosmeticSelection(
    string SelectionId,
    string? SlotTag,
    ConverterScene Scene,
    IReadOnlyList<string>? CandidateNodePaths,
    bool Enabled);
