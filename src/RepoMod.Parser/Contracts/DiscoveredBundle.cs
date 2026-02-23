namespace RepoMod.Parser.Contracts;

public sealed record DiscoveredBundle(
    string FullPath,
    string FileName,
    long SizeBytes,
    string Extension);
