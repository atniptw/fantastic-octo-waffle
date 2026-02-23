using AssetStudio;
using RepoMod.Parser.Adapters.AssetStudio;
using System.Text;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioFileTypeGuesserTests
{
    [Fact]
    public void GuessFromHeader_ReturnsBundleFile_ForUnityFsSignature()
    {
        var bytes = Encoding.ASCII.GetBytes("UnityFS\0test");

        var result = AssetStudioFileTypeGuesser.GuessFromHeader(bytes);

        Assert.Equal(FileType.BundleFile, result);
    }

    [Fact]
    public void GuessFromHeader_ReturnsWebFile_ForUnityWebDataHeader()
    {
        var bytes = Encoding.ASCII.GetBytes("UnityWebData1.0\0");

        var result = AssetStudioFileTypeGuesser.GuessFromHeader(bytes);

        Assert.Equal(FileType.WebFile, result);
    }

    [Fact]
    public void GuessFromHeader_ReturnsGZipFile_ForGZipMagic()
    {
        var bytes = new byte[] { 0x1F, 0x8B, 0x08, 0x00 };

        var result = AssetStudioFileTypeGuesser.GuessFromHeader(bytes);

        Assert.Equal(FileType.GZipFile, result);
    }

    [Fact]
    public void GuessFromHeader_ReturnsZipFile_ForPkMagic()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        var result = AssetStudioFileTypeGuesser.GuessFromHeader(bytes);

        Assert.Equal(FileType.ZipFile, result);
    }

    [Fact]
    public void GuessFromHeader_ReturnsNull_ForUnknownHeader()
    {
        var bytes = Encoding.ASCII.GetBytes("NOT_A_UNITY_FILE");

        var result = AssetStudioFileTypeGuesser.GuessFromHeader(bytes);

        Assert.Null(result);
    }
}
