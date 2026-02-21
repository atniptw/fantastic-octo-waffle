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

        var expectedEntryCount = result.Containers.Sum(container => container.Entries.Count);
        Assert.Equal(expectedEntryCount, result.Inventory.Entries.Count);

        Assert.Contains(result.Inventory.Entries, entry =>
            entry.Path.EndsWith("/pathname", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entry.ResolvedPath));

        var anchorTags = result.SemanticAnchorPoints
            .Select(anchor => anchor.Tag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expectedTags = new[]
        {
            "body",
            "head",
            "hip",
            "leftarm",
            "leftleg",
            "neck",
            "rightarm",
            "rightleg",
            "world"
        };

        Assert.Equal(expectedTags.Length, anchorTags.Count);
        Assert.All(expectedTags, expected => Assert.Contains(expected, anchorTags));
        Assert.All(result.SemanticAnchorPoints, anchor =>
            Assert.Contains("decoration", anchor.Name, StringComparison.OrdinalIgnoreCase));

        var transformLookup = result.SemanticTransforms
            .GroupBy(transform => transform.GameObjectPathId)
            .ToDictionary(group => group.Key, group => group.First());
        var nonZeroAnchorCount = result.SemanticAnchorPoints
            .Select(anchor => transformLookup.TryGetValue(anchor.GameObjectPathId, out var transform)
                ? transform.LocalPosition
                : default)
            .Count(position => position.X != 0 || position.Y != 0 || position.Z != 0);

        Assert.True(nonZeroAnchorCount >= 3, "Expected multiple anchor points to have non-zero positions.");
    }

    [Fact]
    public void Parse_PrintsInventoryReport()
    {
        Assert.True(File.Exists(FixturePath), $"Missing fixture at {FixturePath}.");

        var parser = new UnityPackageParser();
        var input = File.ReadAllBytes(FixturePath);

        var result = parser.Parse(input);
        var report = PackageInventoryReport.Build(result);
        var outputPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "test-results", "unitypackage-inventory.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, report);

        Assert.False(string.IsNullOrWhiteSpace(report));
    }
}
