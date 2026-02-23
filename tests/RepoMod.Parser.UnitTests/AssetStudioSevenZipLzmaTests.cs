using BundleCompression.Lzma;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioSevenZipLzmaTests
{
    [Fact]
    public void DecompressStream_ThrowsForTooShortInput()
    {
        using var input = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });

        var ex = Assert.Throws<Exception>(() => SevenZipLzma.DecompressStream(input));

        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
