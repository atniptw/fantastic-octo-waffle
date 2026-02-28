using AssetStudio;
using System.Text;

namespace RepoMod.Parser.UnitTests;

[Collection("NonParallel")]
public class AssetStudioFileReaderTests
{
    [Fact]
    public void FileReader_DetectsBundleFile_ForUnityFsHeader()
    {
        var bytes = new byte[256];
        Encoding.ASCII.GetBytes("UnityFS\0").CopyTo(bytes, 0);

        using var stream = new MemoryStream(bytes);
        using var reader = new FileReader("bundle.hhh", stream);

        Assert.Equal(FileType.BundleFile, reader.FileType);
    }

    [Fact]
    public void FileReader_DetectsWebFile_ForUnityWebDataHeader()
    {
        var bytes = new byte[256];
        Encoding.ASCII.GetBytes("UnityWebData1.0\0").CopyTo(bytes, 0);

        using var stream = new MemoryStream(bytes);
        using var reader = new FileReader("webdata", stream);

        Assert.Equal(FileType.WebFile, reader.FileType);
    }

    [Fact]
    public void FileReader_DetectsGZipFile_ByMagic()
    {
        var bytes = new byte[] { 0x1F, 0x8B, 0x08, 0x00, 0x00 };

        using var stream = new MemoryStream(bytes);
        using var reader = new FileReader("data.gz", stream);

        Assert.Equal(FileType.GZipFile, reader.FileType);
    }

    [Fact]
    public void FileReader_DetectsZipFile_ByMagic()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };

        using var stream = new MemoryStream(bytes);
        using var reader = new FileReader("archive.zip", stream);

        Assert.Equal(FileType.ZipFile, reader.FileType);
    }
}
