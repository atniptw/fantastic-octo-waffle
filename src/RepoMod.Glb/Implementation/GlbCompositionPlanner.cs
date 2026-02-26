using RepoMod.Glb.Abstractions;
using RepoMod.Glb.Contracts;
using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Implementation;

public sealed class GlbCompositionPlanner : IGlbCompositionPlanner
{
    private static readonly IReadOnlyDictionary<string, string> SlotAnchorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["head"] = "PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Neck_01/Head",
        ["neck"] = "PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Neck_01",
        ["body"] = "PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03",
        ["hip"] = "PlayerAvatar/Armature/Hips",
        ["leftarm"] = "PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Upperarm_L",
        ["rightarm"] = "PlayerAvatar/Armature/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Upperarm_R",
        ["leftleg"] = "PlayerAvatar/Armature/Hips/Thigh_L",
        ["rightleg"] = "PlayerAvatar/Armature/Hips/Thigh_R",
        ["world"] = "world"
    };

    public GlbCompositionPlan BuildPlan(
        ConverterScene avatarScene,
        IReadOnlyList<GlbCosmeticSelection> selections,
        GlbCompositionOptions? options = null)
    {
        options ??= new GlbCompositionOptions();

        var warnings = new List<string>();
        var attachments = new List<GlbAttachmentDecision>();

        foreach (var selection in selections.Where(item => item.Enabled).OrderBy(item => item.SelectionId, StringComparer.Ordinal))
        {
            var requestedSlot = NormalizeSlot(selection.SlotTag, options.UnknownSlotFallback);
            var resolvedSlot = SlotAnchorMap.ContainsKey(requestedSlot)
                ? requestedSlot
                : NormalizeSlot(options.UnknownSlotFallback, "head");

            if (!SlotAnchorMap.ContainsKey(requestedSlot))
            {
                warnings.Add($"Selection '{selection.SelectionId}' used unknown slot '{selection.SlotTag}', falling back to '{resolvedSlot}'.");
            }

            var targetAnchorPath = ResolveAnchorPath(selection, resolvedSlot, out var fromCandidate);
            if (targetAnchorPath == "world")
            {
                warnings.Add($"Selection '{selection.SelectionId}' is marked as world-space and requires runtime follower behavior.");
            }

            var primitiveIds = selection.Scene.Primitives
                .OrderBy(item => item.PrimitiveId, StringComparer.Ordinal)
                .Select(item => item.PrimitiveId)
                .ToArray();

            if (primitiveIds.Length == 0)
            {
                warnings.Add($"Selection '{selection.SelectionId}' has no primitives in converter scene.");
            }

            attachments.Add(new GlbAttachmentDecision(
                selection.SelectionId,
                requestedSlot,
                resolvedSlot,
                targetAnchorPath,
                fromCandidate,
                primitiveIds));
        }

        return new GlbCompositionPlan(avatarScene.SceneId, attachments, warnings);
    }

    private static string ResolveAnchorPath(GlbCosmeticSelection selection, string resolvedSlot, out bool fromCandidate)
    {
        fromCandidate = false;
        if (selection.CandidateNodePaths is { Count: > 0 })
        {
            foreach (var candidate in selection.CandidateNodePaths)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    fromCandidate = true;
                    return candidate.Trim();
                }
            }
        }

        return SlotAnchorMap[resolvedSlot];
    }

    private static string NormalizeSlot(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant();
    }
}
