using AssetStudio;
using System.IO.Compression;
using System.Text;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioImportHelperTests
{
    [Fact]
    public void DecompressGZip_ReturnsReaderWithExpectedPayload()
    {
        var payload = Encoding.UTF8.GetBytes("hello-from-gzip");
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }

        compressed.Position = 0;
        using var source = new FileReader("sample.gz", compressed);

        using var decompressed = ImportHelper.DecompressGZip(source);

        Assert.NotNull(decompressed);
        using var reader = new BinaryReader(decompressed!.BaseStream, Encoding.UTF8, leaveOpen: true);
        decompressed.BaseStream.Position = 0;
        var actual = reader.ReadBytes(payload.Length);
        Assert.Equal(payload, actual);
    }
}
