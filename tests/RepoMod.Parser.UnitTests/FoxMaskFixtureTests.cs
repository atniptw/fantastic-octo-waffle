using AssetStudio;
using RepoMod.Parser.Contracts;
using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class FoxMaskFixtureTests
{
    [Fact]
    public void FoxMaskFixture_DetectedAsBundleFile()
    {
        var fixturePath = GetFixturePath("FoxMask_head.hhh");

        using var stream = File.OpenRead(fixturePath);
        using var reader = new FileReader(fixturePath, stream);

        Assert.Equal(FileType.BundleFile, reader.FileType);
    }

    [Fact]
    public void FoxMaskFixture_FileNameInfersHeadSlot()
    {
        var parser = new ModParser();

        var metadata = parser.ExtractMetadata("FoxMask_head.hhh");

        Assert.Equal("FoxMask_head", metadata.Name);
        Assert.Equal("head", metadata.SlotTag);
    }

    [Fact]
    public void FoxMaskFixture_ScanAndParseMetadata_ProducesHeadBundle()
    {
        var fixturePath = GetFixturePath("FoxMask_head.hhh");
        var fixtureFile = new FileInfo(fixturePath);

        var scanner = new ArchiveScanner();
        var parser = new ModParser();

        var result = scanner.Scan([
            new ArchiveEntryDescriptor(
                fixtureFile.FullName,
                fixtureFile.Name,
                fixtureFile.Length,
                false)
        ]);

        Assert.True(result.Success);
        var bundle = Assert.Single(result.Bundles);
        Assert.Equal(".hhh", bundle.Extension);
        Assert.Equal("FoxMask_head.hhh", bundle.FileName);
        Assert.True(bundle.SizeBytes > 0);

        var metadata = parser.ExtractMetadata(bundle.FileName);
        Assert.Equal("FoxMask_head", metadata.Name);
        Assert.Equal("head", metadata.SlotTag);
    }

    private static string GetFixturePath(string fileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");
        return fixturePath;
    }
}
