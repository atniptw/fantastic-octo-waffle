using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Abstractions;

public interface IArchiveScanner
{
    ScanModArchiveResult Scan(IReadOnlyList<ArchiveEntryDescriptor> entries);
}
