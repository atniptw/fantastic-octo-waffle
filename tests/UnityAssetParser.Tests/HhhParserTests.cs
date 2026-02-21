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

    [Fact]
    public void ConvertToGlb_BlindEyeBody_DecodesPositionsFromExternalStreamData()
    {
        var fixturePath = GetFixtureFilePaths()
            .FirstOrDefault(path => Path.GetFileName(path).Equals("BlindEye_body.hhh", StringComparison.OrdinalIgnoreCase));

        if (fixturePath is null)
        {
            return;
        }

        var parser = new HhhParser();
        var context = new BaseAssetsContext();

        var glb = parser.ConvertToGlb(File.ReadAllBytes(fixturePath), context);
        ValidateGlb(glb);

        var blindEyeMesh = context.SemanticMeshes
            .FirstOrDefault(mesh => mesh.Name.Contains("BlindEye", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(blindEyeMesh);
        Assert.True(blindEyeMesh!.VertexCount > 0);
        Assert.Equal(blindEyeMesh.VertexCount, blindEyeMesh.DecodedPositions.Count);

        var json = ReadJsonChunkText(glb);
        Assert.Contains("\"meshes\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToGlb_WithSemanticMesh_EmitsMeshAndBinaryChunk()
    {
        var fixture = GetFixtureFilePaths();
        if (fixture.Count == 0)
        {
            return;
        }

        var input = File.ReadAllBytes(fixture[0]);
        var parser = new HhhParser();
        var context = CreateSemanticGeometryContext();

        var glb = parser.ConvertToGlb(input, context);

        ValidateGlb(glb);
        Assert.True(HasBinChunk(glb), "Expected GLB BIN chunk for semantic geometry.");

        var json = ReadJsonChunkText(glb);
        Assert.Contains("\"meshes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"accessors\"", json, StringComparison.Ordinal);
        Assert.Contains("\"bufferViews\"", json, StringComparison.Ordinal);
        Assert.Contains("\"POSITION\"", json, StringComparison.Ordinal);
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

    private static string ReadJsonChunkText(byte[] glb)
    {
        var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12, 4));
        var jsonBytes = glb.AsSpan(20, (int)chunkLength);
        return Encoding.UTF8.GetString(jsonBytes).Trim();
    }

    private static bool HasBinChunk(byte[] glb)
    {
        var jsonChunkLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12, 4));
        var offset = 20 + (int)jsonChunkLength;
        if (offset + 8 > glb.Length)
        {
            return false;
        }

        var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(offset + 4, 4));
        return chunkType == 0x004E4942;
    }

    private static BaseAssetsContext CreateSemanticGeometryContext()
    {
        var context = new BaseAssetsContext();

        context.SemanticGameObjects.Add(new SemanticGameObjectInfo(pathId: 200, name: "Root", isActive: true, layer: 0));
        context.SemanticTransforms.Add(new SemanticTransformInfo(
            pathId: 100,
            gameObjectPathId: 200,
            parentPathId: null,
            childrenPathIds: Array.Empty<long>(),
            localPosition: new SemanticVector3(0f, 0f, 0f),
            localRotation: new SemanticQuaternion(1f, 0f, 0f, 0f),
            localScale: new SemanticVector3(1f, 1f, 1f)));

        context.SemanticMeshFilters.Add(new SemanticMeshFilterInfo(pathId: 300, gameObjectPathId: 200, meshPathId: 400));

        var positions = new[]
        {
            new SemanticVector3(0f, 0f, 0f),
            new SemanticVector3(1f, 0f, 0f),
            new SemanticVector3(0f, 1f, 0f)
        };

        var uvs = new[]
        {
            new SemanticVector2(0f, 0f),
            new SemanticVector2(1f, 0f),
            new SemanticVector2(0f, 1f)
        };

        context.SemanticMeshes.Add(new SemanticMeshInfo(
            pathId: 400,
            name: "Triangle",
            bounds: new SemanticBoundsInfo(new SemanticVector3(0f, 0f, 0f), new SemanticVector3(1f, 1f, 1f)),
            channelFlags: new SemanticMeshChannelFlags(positions: true, normals: false, tangents: false, colors: false, uv0: true, uv1: false),
            indexFormat: 1,
            decodedIndices: new uint[] { 0, 1, 2 },
            vertexDataByteLength: 0,
            decodedPositions: positions,
            decodedNormals: Array.Empty<SemanticVector3>(),
            decodedTangents: Array.Empty<SemanticVector4>(),
            decodedColors: Array.Empty<SemanticVector4>(),
            decodedUv0: uvs,
            decodedUv1: Array.Empty<SemanticVector2>(),
            vertexChannels: Array.Empty<SemanticVertexChannelInfo>(),
            vertexStreams: Array.Empty<SemanticVertexStreamInfo>(),
            indexElementSizeBytes: 2,
            indexElementCount: 3,
            indexCount: 3,
            subMeshCount: 1,
            subMeshes: new[] { new SemanticSubMeshInfo(firstByte: 0, indexCount: 3, topology: 0, firstVertex: 0, vertexCount: 3) },
            topology: new[] { 0 },
            vertexCount: 3));

        return context;
    }
}
