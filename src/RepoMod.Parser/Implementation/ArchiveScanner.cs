using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public sealed class ArchiveScanner : IArchiveScanner
{
    public ScanModArchiveResult Scan(IReadOnlyList<ArchiveEntryDescriptor> entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return ScanModArchiveResult.Succeeded([]);
        }

        var bundles = entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => new DiscoveredBundle(
                entry.FullPath,
                entry.FileName,
                entry.SizeBytes,
                Path.GetExtension(entry.FileName).ToLowerInvariant()))
            .Where(bundle => bundle.Extension == ".hhh")
            .ToArray();

        return ScanModArchiveResult.Succeeded(bundles);
    }
}
