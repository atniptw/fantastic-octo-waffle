using System;
using System.IO;
using System.Linq;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class UnityPackageParserTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "MoreHead-UnityPackage",
        "MoreHead-Asset-Pack_v1.3.unitypackage"
    );

    [Fact]
    public void Parse_ThrowsOnNull()
    {
        var parser = new UnityPackageParser();

        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));
    }

    [Fact]
    public void Parse_ReturnsBaseAssetsContext()
    {
        var parser = new UnityPackageParser();
        var input = Array.Empty<byte>();

        var result = parser.Parse(input);

        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_LoadsUnityPackageFixture()
    {
        Assert.True(File.Exists(FixturePath), $"Missing fixture at {FixturePath}.");

        var parser = new UnityPackageParser();
        var input = File.ReadAllBytes(FixturePath);

        var result = parser.Parse(input);

        Assert.NotNull(result);
        Assert.Contains(result.Containers, container =>
            container.Kind == ContainerKind.UnityPackageTar && container.Entries.Count > 0);
        Assert.Contains(result.Containers.SelectMany(container => container.Entries), entry =>
            entry.Path.EndsWith("/asset", StringComparison.OrdinalIgnoreCase));
    }
}
