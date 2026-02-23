namespace RepoMod.Parser.Contracts;

public sealed record ArchiveEntryDescriptor(
    string FullPath,
    string FileName,
    long SizeBytes,
    bool IsDirectory);
