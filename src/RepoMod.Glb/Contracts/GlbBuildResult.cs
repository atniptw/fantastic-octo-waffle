namespace RepoMod.Glb.Contracts;

public sealed record GlbBuildResult(
    byte[] GlbBytes,
    IReadOnlyList<GlbBuildDiagnostic> Diagnostics);
