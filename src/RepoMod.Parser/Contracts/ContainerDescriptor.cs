namespace RepoMod.Parser.Contracts;

public sealed record ContainerDescriptor(
    string ContainerId,
    string SourcePath,
    string SourceType,
    string DisplayName);
