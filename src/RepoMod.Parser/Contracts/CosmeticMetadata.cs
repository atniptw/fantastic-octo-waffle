namespace RepoMod.Parser.Contracts;

public sealed record CosmeticMetadata(
    string Name,
    string? Author,
    string? Version,
    string SlotTag);
