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

    [Fact]
    public void ScanUnityPackage_RealFixture_DiscoversUnityAssets_AndRunsThroughParser()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MoreHead-Asset-Pack_v1.3.unitypackage");
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        var scanner = new ArchiveScanner();
        var parser = new ModParser();

        var result = scanner.ScanUnityPackage(fixturePath);

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Bundles);
        Assert.Contains(result.Bundles, bundle => bundle.Extension == ".asset");
        Assert.DoesNotContain(result.Bundles, bundle => bundle.Extension == ".cs");

        var headCandidate = result.Bundles.FirstOrDefault(bundle => bundle.FileName.Contains("head", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(headCandidate);
        var metadata = parser.ExtractMetadata(headCandidate.FileName);
        Assert.Equal("head", metadata.SlotTag);
    }
}
