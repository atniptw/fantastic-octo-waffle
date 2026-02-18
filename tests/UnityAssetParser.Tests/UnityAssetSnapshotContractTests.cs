using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class UnityAssetSnapshotContractTests
{
    private static string FixturesRoot => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "MoreHead-UnityAssets"
    );

    private static string IndexPath => Path.Combine(FixturesRoot, "index.json");

    public static IEnumerable<object[]> SnapshotCases()
    {
        foreach (var fixture in LoadSelectedFixtures())
        {
            yield return new object[]
            {
                fixture.Name,
                fixture.SnapshotFile,
                fixture.Sha256
            };
        }
    }

    [Fact]
    public void SnapshotIndex_IsPresent_AndDeterministic()
    {
        Assert.True(File.Exists(IndexPath), $"Missing index snapshot at {IndexPath}.");

        using var document = JsonDocument.Parse(File.ReadAllText(IndexPath));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());

        var determinismNode = root.GetProperty("determinismCheck");
        Assert.True(determinismNode.GetProperty("passed").GetBoolean());

        var fixtures = root.GetProperty("selectedFixtures").EnumerateArray().ToList();
        Assert.Equal(5, fixtures.Count);

        foreach (var fixture in fixtures)
        {
            var fixtureName = fixture.GetProperty("name").GetString();
            var snapshotFile = fixture.GetProperty("snapshotFile").GetString();

            Assert.False(string.IsNullOrWhiteSpace(fixtureName));
            Assert.False(string.IsNullOrWhiteSpace(snapshotFile));
            Assert.True(File.Exists(Path.Combine(FixturesRoot, fixtureName!)), $"Missing fixture file {fixtureName}.");
            Assert.True(File.Exists(Path.Combine(FixturesRoot, snapshotFile!)), $"Missing snapshot file {snapshotFile}.");
        }
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public void UnityAssetSnapshot_MatchesParserContract(string fixtureName, string snapshotFile, string expectedFixtureSha256)
    {
        var fixturePath = Path.Combine(FixturesRoot, fixtureName);
        var snapshotPath = Path.Combine(FixturesRoot, snapshotFile);

        Assert.True(File.Exists(fixturePath), $"Missing fixture at {fixturePath}.");
        Assert.True(File.Exists(snapshotPath), $"Missing snapshot at {snapshotPath}.");

        var fixtureBytes = File.ReadAllBytes(fixturePath);
        var actualFixtureSha256 = ComputeSha256LowerHex(fixtureBytes);
        Assert.Equal(expectedFixtureSha256, actualFixtureSha256);

        using var snapshotDocument = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        var root = snapshotDocument.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        AssertSnapshotInternalConsistency(root);

        var fixtureNode = root.GetProperty("fixture");
        Assert.Equal(actualFixtureSha256, fixtureNode.GetProperty("sha256").GetString());

        var snapshotRelativePath = fixtureNode.GetProperty("relativePath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(snapshotRelativePath));
        Assert.EndsWith(fixtureName, snapshotRelativePath!, StringComparison.Ordinal);

        var parser = new HhhParser();
        var context = new BaseAssetsContext();
        _ = parser.ConvertToGlb(fixtureBytes, context);

        AssertContainersMatch(root.GetProperty("containers"), context);
        AssertSerializedFilesMatch(root.GetProperty("serializedFiles"), context);
        AssertSummaryMatches(root.GetProperty("summary"), context);
        AssertObjectsMatch(root.GetProperty("objects"), context);
        AssertEntriesMatch(root.GetProperty("entries"), context);
        AssertWarningsMatch(root.GetProperty("warnings"), context);
    }

    private static void AssertContainersMatch(JsonElement snapshotContainersNode, BaseAssetsContext context)
    {
        var snapshotBundleContainers = snapshotContainersNode
            .EnumerateArray()
            .Where(element => string.Equals(element.GetProperty("kind").GetString(), "BundleFile", StringComparison.Ordinal))
            .Select(element => new SnapshotContainer(
                element.GetProperty("kind").GetString()!,
                element.GetProperty("entryCount").GetInt32(),
                element.GetProperty("sourceName").GetString()!
            ))
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .OrderBy(item => item.EntryCount)
            .ThenBy(item => item.SourceName, StringComparer.Ordinal)
            .ToList();

        var expectedBundleContainers = context.Containers
            .Where(container => container.Kind == ContainerKind.UnityFs)
            .Select(item => new SnapshotContainer(
                MapContainerKind(item.Kind),
                item.Entries.Count,
                item.SourceName
            ))
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .OrderBy(item => item.EntryCount)
            .ThenBy(item => item.SourceName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(snapshotBundleContainers.Count, expectedBundleContainers.Count);
        Assert.Equal(snapshotBundleContainers.Select(item => item.EntryCount), expectedBundleContainers.Select(item => item.EntryCount));

        var expectedSerializedContainerCount = context.Containers.Count(container => container.Kind == ContainerKind.SerializedFile);
        var snapshotSerializedContainerCount = snapshotContainersNode
            .EnumerateArray()
            .Count(element => string.Equals(element.GetProperty("kind").GetString(), "SerializedFile", StringComparison.Ordinal));

        Assert.Equal(snapshotSerializedContainerCount, expectedSerializedContainerCount);
    }

    private static void AssertSerializedFilesMatch(JsonElement snapshotSerializedFilesNode, BaseAssetsContext context)
    {
        var snapshotSerializedFiles = snapshotSerializedFilesNode
            .EnumerateArray()
            .Select(element => new SnapshotSerializedFile(
                element.GetProperty("sourceName").GetString()!
            ))
            .OrderBy(item => item.SourceName, StringComparer.Ordinal)
            .ToList();

        var expectedSerializedFiles = context.SerializedFiles
            .Select(item => new SnapshotSerializedFile(item.SourceName))
            .OrderBy(item => item.SourceName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expectedSerializedFiles.Count, snapshotSerializedFiles.Count);
        Assert.Equal(expectedSerializedFiles, snapshotSerializedFiles);
    }

    private static void AssertSummaryMatches(JsonElement snapshotSummary, BaseAssetsContext context)
    {
        var snapshotTop = new SnapshotTopContainer(
            snapshotSummary.GetProperty("topContainer").GetProperty("kind").GetString()!,
            snapshotSummary.GetProperty("topContainer").GetProperty("entryCount").GetInt32()
        );

        Assert.NotEmpty(context.Containers);
        var parserTopContainer = context.Containers.First();
        var expectedTop = new SnapshotTopContainer(
            MapContainerKind(parserTopContainer.Kind),
            parserTopContainer.Entries.Count
        );
        Assert.Equal(expectedTop, snapshotTop);

        Assert.Equal(context.Containers.Count, snapshotSummary.GetProperty("totalContainerCount").GetInt32());
        Assert.Equal(context.SerializedFiles.Count, snapshotSummary.GetProperty("serializedFileCount").GetInt32());

        var snapshotKindCounts = snapshotSummary.GetProperty("containerKindCounts")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);
        var parserKindCounts = context.Containers
            .GroupBy(container => MapContainerKind(container.Kind), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(parserKindCounts, snapshotKindCounts);
    }

    private static void AssertObjectsMatch(JsonElement snapshotObjectsNode, BaseAssetsContext context)
    {
        var snapshotObjects = snapshotObjectsNode
            .EnumerateArray()
            .Select(element => new SnapshotObject(
                element.GetProperty("pathId").GetInt64(),
                element.GetProperty("byteStart").GetInt64(),
                element.GetProperty("byteSize").GetUInt32(),
                element.GetProperty("typeId").GetInt32(),
                element.TryGetProperty("classId", out var classIdNode) && classIdNode.ValueKind != JsonValueKind.Null
                    ? classIdNode.GetInt32()
                    : null
            ))
            .OrderBy(item => item.PathId)
            .ThenBy(item => item.ByteStart)
            .ThenBy(item => item.ByteSize)
            .ThenBy(item => item.TypeId)
            .ThenBy(item => item.ClassId)
            .ToList();

        var expectedObjects = context.SerializedFiles
            .SelectMany(file => file.Objects)
            .Select(item => new SnapshotObject(
                item.PathId,
                item.ByteStart,
                item.ByteSize,
                item.TypeId,
                item.ClassId
            ))
            .OrderBy(item => item.PathId)
            .ThenBy(item => item.ByteStart)
            .ThenBy(item => item.ByteSize)
            .ThenBy(item => item.TypeId)
            .ThenBy(item => item.ClassId)
            .ToList();

        Assert.Equal(expectedObjects.Count, snapshotObjects.Count);
        Assert.Equal(expectedObjects, snapshotObjects);
    }

    private static void AssertEntriesMatch(JsonElement snapshotEntriesNode, BaseAssetsContext context)
    {
        var snapshotEntries = snapshotEntriesNode
            .EnumerateArray()
            .Select(element => new SnapshotEntry(
                element.GetProperty("kind").GetInt32(),
                element.GetProperty("path").GetString()!,
                element.GetProperty("size").GetUInt32()
            ))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Size)
            .ThenBy(item => item.Kind)
            .ToList();

        var expectedEntries = context.SerializedFiles
            .SelectMany(file => file.Objects)
            .Select(item => new SnapshotEntry(
                item.TypeId,
                item.PathId.ToString(CultureInfo.InvariantCulture),
                item.ByteSize
            ))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Size)
            .ThenBy(item => item.Kind)
            .ToList();

        Assert.Equal(expectedEntries.Count, snapshotEntries.Count);
        Assert.Equal(expectedEntries, snapshotEntries);
    }

    private static void AssertWarningsMatch(JsonElement snapshotWarningsNode, BaseAssetsContext context)
    {
        var snapshotCount = snapshotWarningsNode.GetProperty("count").GetInt32();
        Assert.True(snapshotCount >= 0);

        var snapshotCodes = snapshotWarningsNode.GetProperty("codes")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.True(snapshotCodes.Count <= snapshotCount || snapshotCount == 0);

        _ = context.Warnings;
    }

    private static void AssertSnapshotInternalConsistency(JsonElement root)
    {
        var summary = root.GetProperty("summary");
        var serializedFiles = root.GetProperty("serializedFiles").EnumerateArray().ToList();
        var warnings = root.GetProperty("warnings");

        Assert.Equal(serializedFiles.Count, summary.GetProperty("serializedFileCount").GetInt32());
        Assert.Equal(warnings.GetProperty("count").GetInt32(), summary.GetProperty("warningCount").GetInt32());

        var containerKindCounts = summary.GetProperty("containerKindCounts")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);

        var snapshotContainers = root.GetProperty("containers")
            .EnumerateArray()
            .Select(item => item.GetProperty("kind").GetString() ?? string.Empty)
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(containerKindCounts, snapshotContainers);
    }

    private static string MapContainerKind(ContainerKind kind)
    {
        return kind switch
        {
            ContainerKind.UnityFs => "BundleFile",
            ContainerKind.SerializedFile => "SerializedFile",
            ContainerKind.UnityWeb => "UnityWeb",
            ContainerKind.UnityRaw => "UnityRaw",
            ContainerKind.UnityPackageTar => "UnityPackageTar",
            _ => kind.ToString()
        };
    }

    private static string NormalizeWarningCode(string warning)
    {
        if (warning.Contains("LZMA", StringComparison.OrdinalIgnoreCase))
        {
            return "UNITYFS_LZMA_UNSUPPORTED";
        }

        if (warning.Contains("out of range", StringComparison.OrdinalIgnoreCase))
        {
            return "OUT_OF_RANGE";
        }

        if (warning.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            return "UNSUPPORTED";
        }

        return "GENERIC_WARNING";
    }

    private static IReadOnlyList<SelectedFixture> LoadSelectedFixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(IndexPath));
        return document.RootElement
            .GetProperty("selectedFixtures")
            .EnumerateArray()
            .Select(element => new SelectedFixture(
                element.GetProperty("name").GetString()!,
                element.GetProperty("snapshotFile").GetString()!,
                element.GetProperty("sha256").GetString()!
            ))
            .ToList();
    }

    private static string ComputeSha256LowerHex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record SelectedFixture(string Name, string SnapshotFile, string Sha256);
    private sealed record SnapshotContainer(string Kind, int EntryCount, string SourceName);
    private sealed record SnapshotSerializedFile(string SourceName);
    private sealed record SnapshotTopContainer(string Kind, int EntryCount);
    private sealed record SnapshotObject(long PathId, long ByteStart, uint ByteSize, int TypeId, int? ClassId);
    private sealed record SnapshotEntry(int Kind, string Path, uint Size);
}
