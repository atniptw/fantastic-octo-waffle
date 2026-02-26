using RepoMod.Glb.Contracts;
using RepoMod.Glb.Implementation;
using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.UnitTests;

public class GlbCompositionPlannerTests
{
    [Fact]
    public void BuildPlan_UsesSlotFallbackAndDeterministicSelectionOrder()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = new ConverterScene("scene:avatar", "container", "unitypackage", [], [], []);

        var selections = new[]
        {
            new GlbCosmeticSelection("z-item", "unknown", CreateCosmeticScene("primitive:z"), null, Enabled: true),
            new GlbCosmeticSelection("a-item", "head", CreateCosmeticScene("primitive:a"), null, Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        Assert.Equal("scene:avatar", plan.AvatarSceneId);
        Assert.Equal(2, plan.Attachments.Count);
        Assert.Equal("a-item", plan.Attachments[0].SelectionId);
        Assert.Equal("z-item", plan.Attachments[1].SelectionId);
        Assert.Equal("head", plan.Attachments[1].ResolvedSlot);
        Assert.Contains(plan.Warnings, item => item.Contains("unknown slot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPlan_PrefersCandidateNodePath_WhenAvailable()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = new ConverterScene("scene:avatar", "container", "unitypackage", [], [], []);

        var selections = new[]
        {
            new GlbCosmeticSelection(
                "candidate-item",
                "leftarm",
                CreateCosmeticScene("primitive:1"),
                ["PlayerAvatar/Armature/Hips/Spine_01/CustomAnchor"],
                Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        var decision = Assert.Single(plan.Attachments);
        Assert.True(decision.ResolvedFromCandidate);
        Assert.Equal("PlayerAvatar/Armature/Hips/Spine_01/CustomAnchor", decision.TargetAnchorPath);
    }

    [Fact]
    public void BuildPlan_EmitsWorldFollowerWarning_ForWorldSlot()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = new ConverterScene("scene:avatar", "container", "unitypackage", [], [], []);

        var selections = new[]
        {
            new GlbCosmeticSelection("world-item", "world", CreateCosmeticScene("primitive:w"), null, Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        var decision = Assert.Single(plan.Attachments);
        Assert.Equal("world", decision.TargetAnchorPath);
        Assert.Contains(plan.Warnings, item => item.Contains("world-space", StringComparison.OrdinalIgnoreCase));
    }

    private static ConverterScene CreateCosmeticScene(string primitiveId)
    {
        var primitive = new ConverterPrimitive(
            primitiveId,
            "renderer:1",
            "node:1",
            "mesh:1",
            null,
            0,
            0,
            [0, 1, 2],
            [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            null,
            null,
            null,
            null,
            null);

        return new ConverterScene(
            "scene:cosmetic",
            "container",
            "hhh",
            [],
            [primitive],
            []);
    }
}
