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

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        Assert.True(schemaVersion is 1 or 2, $"Unsupported index schemaVersion: {schemaVersion}");

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
        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();

        Assert.True(schemaVersion is 1 or 2, $"Unsupported snapshot schemaVersion: {schemaVersion}");
        AssertSnapshotInternalConsistency(root, schemaVersion);

        var fixtureNode = root.GetProperty("fixture");
        Assert.Equal(actualFixtureSha256, fixtureNode.GetProperty("sha256").GetString());

        var snapshotRelativePath = fixtureNode.GetProperty("relativePath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(snapshotRelativePath));
        Assert.EndsWith(fixtureName, snapshotRelativePath!, StringComparison.Ordinal);

        var parser = new HhhParser();
        var context = new BaseAssetsContext();
        _ = parser.ConvertToGlb(fixtureBytes, context);

        if (schemaVersion == 1)
        {
            AssertContainersMatch(root.GetProperty("containers"), context);
            AssertSerializedFilesMatch(root.GetProperty("serializedFiles"), context);
            AssertSummaryMatches(root.GetProperty("summary"), context);
            AssertObjectsMatch(root.GetProperty("objects"), context);
            AssertEntriesMatch(root.GetProperty("entries"), context);
            AssertWarningsMatch(root.GetProperty("warnings"), context);
            return;
        }

        AssertObjectsByPathIdClassIdTypeMatch(root.GetProperty("objects"), context);
        AssertV2SummaryMatchesParser(root.GetProperty("summary"), context);
        AssertV2HierarchyMatchesParser(fixtureName, root.GetProperty("hierarchy"), root.GetProperty("objects"), context);
        AssertV2MeshFilterLinksMatchParser(root.GetProperty("objects"), context);
        AssertV2MeshRendererLinksMatchParser(root.GetProperty("objects"), context);
        AssertV2MeshCoreMatchesParser(root.GetProperty("meshes"), context);
        AssertV2MeshChannelsMatchParser(root.GetProperty("meshes"), context);
        AssertV2MeshRenderableSemantics(context);
        AssertV2MaterialCoreMatchesParser(root.GetProperty("materials"), context);
        AssertV2TextureCoreMatchesParser(root.GetProperty("textures"), context);
        AssertV2MaterialConsistency(root.GetProperty("materials"), root.GetProperty("objects"));
        AssertV2MeshConsistency(root.GetProperty("meshes"), root.GetProperty("objects"));
        AssertV2TextureConsistency(root.GetProperty("textures"), root.GetProperty("objects"));
        AssertV2WarningsConsistency(root.GetProperty("warnings"), context);
    }

    private static void AssertObjectsByPathIdClassIdTypeMatch(JsonElement snapshotObjectsNode, BaseAssetsContext context)
    {
        var snapshotObjects = snapshotObjectsNode
            .EnumerateArray()
            .Select(element => (
                PathId: element.GetProperty("pathId").GetInt64(),
                ClassId: (int?)element.GetProperty("classId").GetInt32(),
                Type: element.GetProperty("type").GetString() ?? string.Empty
            ))
            .OrderBy(item => item.PathId)
            .ThenBy(item => item.ClassId)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ToList();

        var parserObjects = context.SemanticObjects
            .Select(item => (item.PathId, item.ClassId, Type: item.TypeName))
            .OrderBy(item => item.PathId)
            .ThenBy(item => item.ClassId)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(snapshotObjects.Count, parserObjects.Count);
        Assert.Equal(snapshotObjects, parserObjects);
    }

    private static void AssertV2SummaryMatchesParser(JsonElement summaryNode, BaseAssetsContext context)
    {
        Assert.Equal(context.SemanticObjects.Count, summaryNode.GetProperty("objectCount").GetInt32());

        var snapshotTypeCounts = summaryNode.GetProperty("objectTypeCount")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);

        var parserTypeCounts = context.BuildSemanticObjectTypeCounts();
        Assert.Equal(snapshotTypeCounts, parserTypeCounts);
    }

    private static readonly HashSet<string> StrictHierarchyFixtures = new(StringComparer.Ordinal)
    {
        "BambooCopter_head.hhh",
        "BlindEye_body.hhh",
        "BrowSad_head.hhh",
        "Cigar_neck.hhh",
        "Sakuyas hairdo_head.hhh"
    };

    private static void AssertV2HierarchyMatchesParser(string fixtureName, JsonElement hierarchyNode, JsonElement snapshotObjectsNode, BaseAssetsContext context)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        var gameObjects = hierarchyNode.GetProperty("gameObjects").EnumerateArray().ToList();
        var transforms = hierarchyNode.GetProperty("transforms").EnumerateArray().ToList();

        var gameObjectIds = gameObjects.Select(item => item.GetProperty("pathId").GetInt64()).ToHashSet();
        var transformIds = transforms.Select(item => item.GetProperty("pathId").GetInt64()).ToHashSet();

        Assert.Equal(gameObjectIds.Count, gameObjects.Count);
        Assert.Equal(transformIds.Count, transforms.Count);

        var snapshotGameObjects = gameObjects
            .Select(item => (
                PathId: item.GetProperty("pathId").GetInt64(),
                Name: item.GetProperty("name").GetString() ?? string.Empty,
                IsActive: item.GetProperty("isActive").GetBoolean(),
                Layer: item.GetProperty("layer").GetInt32()))
            .OrderBy(item => item.PathId)
            .ToList();

        var parserGameObjects = context.SemanticGameObjects
            .Select(item => (item.PathId, item.Name, item.IsActive, item.Layer))
            .OrderBy(item => item.PathId)
            .ToList();

        Assert.Equal(parserGameObjects.Count, parserGameObjects.Select(item => item.PathId).Distinct().Count());

        var snapshotTransforms = transforms
            .Select(item => new SnapshotTransformRow(
                item.GetProperty("pathId").GetInt64(),
                item.GetProperty("gameObjectPathId").GetInt64(),
                item.GetProperty("parentPathId").ValueKind == JsonValueKind.Null ? null : item.GetProperty("parentPathId").GetInt64(),
                item.GetProperty("childrenPathIds").EnumerateArray().Select(child => child.GetInt64()).OrderBy(child => child).ToArray(),
                ParseVector3(item.GetProperty("localPosition")),
                ParseQuaternion(item.GetProperty("localRotation")),
                ParseVector3(item.GetProperty("localScale"))))
            .OrderBy(item => item.PathId)
            .ToList();

        var parserTransforms = context.SemanticTransforms
            .Select(item => new SnapshotTransformRow(
                item.PathId,
                item.GameObjectPathId,
                item.ParentPathId,
                item.ChildrenPathIds.OrderBy(child => child).ToArray(),
                item.LocalPosition,
                item.LocalRotation,
                item.LocalScale))
            .OrderBy(item => item.PathId)
            .ToList();

        Assert.Equal(parserTransforms.Count, parserTransforms.Select(item => item.PathId).Distinct().Count());

        var hasCompleteHierarchyDecode =
            parserGameObjects.Count == snapshotGameObjects.Count
            && parserTransforms.Count == snapshotTransforms.Count;

        if (StrictHierarchyFixtures.Contains(fixtureName))
        {
            Assert.True(
                hasCompleteHierarchyDecode,
                $"Fixture '{fixtureName}' must decode full hierarchy. Snapshot GO/Transform counts: {snapshotGameObjects.Count}/{snapshotTransforms.Count}, parser counts: {parserGameObjects.Count}/{parserTransforms.Count}");

            Assert.Equal(snapshotGameObjects, parserGameObjects);

            for (var i = 0; i < snapshotTransforms.Count; i++)
            {
                Assert.Equal(snapshotTransforms[i].PathId, parserTransforms[i].PathId);
                Assert.Equal(snapshotTransforms[i].GameObjectPathId, parserTransforms[i].GameObjectPathId);
                Assert.Equal(snapshotTransforms[i].ParentPathId, parserTransforms[i].ParentPathId);
                Assert.Equal(snapshotTransforms[i].ChildrenPathIds, parserTransforms[i].ChildrenPathIds);
                AssertVector3Equal(snapshotTransforms[i].LocalPosition, parserTransforms[i].LocalPosition);
                AssertQuaternionEqual(snapshotTransforms[i].LocalRotation, parserTransforms[i].LocalRotation);
                AssertVector3Equal(snapshotTransforms[i].LocalScale, parserTransforms[i].LocalScale);
            }
        }
        else
        {
            foreach (var parserGameObject in parserGameObjects)
            {
                Assert.Contains(parserGameObject.PathId, gameObjectIds);
            }

            foreach (var parserTransform in parserTransforms)
            {
                Assert.Contains(parserTransform.PathId, transformIds);
                Assert.Contains(parserTransform.GameObjectPathId, gameObjectIds);

                if (parserTransform.ParentPathId.HasValue)
                {
                    Assert.Contains(parserTransform.ParentPathId.Value, transformIds);
                }

                foreach (var childPathId in parserTransform.ChildrenPathIds)
                {
                    Assert.Contains(childPathId, transformIds);
                }
            }
        }

        foreach (var gameObject in gameObjects)
        {
            var pathId = gameObject.GetProperty("pathId").GetInt64();
            Assert.True(objectMap.TryGetValue(pathId, out var classId));
            Assert.Equal(1, classId);
            Assert.Equal(JsonValueKind.Array, gameObject.GetProperty("components").ValueKind);
            _ = gameObject.GetProperty("isActive").GetBoolean();
            _ = gameObject.GetProperty("layer").GetInt32();
            Assert.False(string.IsNullOrWhiteSpace(gameObject.GetProperty("name").GetString()));
        }

        foreach (var transform in transforms)
        {
            var pathId = transform.GetProperty("pathId").GetInt64();
            Assert.True(objectMap.TryGetValue(pathId, out var classId));
            Assert.Equal(4, classId);

            var gameObjectPathId = transform.GetProperty("gameObjectPathId").GetInt64();
            Assert.Contains(gameObjectPathId, gameObjectIds);

            var parentNode = transform.GetProperty("parentPathId");
            if (parentNode.ValueKind != JsonValueKind.Null)
            {
                Assert.Contains(parentNode.GetInt64(), transformIds);
            }

            foreach (var childNode in transform.GetProperty("childrenPathIds").EnumerateArray())
            {
                Assert.Contains(childNode.GetInt64(), transformIds);
            }

            AssertVector3Node(transform.GetProperty("localPosition"));
            AssertQuaternionNode(transform.GetProperty("localRotation"));
            AssertVector3Node(transform.GetProperty("localScale"));
        }
    }

    private static void AssertV2MaterialConsistency(JsonElement materialsNode, JsonElement snapshotObjectsNode)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        foreach (var material in materialsNode.EnumerateArray())
        {
            var pathId = material.GetProperty("pathId").GetInt64();
            Assert.True(objectMap.TryGetValue(pathId, out var classId));
            Assert.Equal(21, classId);

            var shaderPathIdNode = material.GetProperty("shaderPathId");
            if (shaderPathIdNode.ValueKind != JsonValueKind.Null)
            {
                var shaderPathId = shaderPathIdNode.GetInt64();
                Assert.True(objectMap.TryGetValue(shaderPathId, out var shaderClassId));
                Assert.Equal(48, shaderClassId);
            }

            _ = material.GetProperty("name").GetString();
            var properties = material.GetProperty("properties");
            _ = properties.GetProperty("scalars").EnumerateObject().Count();
            _ = properties.GetProperty("vectors").EnumerateObject().Count();

            foreach (var textureProperty in properties.GetProperty("textures").EnumerateObject())
            {
                var textureNode = textureProperty.Value;
                AssertVector2Node(textureNode.GetProperty("offset"));
                AssertVector2Node(textureNode.GetProperty("scale"));

                var texturePathIdNode = textureNode.GetProperty("texturePathId");
                if (texturePathIdNode.ValueKind != JsonValueKind.Null)
                {
                    Assert.True(objectMap.TryGetValue(texturePathIdNode.GetInt64(), out _));
                }
            }
        }
    }

    private static void AssertV2MeshFilterLinksMatchParser(JsonElement snapshotObjectsNode, BaseAssetsContext context)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        var expectedMeshFilterCount = objectMap.Count(item => item.Value == 33);
        Assert.Equal(expectedMeshFilterCount, context.SemanticMeshFilters.Count);

        foreach (var meshFilter in context.SemanticMeshFilters)
        {
            Assert.True(objectMap.TryGetValue(meshFilter.PathId, out var meshFilterClassId));
            Assert.Equal(33, meshFilterClassId);

            Assert.True(objectMap.TryGetValue(meshFilter.GameObjectPathId, out var gameObjectClassId));
            Assert.Equal(1, gameObjectClassId);

            Assert.True(objectMap.TryGetValue(meshFilter.MeshPathId, out var meshClassId));
            Assert.Equal(43, meshClassId);
        }
    }

    private static void AssertV2MeshRendererLinksMatchParser(JsonElement snapshotObjectsNode, BaseAssetsContext context)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        var expectedMeshRendererCount = objectMap.Count(item => item.Value == 23);
        Assert.Equal(expectedMeshRendererCount, context.SemanticMeshRenderers.Count);

        foreach (var meshRenderer in context.SemanticMeshRenderers)
        {
            Assert.True(objectMap.TryGetValue(meshRenderer.PathId, out var meshRendererClassId));
            Assert.Equal(23, meshRendererClassId);

            Assert.True(objectMap.TryGetValue(meshRenderer.GameObjectPathId, out var gameObjectClassId));
            Assert.Equal(1, gameObjectClassId);

            Assert.NotEmpty(meshRenderer.MaterialPathIds);
            foreach (var materialPathId in meshRenderer.MaterialPathIds)
            {
                Assert.True(objectMap.TryGetValue(materialPathId, out var materialClassId));
                Assert.Equal(21, materialClassId);
            }
        }
    }

    private static void AssertV2MaterialCoreMatchesParser(JsonElement materialsNode, BaseAssetsContext context)
    {
        var snapshotMaterials = materialsNode
            .EnumerateArray()
            .Select(material => (
                PathId: material.GetProperty("pathId").GetInt64(),
                Name: material.GetProperty("name").GetString() ?? string.Empty,
                ShaderPathId: material.GetProperty("shaderPathId").ValueKind == JsonValueKind.Null
                    ? (long?)null
                    : material.GetProperty("shaderPathId").GetInt64()))
            .OrderBy(item => item.PathId)
            .ToList();

        var parserMaterials = context.SemanticMaterials
            .Select(item => (item.PathId, item.Name, item.ShaderPathId))
            .OrderBy(item => item.PathId)
            .ToList();

        Assert.Equal(snapshotMaterials, parserMaterials);
    }

    private static void AssertV2MeshCoreMatchesParser(JsonElement meshesNode, BaseAssetsContext context)
    {
        var snapshotMeshes = meshesNode
            .EnumerateArray()
            .Select(mesh => (
                PathId: mesh.GetProperty("pathId").GetInt64(),
                Name: mesh.GetProperty("name").GetString() ?? string.Empty,
                IndexCount: mesh.GetProperty("indexCount").GetInt32(),
                SubMeshCount: mesh.GetProperty("subMeshCount").GetInt32(),
                Topology: mesh.GetProperty("topology").EnumerateArray().Select(item => item.GetInt32()).ToArray(),
                VertexCount: mesh.GetProperty("vertexCount").GetInt32(),
                Center: ParseVector3(mesh.GetProperty("bounds").GetProperty("center")),
                Extent: ParseVector3(mesh.GetProperty("bounds").GetProperty("extent"))))
            .OrderBy(item => item.PathId)
            .ToList();

        var parserMeshes = context.SemanticMeshes
            .Select(mesh => (
                mesh.PathId,
                mesh.Name,
                mesh.IndexCount,
                mesh.SubMeshCount,
                Topology: mesh.Topology.ToArray(),
                mesh.VertexCount,
                mesh.Bounds.Center,
                mesh.Bounds.Extent))
            .OrderBy(item => item.PathId)
            .ToList();

        Assert.Equal(snapshotMeshes.Count, parserMeshes.Count);

        for (var i = 0; i < snapshotMeshes.Count; i++)
        {
            Assert.Equal(snapshotMeshes[i].PathId, parserMeshes[i].PathId);
            Assert.Equal(snapshotMeshes[i].Name, parserMeshes[i].Name);
            Assert.Equal(snapshotMeshes[i].IndexCount, parserMeshes[i].IndexCount);
            Assert.Equal(snapshotMeshes[i].SubMeshCount, parserMeshes[i].SubMeshCount);
            Assert.Equal(snapshotMeshes[i].Topology, parserMeshes[i].Topology);
            Assert.Equal(snapshotMeshes[i].VertexCount, parserMeshes[i].VertexCount);
            AssertVector3Equal(snapshotMeshes[i].Center, parserMeshes[i].Center);
            AssertVector3Equal(snapshotMeshes[i].Extent, parserMeshes[i].Extent);
        }
    }

    private static void AssertV2MeshChannelsMatchParser(JsonElement meshesNode, BaseAssetsContext context)
    {
        var snapshotChannelsByPath = meshesNode
            .EnumerateArray()
            .ToDictionary(
                mesh => mesh.GetProperty("pathId").GetInt64(),
                mesh => mesh.GetProperty("channels"));

        foreach (var mesh in context.SemanticMeshes)
        {
            Assert.True(snapshotChannelsByPath.TryGetValue(mesh.PathId, out var channelsNode));
            Assert.Equal(channelsNode.GetProperty("positions").GetBoolean(), mesh.ChannelFlags.Positions);
            Assert.Equal(channelsNode.GetProperty("normals").GetBoolean(), mesh.ChannelFlags.Normals);
            Assert.Equal(channelsNode.GetProperty("tangents").GetBoolean(), mesh.ChannelFlags.Tangents);
            Assert.Equal(channelsNode.GetProperty("colors").GetBoolean(), mesh.ChannelFlags.Colors);
            Assert.Equal(channelsNode.GetProperty("uv0").GetBoolean(), mesh.ChannelFlags.Uv0);
            Assert.Equal(channelsNode.GetProperty("uv1").GetBoolean(), mesh.ChannelFlags.Uv1);
        }
    }

    private static void AssertV2MeshRenderableSemantics(BaseAssetsContext context)
    {
        foreach (var mesh in context.SemanticMeshes)
        {
            Assert.Equal(mesh.SubMeshCount, mesh.SubMeshes.Count);
            Assert.Equal(mesh.SubMeshCount, mesh.Topology.Count);
            Assert.True(mesh.IndexElementSizeBytes is 2 or 4);
            Assert.Equal(mesh.IndexElementCount * mesh.IndexElementSizeBytes, mesh.IndexCount);
            Assert.True(mesh.VertexDataByteLength >= 0);

            if (mesh.IndexCount > 0)
            {
                Assert.Equal(mesh.IndexElementCount, mesh.DecodedIndices.Count);
            }

            if (mesh.DecodedIndices.Count > 0)
            {
                Assert.Equal(mesh.IndexElementCount, mesh.DecodedIndices.Count);
                if (mesh.VertexCount > 0)
                {
                    Assert.True(mesh.DecodedIndices.Max() < mesh.VertexCount);
                }

                if (mesh.IndexFormat.HasValue)
                {
                    Assert.True(mesh.IndexFormat.Value is 0 or 1);
                    Assert.Equal(mesh.IndexFormat.Value == 0 ? 2 : 4, mesh.IndexElementSizeBytes);
                }
            }

            if (mesh.DecodedPositions.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedPositions.Count);
                Assert.True(mesh.VertexDataByteLength >= mesh.VertexCount * 12);
            }

            if (mesh.DecodedNormals.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedNormals.Count);
                Assert.True(mesh.ChannelFlags.Normals);
            }

            if (mesh.DecodedTangents.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedTangents.Count);
                Assert.True(mesh.ChannelFlags.Tangents);
            }

            if (mesh.DecodedColors.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedColors.Count);
                Assert.True(mesh.ChannelFlags.Colors);
            }

            if (mesh.DecodedUv0.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedUv0.Count);
                Assert.True(mesh.ChannelFlags.Uv0);
            }

            if (mesh.DecodedUv1.Count > 0)
            {
                Assert.Equal(mesh.VertexCount, mesh.DecodedUv1.Count);
                Assert.True(mesh.ChannelFlags.Uv1);
            }

            if (!mesh.ChannelFlags.Normals)
            {
                Assert.Empty(mesh.DecodedNormals);
            }

            if (!mesh.ChannelFlags.Tangents)
            {
                Assert.Empty(mesh.DecodedTangents);
            }

            if (!mesh.ChannelFlags.Colors)
            {
                Assert.Empty(mesh.DecodedColors);
            }

            if (!mesh.ChannelFlags.Uv0)
            {
                Assert.Empty(mesh.DecodedUv0);
            }

            if (!mesh.ChannelFlags.Uv1)
            {
                Assert.Empty(mesh.DecodedUv1);
            }

            if (mesh.VertexChannels.Count > 0)
            {
                Assert.Equal(mesh.VertexChannels.Count, mesh.VertexChannels.Select(channel => channel.ChannelIndex).Distinct().Count());
                foreach (var channel in mesh.VertexChannels)
                {
                    Assert.True(channel.ChannelIndex >= 0);
                    Assert.True(channel.Stream >= 0);
                    Assert.True(channel.Offset >= 0);
                    Assert.True(channel.Format >= 0);
                    Assert.True(channel.Dimension is >= 1 and <= 4);
                }
            }

            if (mesh.VertexStreams.Count > 0)
            {
                Assert.Equal(mesh.VertexStreams.Count, mesh.VertexStreams.Select(stream => stream.Stream).Distinct().Count());
                foreach (var stream in mesh.VertexStreams)
                {
                    Assert.True(stream.Stream >= 0);
                    Assert.True(stream.Offset >= 0);
                    Assert.True(stream.Stride > 0);
                    Assert.True(stream.ByteLength >= 0);
                    Assert.Equal(stream.Stride * mesh.VertexCount, stream.ByteLength);
                    Assert.True(stream.Offset + stream.ByteLength <= mesh.VertexDataByteLength);
                }
            }

            for (var i = 0; i < mesh.SubMeshes.Count; i++)
            {
                var subMesh = mesh.SubMeshes[i];
                Assert.True(subMesh.FirstByte >= 0);
                Assert.True(subMesh.IndexCount >= 0);
                Assert.True(subMesh.FirstVertex >= 0);
                Assert.True(subMesh.VertexCount >= 0);
                Assert.True(subMesh.FirstVertex + subMesh.VertexCount <= mesh.VertexCount);
                Assert.Equal(0, subMesh.FirstByte % mesh.IndexElementSizeBytes);

                var firstIndex = subMesh.FirstByte / mesh.IndexElementSizeBytes;
                Assert.True(firstIndex >= 0);
                Assert.True(firstIndex + subMesh.IndexCount <= mesh.IndexElementCount);
                Assert.Equal(mesh.Topology[i], subMesh.Topology);
            }
        }
    }

    private static void AssertV2TextureCoreMatchesParser(JsonElement texturesNode, BaseAssetsContext context)
    {
        var snapshotTextures = texturesNode
            .EnumerateArray()
            .Select(texture => (
                PathId: texture.GetProperty("pathId").GetInt64(),
                Name: texture.GetProperty("name").GetString() ?? string.Empty,
                Width: texture.GetProperty("width").GetInt32(),
                Height: texture.GetProperty("height").GetInt32(),
                Format: texture.GetProperty("format").GetInt32(),
                MipCount: texture.GetProperty("mipCount").GetInt32()))
            .OrderBy(item => item.PathId)
            .ToList();

        var parserTextures = context.SemanticTextures
            .Select(item => (item.PathId, item.Name, item.Width, item.Height, item.Format, item.MipCount))
            .OrderBy(item => item.PathId)
            .ToList();

        Assert.Equal(snapshotTextures, parserTextures);
    }

    private static void AssertV2MeshConsistency(JsonElement meshesNode, JsonElement snapshotObjectsNode)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        foreach (var mesh in meshesNode.EnumerateArray())
        {
            var pathId = mesh.GetProperty("pathId").GetInt64();
            Assert.True(objectMap.TryGetValue(pathId, out var classId));
            Assert.Equal(43, classId);

            var subMeshCount = mesh.GetProperty("subMeshCount").GetInt32();
            Assert.True(subMeshCount >= 0);
            Assert.Equal(subMeshCount, mesh.GetProperty("topology").GetArrayLength());

            Assert.True(mesh.GetProperty("vertexCount").GetInt32() >= 0);
            Assert.True(mesh.GetProperty("indexCount").GetInt32() >= 0);

            AssertVector3Node(mesh.GetProperty("bounds").GetProperty("center"));
            AssertVector3Node(mesh.GetProperty("bounds").GetProperty("extent"));
        }
    }

    private static void AssertV2TextureConsistency(JsonElement texturesNode, JsonElement snapshotObjectsNode)
    {
        var objectMap = snapshotObjectsNode
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("pathId").GetInt64(),
                element => element.GetProperty("classId").GetInt32());

        foreach (var texture in texturesNode.EnumerateArray())
        {
            var pathId = texture.GetProperty("pathId").GetInt64();
            Assert.True(objectMap.TryGetValue(pathId, out var classId));
            Assert.Equal(28, classId);

            Assert.True(texture.GetProperty("width").GetInt32() >= 0);
            Assert.True(texture.GetProperty("height").GetInt32() >= 0);
            Assert.True(texture.GetProperty("mipCount").GetInt32() >= 0);
            Assert.False(string.IsNullOrWhiteSpace(texture.GetProperty("name").GetString()));
            _ = texture.GetProperty("format").GetInt32();
        }
    }

    private static void AssertV2WarningsConsistency(JsonElement warningsNode, BaseAssetsContext context)
    {
        Assert.Equal(JsonValueKind.Array, warningsNode.ValueKind);

        var snapshotCodes = warningsNode.EnumerateArray()
            .Select(warning =>
            {
                Assert.Equal(JsonValueKind.String, warning.ValueKind);
                var value = warning.GetString();
                Assert.False(string.IsNullOrWhiteSpace(value));
                return NormalizeWarningCode(value!);
            })
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        var parserCodes = context.Warnings
            .Select(NormalizeWarningCode)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(snapshotCodes, parserCodes);
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

        var parserCodes = context.Warnings
            .Select(NormalizeWarningCode)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(snapshotCount, parserCodes.Count);

        if (snapshotCodes.Count > 0)
        {
            Assert.Equal(snapshotCodes, parserCodes);
            return;
        }

        Assert.Empty(parserCodes);
    }

    private static void AssertSnapshotInternalConsistency(JsonElement root, int schemaVersion)
    {
        if (schemaVersion == 1)
        {
            AssertSnapshotInternalConsistencyV1(root);
            return;
        }

        AssertSnapshotInternalConsistencyV2(root);
    }

    private static void AssertSnapshotInternalConsistencyV1(JsonElement root)
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

    private static void AssertSnapshotInternalConsistencyV2(JsonElement root)
    {
        Assert.Equal(JsonValueKind.Array, root.GetProperty("warnings").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("skinning").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("serializedFiles").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("containers").ValueKind);

        var summary = root.GetProperty("summary");
        var objects = root.GetProperty("objects").EnumerateArray().ToList();

        Assert.Equal(objects.Count, summary.GetProperty("objectCount").GetInt32());

        var snapshotTypeCounts = objects
            .GroupBy(item => item.GetProperty("type").GetString() ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var summaryTypeCounts = summary.GetProperty("objectTypeCount")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);

        Assert.Equal(summaryTypeCounts, snapshotTypeCounts);

        var determinismCheckNode = root.GetProperty("determinismCheck");
        Assert.Equal(JsonValueKind.String, determinismCheckNode.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(determinismCheckNode.GetString()));
    }

    private static void AssertVector3Node(JsonElement node)
    {
        _ = node.GetProperty("x").GetDouble();
        _ = node.GetProperty("y").GetDouble();
        _ = node.GetProperty("z").GetDouble();
    }

    private static void AssertQuaternionNode(JsonElement node)
    {
        _ = node.GetProperty("w").GetDouble();
        _ = node.GetProperty("x").GetDouble();
        _ = node.GetProperty("y").GetDouble();
        _ = node.GetProperty("z").GetDouble();
    }

    private static void AssertVector2Node(JsonElement node)
    {
        _ = node.GetProperty("x").GetDouble();
        _ = node.GetProperty("y").GetDouble();
    }

    private static SemanticVector3 ParseVector3(JsonElement node)
    {
        return new SemanticVector3(
            (float)node.GetProperty("x").GetDouble(),
            (float)node.GetProperty("y").GetDouble(),
            (float)node.GetProperty("z").GetDouble());
    }

    private static SemanticQuaternion ParseQuaternion(JsonElement node)
    {
        return new SemanticQuaternion(
            (float)node.GetProperty("w").GetDouble(),
            (float)node.GetProperty("x").GetDouble(),
            (float)node.GetProperty("y").GetDouble(),
            (float)node.GetProperty("z").GetDouble());
    }

    private static void AssertVector3Equal(SemanticVector3 expected, SemanticVector3 actual)
    {
        Assert.True(Math.Abs(expected.X - actual.X) < 1e-4f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 1e-4f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 1e-4f);
    }

    private static void AssertQuaternionEqual(SemanticQuaternion expected, SemanticQuaternion actual)
    {
        Assert.True(Math.Abs(expected.W - actual.W) < 1e-4f);
        Assert.True(Math.Abs(expected.X - actual.X) < 1e-4f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 1e-4f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 1e-4f);
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
    private sealed record SnapshotTransformRow(
        long PathId,
        long GameObjectPathId,
        long? ParentPathId,
        long[] ChildrenPathIds,
        SemanticVector3 LocalPosition,
        SemanticQuaternion LocalRotation,
        SemanticVector3 LocalScale);
}
