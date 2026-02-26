namespace RepoMod.Glb.Contracts;

public sealed record GlbBuildDiagnostic(
    string Severity,
    string Code,
    string Message,
    string? PrimitiveId);
