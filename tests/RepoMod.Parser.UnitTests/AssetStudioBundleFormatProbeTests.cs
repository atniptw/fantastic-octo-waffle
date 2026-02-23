using RepoMod.Parser.Adapters.AssetStudio;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioBundleFormatProbeTests
{
    [Theory]
    [InlineData("UnityFS")]
    [InlineData("UnityWeb")]
    [InlineData("UnityRaw")]
    [InlineData("UnityArchive")]
    public void IsLikelyUnityBundle_ReturnsTrue_ForKnownSignatures(string signature)
    {
        var probe = new AssetStudioBundleFormatProbe();
        var bytes = System.Text.Encoding.ASCII.GetBytes(signature + "\nrest");

        var result = probe.IsLikelyUnityBundle(bytes);

        Assert.True(result);
    }

    [Fact]
    public void IsLikelyUnityBundle_ReturnsFalse_ForUnknownHeader()
    {
        var probe = new AssetStudioBundleFormatProbe();
        var bytes = System.Text.Encoding.ASCII.GetBytes("PK\u0003\u0004zip");

        var result = probe.IsLikelyUnityBundle(bytes);

        Assert.False(result);
    }
}
