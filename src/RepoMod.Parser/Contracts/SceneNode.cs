namespace RepoMod.Parser.Contracts;

public sealed record SceneNode(
    string NodeId,
    string Kind,
    string Label,
    string? AssetId,
    string? Guid);
