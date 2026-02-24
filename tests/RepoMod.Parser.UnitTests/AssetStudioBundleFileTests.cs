using AssetStudio;
using AssetStudio.CustomOptions;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioBundleFileTests
{
    [Fact]
    public void Constructor_AllowsUnknownSignature_WithoutThrowing()
    {
        using var stream = new MemoryStream(BuildUnknownBundleHeader());
        using var reader = new FileReader("unknown.bundle", stream);

        var bundle = new BundleFile(reader, new CustomBundleOptions(new ImportOptions()));

        Assert.Equal("Unknown", bundle.m_Header.signature);
    }

    [Fact]
    public void Constructor_ParsesFoxMaskFixture_WithExpectedStructure()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FoxMask_head.hhh");
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        using var stream = File.OpenRead(fixturePath);
        using var reader = new FileReader(fixturePath, stream);

        var bundle = new BundleFile(reader, new CustomBundleOptions(new ImportOptions()));

        Assert.Equal("UnityFS", bundle.m_Header.signature);
        Assert.NotNull(bundle.fileList);
        Assert.NotEmpty(bundle.fileList);
        Assert.All(bundle.fileList, file => Assert.False(string.IsNullOrWhiteSpace(file.fileName)));
        Assert.All(bundle.fileList, file => Assert.False(string.IsNullOrWhiteSpace(file.path)));
        Assert.All(bundle.fileList, file => Assert.Equal(Path.GetFileName(file.path), file.fileName));
        Assert.All(bundle.fileList, file => Assert.NotNull(file.stream));
        Assert.Contains(bundle.fileList, file => file.stream is { CanRead: true, Length: > 0 });
    }

    private static byte[] BuildUnknownBundleHeader()
    {
        using var stream = new MemoryStream();
        stream.Write(System.Text.Encoding.ASCII.GetBytes("Unknown\0"));
        stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x06 });
        stream.WriteByte(0x00);
        stream.WriteByte(0x00);
        return stream.ToArray();
    }
}
