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
        var avatar = CreateAvatarScene();

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
        Assert.Equal("PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Neck_01/Head", plan.Attachments[0].TargetAnchorPath);
        Assert.Contains(plan.Warnings, item => item.Contains("unknown slot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPlan_PrefersCandidateNodePath_WhenAvailable()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = CreateAvatarScene();

        var selections = new[]
        {
            new GlbCosmeticSelection(
                "candidate-item",
                "leftarm",
                CreateCosmeticScene("primitive:1"),
                ["PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Upperarm_L"],
                Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        var decision = Assert.Single(plan.Attachments);
        Assert.True(decision.ResolvedFromCandidate);
        Assert.Equal("PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Upperarm_L", decision.TargetAnchorPath);
    }

    [Fact]
    public void BuildPlan_EmitsWorldFollowerWarning_ForWorldSlot()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = CreateAvatarScene();

        var selections = new[]
        {
            new GlbCosmeticSelection("world-item", "world", CreateCosmeticScene("primitive:w"), null, Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        var decision = Assert.Single(plan.Attachments);
        Assert.Equal("world", decision.TargetAnchorPath);
        Assert.Contains(plan.Warnings, item => item.Contains("world-space", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPlan_FallsBackToNearestExistingAnchor_WhenSlotAnchorMissing()
    {
        var planner = new GlbCompositionPlanner();
        var avatar = CreateAvatarSceneWithoutHeadNode();

        var selections = new[]
        {
            new GlbCosmeticSelection("head-item", "head", CreateCosmeticScene("primitive:h"), null, Enabled: true)
        };

        var plan = planner.BuildPlan(avatar, selections);

        var decision = Assert.Single(plan.Attachments);
        Assert.Equal("PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Neck_01", decision.TargetAnchorPath);
        Assert.Contains(plan.Warnings, item => item.Contains("nearest existing", StringComparison.OrdinalIgnoreCase));
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

    private static ConverterScene CreateAvatarScene()
    {
        return new ConverterScene(
            "scene:avatar",
            "container",
            "unitypackage",
            [
                new ConverterNode("n0", "PlayerAvatar", null, ["n1"], null, null, null),
                new ConverterNode("n1", "Armature", "n0", ["n2"], null, null, null),
                new ConverterNode("n2", "Hips", "n1", ["n3", "n10", "n11"], null, null, null),
                new ConverterNode("n3", "Spine_01", "n2", ["n4"], null, null, null),
                new ConverterNode("n4", "Spine_02", "n3", ["n5"], null, null, null),
                new ConverterNode("n5", "Spine_03", "n4", ["n6", "n8", "n9"], null, null, null),
                new ConverterNode("n6", "Neck_01", "n5", ["n7"], null, null, null),
                new ConverterNode("n7", "Head", "n6", [], null, null, null),
                new ConverterNode("n8", "Clavicle_L", "n5", ["n12"], null, null, null),
                new ConverterNode("n9", "Clavicle_R", "n5", [], null, null, null),
                new ConverterNode("n10", "Thigh_L", "n2", [], null, null, null),
                new ConverterNode("n11", "Thigh_R", "n2", [], null, null, null),
                new ConverterNode("n12", "Upperarm_L", "n8", [], null, null, null)
            ],
            [],
            []);
    }

    private static ConverterScene CreateAvatarSceneWithoutHeadNode()
    {
        var avatar = CreateAvatarScene();
        var nodes = avatar.Nodes.Where(node => node.NodeId != "n7").ToArray();
        var neck = nodes.Single(node => node.NodeId == "n6") with { ChildNodeIds = [] };
        var rebuilt = nodes.Select(node => node.NodeId == "n6" ? neck : node).ToArray();
        return avatar with { Nodes = rebuilt };
    }
}
