using RepoMod.Parser.Contracts;
using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class ArchiveScannerTests
{
    [Fact]
    public void Scan_FiltersOnlyHhhFiles()
    {
        var scanner = new ArchiveScanner();
        var entries = new List<ArchiveEntryDescriptor>
        {
            new("mods/a.hhh", "a.hhh", 100, false),
            new("mods/b.txt", "b.txt", 100, false),
            new("mods/folder/", "folder", 0, true),
            new("mods/c.HHH", "c.HHH", 100, false)
        };

        var result = scanner.Scan(entries);

        Assert.True(result.Success);
        Assert.Equal(2, result.Bundles.Count);
        Assert.All(result.Bundles, bundle => Assert.Equal(".hhh", bundle.Extension));
    }
}
