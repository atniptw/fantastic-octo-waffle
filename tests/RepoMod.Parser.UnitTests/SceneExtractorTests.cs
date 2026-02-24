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
        Assert.NotEmpty(result.Scene.AvatarAssetIds);
        Assert.Contains(result.Scene.Assets, asset => asset.IsAvatarCandidate);
        Assert.Contains(result.Scene.Assets, asset => !string.IsNullOrWhiteSpace(asset.PackageGuid));
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "mesh");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "texture");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "material");
        Assert.Contains(result.Scene.Assets, asset => asset.AssetKind == "prefab");
        Assert.Contains(result.Scene.Assets, asset => asset.ReferencedGuids.Count > 0);
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

        var asset = Assert.Single(result.Scene.Assets);
        Assert.Equal("neck", asset.SlotTag);
        Assert.Equal("cosmetic-bundle", asset.AssetKind);

        var hint = Assert.Single(result.Scene.AttachmentHints);
        Assert.Equal("neck", hint.SlotTag);
        Assert.Contains(hint.CandidateBoneNames, name => name.Contains("neck", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Scene.Warnings, warning => warning.Contains("avatar-package context", StringComparison.OrdinalIgnoreCase));
    }
}
