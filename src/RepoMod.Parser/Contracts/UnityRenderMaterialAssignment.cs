namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderMaterialAssignment(
    int SubMeshIndex,
    string? MaterialObjectId);
