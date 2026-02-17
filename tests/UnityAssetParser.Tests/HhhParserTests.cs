using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class HhhParserTests
{
    private static string FixturesRoot => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "MoreHead-UnityAssets"
    );

    public static IEnumerable<object[]> FixtureFiles()
    {
        foreach (var path in GetFixtureFilePaths())
        {
            yield return new object[] { path };
        }
    }

    [Fact]
    public void Fixtures_AreAvailable()
    {
        Assert.NotEmpty(GetFixtureFilePaths());
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void ConvertToGlb_ReturnsValidGlb(string filePath)
    {
        var input = File.ReadAllBytes(filePath);
        var parser = new HhhParser();

        var glb = parser.ConvertToGlb(input);

        ValidateGlb(glb);
    }

    [Fact]
    public void ConvertToGlb_WithContext_PopulatesSkeletonContainer()
    {
        var fixture = GetFixtureFilePaths();
        if (fixture.Count == 0)
        {
            return;
        }

        var input = File.ReadAllBytes(fixture[0]);
        var parser = new HhhParser();
        var context = new BaseAssetsContext();

        parser.ConvertToGlb(input, context);

        Assert.NotEmpty(context.Containers);
        Assert.Contains(context.Containers, container => container.SourceName == "hhh");
    }

    [Fact]
    public void ConvertToGlb_WithContext_DetectsUnityFsEntries()
    {
        var fixtures = GetFixtureFilePaths();
        if (fixtures.Count == 0)
        {
            return;
        }

        var parser = new HhhParser();
        var context = new BaseAssetsContext();

        foreach (var path in fixtures)
        {
            parser.ConvertToGlb(File.ReadAllBytes(path), context);
        }

        Assert.Contains(context.Containers, container =>
            container.Kind == ContainerKind.UnityFs
            && (!string.IsNullOrWhiteSpace(container.UnityRevision)
                || !string.IsNullOrWhiteSpace(container.UnityVersion)));
    }

    private static IReadOnlyList<string> GetFixtureFilePaths()
    {
        if (!Directory.Exists(FixturesRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(FixturesRoot, "*.hhh", SearchOption.AllDirectories);
    }

    private static void ValidateGlb(byte[] glb)
    {
        Assert.NotNull(glb);
        Assert.True(glb.Length >= 20, "GLB is too small to contain a header and chunk.");

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(0, 4));
        Assert.Equal(0x46546C67u, magic);

        var version = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4, 4));
        Assert.Equal(2u, version);

        var totalLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8, 4));
        Assert.Equal((uint)glb.Length, totalLength);

        var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12, 4));
        var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(16, 4));

        Assert.True(chunkLength > 0, "JSON chunk must be non-empty.");
        Assert.Equal(0x4E4F534Au, chunkType);
        Assert.True(chunkLength % 4 == 0, "Chunk length must be 4-byte aligned.");

        var chunkEnd = 20 + (int)chunkLength;
        Assert.True(chunkEnd <= glb.Length, "Chunk extends beyond buffer length.");

        var jsonBytes = glb.AsSpan(20, (int)chunkLength);
        var jsonText = Encoding.UTF8.GetString(jsonBytes).Trim();
        Assert.False(string.IsNullOrWhiteSpace(jsonText), "JSON chunk must contain text.");
    }
}
