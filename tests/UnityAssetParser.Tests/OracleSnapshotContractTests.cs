using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class OracleSnapshotContractTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "MoreHead-UnityPackage",
        "MoreHead-Asset-Pack_v1.3.unitypackage"
    );

    private static string SnapshotsRoot => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "MoreHead-Snapshots",
        "UnityPackage"
    );

    private static string SummarySnapshotPath => Path.Combine(SnapshotsRoot, "summary_snapshot.json");
    private static string TarManifestSnapshotPath => Path.Combine(SnapshotsRoot, "tar_manifest_snapshot.json");
    private static string ContainersSnapshotPath => Path.Combine(SnapshotsRoot, "unityfs_containers_snapshot.json");
    private static string SerializedSnapshotPath => Path.Combine(SnapshotsRoot, "serialized_files_snapshot.json");
    private static string WarningsSnapshotPath => Path.Combine(SnapshotsRoot, "warnings_snapshot.json");

    [Fact]
    public void SplitSnapshots_Exist_AndReferenceFixtureHash()
    {
        Assert.True(File.Exists(FixturePath), $"Missing fixture at {FixturePath}.");
        var fixtureHash = ComputeSha256LowerHex(File.ReadAllBytes(FixturePath));

        foreach (var path in SnapshotPaths())
        {
            Assert.True(File.Exists(path), $"Missing split snapshot at {path}.");

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("fixture", out var fixtureNode), $"Snapshot {path} is missing fixture metadata.");
            Assert.True(fixtureNode.TryGetProperty("sha256", out var shaNode), $"Snapshot {path} fixture metadata is missing sha256.");
            Assert.Equal(fixtureHash, shaNode.GetString());
        }
    }

    [Fact]
    public void SummarySnapshot_MatchesParserSummary()
    {
        var parserSummary = BuildParserSummary();

        using var document = JsonDocument.Parse(File.ReadAllText(SummarySnapshotPath));
        var root = document.RootElement;
        var summary = root.GetProperty("summary");
        var topContainer = summary.GetProperty("topContainer");

        Assert.Equal(parserSummary.TopContainerKind, topContainer.GetProperty("kind").GetString());
        Assert.Equal(parserSummary.TopContainerEntryCount, topContainer.GetProperty("entryCount").GetInt32());
        Assert.Equal(parserSummary.TopContainerAssetEntryCount, topContainer.GetProperty("assetEntryCount").GetInt32());

        Assert.Equal(parserSummary.TotalContainerCount, summary.GetProperty("totalContainerCount").GetInt32());
        Assert.Equal(parserSummary.SerializedFileCount, summary.GetProperty("serializedFileCount").GetInt32());
        Assert.Equal(parserSummary.WarningCount, summary.GetProperty("warningCount").GetInt32());

        var kindCounts = summary.GetProperty("containerKindCounts")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);
        Assert.Equal(parserSummary.ContainerKindCounts, kindCounts);
    }

    [Fact]
    public void TarManifestSnapshot_RegularFilesMatchTopContainerEntries()
    {
        var context = ParseFixture();
        var topContainer = context.Containers.Single(container => container.Kind == ContainerKind.UnityPackageTar);

        using var document = JsonDocument.Parse(File.ReadAllText(TarManifestSnapshotPath));
        var root = document.RootElement;
        var entries = root.GetProperty("manifest").GetProperty("entries").EnumerateArray();

        var snapshotRegularEntries = entries
            .Where(entry => entry.TryGetProperty("isfile", out var isFile) && isFile.ValueKind == JsonValueKind.True)
            .Select(entry => (Path: entry.GetProperty("path").GetString()!, Size: entry.GetProperty("size").GetInt64()))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Size)
            .ToList();

        var parserEntries = topContainer.Entries
            .Select(entry => (entry.Path, entry.Size))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Size)
            .ToList();

        Assert.Equal(parserEntries.Count, snapshotRegularEntries.Count);
        Assert.Equal(parserEntries, snapshotRegularEntries);
    }

    [Fact]
    public void ContainersSnapshot_ChildAssetPathsMatchTopContainerAssetEntries()
    {
        var context = ParseFixture();
        var topContainer = context.Containers.Single(container => container.Kind == ContainerKind.UnityPackageTar);

        var parserAssetPaths = topContainer.Entries
            .Where(entry => entry.Path.EndsWith("/asset", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        using var document = JsonDocument.Parse(File.ReadAllText(ContainersSnapshotPath));
        var root = document.RootElement;

        var items = root.GetProperty("containers").GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var childPaths = items
            .SelectMany(item => item.GetProperty("childFiles").EnumerateArray())
            .Select(child => child.GetProperty("name").GetString()!)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(parserAssetPaths, childPaths);
    }

    [Fact]
    public void SerializedAndWarningsSnapshots_MatchParserCounts()
    {
        var context = ParseFixture();

        using var serializedDocument = JsonDocument.Parse(File.ReadAllText(SerializedSnapshotPath));
        using var warningsDocument = JsonDocument.Parse(File.ReadAllText(WarningsSnapshotPath));

        var serializedCount = serializedDocument.RootElement
            .GetProperty("serializedFiles")
            .GetProperty("count")
            .GetInt32();
        Assert.Equal(context.SerializedFiles.Count, serializedCount);

        var warningNode = warningsDocument.RootElement.GetProperty("warnings");
        var warningCount = warningNode.GetProperty("count").GetInt32();
        Assert.Equal(context.Warnings.Count, warningCount);

        var warningCodes = warningNode.GetProperty("codes")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        Assert.Empty(warningCodes);
    }

    private static IEnumerable<string> SnapshotPaths()
    {
        yield return SummarySnapshotPath;
        yield return TarManifestSnapshotPath;
        yield return ContainersSnapshotPath;
        yield return SerializedSnapshotPath;
        yield return WarningsSnapshotPath;
    }

    private static BaseAssetsContext ParseFixture()
    {
        var parser = new UnityPackageParser();
        return parser.Parse(File.ReadAllBytes(FixturePath));
    }

    private static ParserSummary BuildParserSummary()
    {
        var context = ParseFixture();
        var topContainer = context.Containers.Single(container => container.Kind == ContainerKind.UnityPackageTar);

        var containerKindCounts = context.Containers
            .GroupBy(container => container.Kind.ToString())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new ParserSummary
        {
            TopContainerKind = topContainer.Kind.ToString(),
            TopContainerEntryCount = topContainer.Entries.Count,
            TopContainerAssetEntryCount = topContainer.Entries.Count(entry => entry.Path.EndsWith("/asset", StringComparison.OrdinalIgnoreCase)),
            TotalContainerCount = context.Containers.Count,
            SerializedFileCount = context.SerializedFiles.Count,
            WarningCount = context.Warnings.Count,
            ContainerKindCounts = containerKindCounts
        };
    }

    private static string ComputeSha256LowerHex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class ParserSummary
    {
        public string TopContainerKind { get; init; } = string.Empty;
        public int TopContainerEntryCount { get; init; }
        public int TopContainerAssetEntryCount { get; init; }
        public int TotalContainerCount { get; init; }
        public int SerializedFileCount { get; init; }
        public int WarningCount { get; init; }
        public Dictionary<string, int> ContainerKindCounts { get; init; } = new(StringComparer.Ordinal);
    }
}
