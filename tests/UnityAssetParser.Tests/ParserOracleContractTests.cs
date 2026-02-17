using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class ParserOracleContractTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "UnityPackage",
        "MoreHead-Asset-Pack_v1.3.unitypackage"
    );

    private static string OraclePath => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "oracle",
        "unitypackage-morehead-v1.json"
    );

    [Fact]
    public void UnityPackage_MetadataParity_MatchesOracleContract()
    {
        Assert.True(File.Exists(FixturePath), $"Missing fixture at {FixturePath}.");
        Assert.True(File.Exists(OraclePath), $"Missing oracle artifact at {OraclePath}.");

        var oracle = LoadOracle(OraclePath);
        var fixtureBytes = File.ReadAllBytes(FixturePath);
        var fixtureHash = ComputeSha256LowerHex(fixtureBytes);

        Assert.Equal(oracle.Fixture.Sha256, fixtureHash);

        var parser = new UnityPackageParser();
        var result = parser.Parse(fixtureBytes);
        var actual = BuildSummary(result);

        Assert.Equal(oracle.Summary.TopContainer.Kind, actual.TopContainer.Kind);
        Assert.Equal(oracle.Summary.TopContainer.EntryCount, actual.TopContainer.EntryCount);
        Assert.Equal(oracle.Summary.TopContainer.AssetEntryCount, actual.TopContainer.AssetEntryCount);
        Assert.Equal(oracle.Summary.TotalContainerCount, actual.TotalContainerCount);
        Assert.Equal(oracle.Summary.SerializedFileCount, actual.SerializedFileCount);
        Assert.Equal(oracle.Summary.WarningCount, actual.WarningCount);
        Assert.Equal(oracle.Summary.ContainerKindCounts, actual.ContainerKindCounts);
    }

    private static OracleContract LoadOracle(string path)
    {
        var json = File.ReadAllText(path);
        var contract = JsonSerializer.Deserialize<OracleContract>(json, JsonOptions());
        Assert.NotNull(contract);
        Assert.Equal(1, contract!.SchemaVersion);
        return contract;
    }

    private static ContractSummary BuildSummary(BaseAssetsContext context)
    {
        var topContainer = context.Containers.Single(container => container.Kind == ContainerKind.UnityPackageTar);
        var assetEntryCount = topContainer.Entries.Count(entry =>
            entry.Path.EndsWith("/asset", StringComparison.OrdinalIgnoreCase));

        var kindCounts = context.Containers
            .GroupBy(container => container.Kind.ToString())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new ContractSummary
        {
            TopContainer = new TopContainerSummary
            {
                Kind = topContainer.Kind.ToString(),
                EntryCount = topContainer.Entries.Count,
                AssetEntryCount = assetEntryCount
            },
            TotalContainerCount = context.Containers.Count,
            SerializedFileCount = context.SerializedFiles.Count,
            WarningCount = context.Warnings.Count,
            ContainerKindCounts = kindCounts
        };
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string ComputeSha256LowerHex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class OracleContract
    {
        public int SchemaVersion { get; init; }
        public OracleFixture Fixture { get; init; } = new();
        public ContractSummary Summary { get; init; } = new();
    }

    private sealed class OracleFixture
    {
        public string RelativePath { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
    }

    private sealed class ContractSummary
    {
        public TopContainerSummary TopContainer { get; init; } = new();
        public int TotalContainerCount { get; init; }
        public int SerializedFileCount { get; init; }
        public int WarningCount { get; init; }
        public Dictionary<string, int> ContainerKindCounts { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class TopContainerSummary
    {
        public string Kind { get; init; } = string.Empty;
        public int EntryCount { get; init; }
        public int AssetEntryCount { get; init; }
    }
}