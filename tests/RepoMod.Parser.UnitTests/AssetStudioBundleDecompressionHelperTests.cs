using AssetStudio;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioBundleDecompressionHelperTests
{
    [Fact]
    public void DecompressBlock_ReturnsMinusOne_ForUnsupportedType()
    {
        var src = new byte[] { 0x01, 0x02 };
        Span<byte> dst = stackalloc byte[4];
        var error = string.Empty;

        var written = BundleDecompressionHelper.DecompressBlock(CompressionType.None, src, dst, ref error);

        Assert.Equal(-1, written);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DecompressLzmaStream_ReturnsMinusOne_ForInvalidPayload()
    {
        using var compressed = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        using var decompressed = new MemoryStream();
        var error = string.Empty;

        var written = BundleDecompressionHelper.DecompressLzmaStream(
            compressed,
            decompressed,
            compressed.Length,
            decompressedSize: 16,
            ref error);

        Assert.Equal(-1, written);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
