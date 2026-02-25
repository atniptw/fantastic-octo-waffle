using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class SceneExtractorTests
{
    [Fact]
    public void ParseUnityPackage_IncludesAvatarCandidatesAndReferenceMetadata()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MoreHead-Asset-Pack_v1.3.unitypackage");
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        var extractor = new SceneExtractor(new ArchiveScanner(), new ModParser());

        var result = extractor.ParseUnityPackage(fixturePath);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Scene);
        Assert.Equal("unitypackage", result.Scene.Container.SourceType);
        Assert.NotEmpty(result.Scene.Assets);
        Assert.NotEmpty(result.Scene.ObjectRefs);
        Assert.NotNull(result.Scene.RenderObjects);
        Assert.NotNull(result.Scene.RenderPrimitives);
        Assert.NotNull(result.Scene.RenderMeshes);
        Assert.NotNull(result.Scene.RenderMaterials);
        Assert.NotNull(result.Scene.RenderTextures);
        Assert.NotEmpty(result.Scene.AvatarAssetIds);
        Assert.Contains(result.Scene.Assets, asset => asset.IsAvatarCandidate);
        Assert.Contains(result.Scene.Assets, asset => !string.IsNullOrWhiteSpace(asset.PackageGuid));
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "mesh");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "texture");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "material");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "prefab");
        Assert.Contains(result.Scene.Assets, asset => asset.ReferencedGuids.Count > 0);
        Assert.Contains(result.Scene.ObjectRefs, objectRef => objectRef.ObjectId.Contains(":obj:", StringComparison.Ordinal));
        Assert.NotNull(result.Scene.Graph);
        Assert.NotEmpty(result.Scene.Graph.Nodes);
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "contains");
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "describes");
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "guid-ref");

        if (result.Scene.ObjectRefs.Any(objectRef => objectRef.ClassId is not null))
        {
            Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "typed-as");
            Assert.Contains(result.Scene.Graph.Nodes, node => node.Kind == "unity-class");
        }

        if (result.Scene.ObjectRefs.Any(objectRef => objectRef.OutboundObjectRefs.Count > 0))
        {
            Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "object-ref");
            Assert.Contains(
                result.Scene.Graph.RefLinks,
                link => link.Status is "object-resolved" or "object-unresolved" or "object-external" or "object-asset-resolved");

            if (result.Scene.ObjectRefs.SelectMany(objectRef => objectRef.OutboundObjectRefs).Any(pointer => pointer.FileId > 0))
            {
                Assert.Contains(
                    result.Scene.Graph.RefLinks,
                    link => link.Status == "object-external"
                            && link.TargetGuid.Contains(":obj:", StringComparison.Ordinal));
            }
        }

        Assert.Contains(result.Scene.Graph.RefLinks, link => link.Status == "resolved" || link.Status == "unresolved");

        if (result.Scene.RenderObjects.Count > 0)
        {
            Assert.Contains(
                result.Scene.RenderObjects,
                item => item.Kind is "transform" or "gameobject" or "meshfilter" or "meshrenderer" or "skinnedmeshrenderer");

            if (result.Scene.RenderObjects.Any(item => item.MaterialAssignments.Count > 0))
            {
                Assert.Contains(
                    result.Scene.RenderObjects,
                    item => item.MaterialAssignments.All(assignment => assignment.SubMeshIndex >= 0));
            }
        }

        if (result.Scene.RenderPrimitives.Count > 0)
        {
            Assert.Contains(result.Scene.RenderPrimitives, item => item.PrimitiveId.StartsWith("primitive:", StringComparison.Ordinal));
            Assert.Contains(result.Scene.RenderPrimitives, item => item.SubMeshIndex >= 0 && !string.IsNullOrWhiteSpace(item.MeshObjectId));
        }

        if (result.Scene.RenderMeshes.Count > 0)
        {
            Assert.Contains(result.Scene.RenderMeshes, mesh => mesh.ObjectId.Contains(":obj:", StringComparison.Ordinal));
            Assert.Contains(
                result.Scene.RenderMeshes,
                mesh => mesh.VertexCount is not null || mesh.VertexDataByteCount is not null || mesh.IndexBufferElementCount is not null);

            if (result.Scene.RenderMeshes.Any(mesh => !string.IsNullOrWhiteSpace(mesh.VertexDataBase64) || mesh.IndexValues is { Count: > 0 }))
            {
                Assert.Contains(
                    result.Scene.RenderMeshes,
                    mesh => !string.IsNullOrWhiteSpace(mesh.VertexDataBase64) || mesh.IndexValues is { Count: > 0 });
            }

            if (result.Scene.RenderMeshes.Any(mesh => mesh.Positions is { Count: > 0 } || mesh.Normals is { Count: > 0 } || mesh.Uv0 is { Count: > 0 }))
            {
                Assert.Contains(
                    result.Scene.RenderMeshes,
                    mesh => (mesh.Positions is { Count: > 0 } && mesh.Positions.Count % 3 == 0)
                            || (mesh.Normals is { Count: > 0 } && mesh.Normals.Count % 3 == 0)
                            || (mesh.Uv0 is { Count: > 0 } && mesh.Uv0.Count % 2 == 0));
            }

            if (result.Scene.RenderMeshes.Any(mesh => mesh.SubMeshes.Count > 0))
            {
                Assert.Contains(
                    result.Scene.RenderMeshes,
                    mesh => mesh.SubMeshes.All(subMesh => subMesh.SubMeshIndex >= 0));
            }
        }

        if (result.Scene.RenderMaterials.Count > 0)
        {
            Assert.Contains(result.Scene.RenderMaterials, material => material.ObjectId.Contains(":obj:", StringComparison.Ordinal));
            Assert.Contains(
                result.Scene.RenderMaterials,
                material => material.TextureBindings.Count > 0 || material.FloatProperties.Count > 0 || material.ColorProperties.Count > 0);
        }

        if (result.Scene.RenderTextures.Count > 0)
        {
            Assert.Contains(result.Scene.RenderTextures, texture => texture.ObjectId.Contains(":obj:", StringComparison.Ordinal));
            Assert.Contains(
                result.Scene.RenderTextures,
                texture => texture.Width is not null || texture.Height is not null || texture.ImageDataByteCount is not null || texture.StreamSize is not null);
        }
    }

    [Fact]
    public void ParseCosmeticBundle_ProducesAttachmentHints()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FoxMask_head.hhh");
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        var extractor = new SceneExtractor(new ArchiveScanner(), new ModParser());

        var result = extractor.ParseCosmeticBundle(fixturePath);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Scene);
        Assert.Empty(result.Scene.RenderObjects);
        Assert.Empty(result.Scene.RenderPrimitives);
        Assert.Empty(result.Scene.RenderMeshes);
        Assert.Empty(result.Scene.RenderMaterials);
        Assert.Empty(result.Scene.RenderTextures);
        Assert.Equal("hhh", result.Scene.Container.SourceType);
        var asset = Assert.Single(result.Scene.Assets);
        Assert.True(asset.IsCosmeticCandidate);
        Assert.Equal("head", asset.SlotTag);
        Assert.Equal("cosmetic-bundle", asset.AssetKind);

        var hint = Assert.Single(result.Scene.AttachmentHints);
        Assert.Equal("head", hint.SlotTag);
        Assert.NotEmpty(hint.CandidateBoneNames);
        Assert.NotEmpty(hint.CandidateNodePaths);
        Assert.Contains(result.Scene.Warnings, warning => warning.Contains("avatar-package context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "contains");
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "describes");
    }

    [Fact]
    public void ParseCosmeticBundle_CigarFixture_ReportsNeckAttachmentContext()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cigar_neck.hhh");
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        var extractor = new SceneExtractor(new ArchiveScanner(), new ModParser());

        var result = extractor.ParseCosmeticBundle(fixturePath);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Scene);
        Assert.Empty(result.Scene.RenderObjects);
        Assert.Empty(result.Scene.RenderPrimitives);
        Assert.Empty(result.Scene.RenderMeshes);
        Assert.Empty(result.Scene.RenderMaterials);
        Assert.Empty(result.Scene.RenderTextures);

        var asset = Assert.Single(result.Scene.Assets);
        Assert.Equal("neck", asset.SlotTag);
        Assert.Equal("cosmetic-bundle", asset.AssetKind);

        var hint = Assert.Single(result.Scene.AttachmentHints);
        Assert.Equal("neck", hint.SlotTag);
        Assert.Contains(hint.CandidateBoneNames, name => name.Contains("neck", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Scene.Warnings, warning => warning.Contains("avatar-package context", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(result.Scene.Graph.Nodes);
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "contains");
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "describes");
        Assert.Contains(result.Scene.Graph.Edges, edge => edge.EdgeKind == "has-warning");
    }
}
