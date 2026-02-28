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
        var avatarPaths = BuildAvatarPathIndex(avatarScene);

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

            var targetAnchorPath = ResolveAnchorPath(selection, resolvedSlot, avatarPaths, warnings, out var fromCandidate);
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

    public GlbCompositionResult BuildComposition(
        ConverterScene avatarScene,
        IReadOnlyList<GlbCosmeticSelection> selections,
        GlbCompositionOptions? options = null)
    {
        var plan = BuildPlan(avatarScene, selections, options);
        var warnings = plan.Warnings.ToList();
        var selectionById = selections.ToDictionary(item => item.SelectionId, StringComparer.Ordinal);

        var anchorGroups = new List<GlbAnchorComposition>();
        foreach (var group in plan.Attachments
                     .OrderBy(item => item.TargetAnchorPath, StringComparer.Ordinal)
                     .GroupBy(item => item.TargetAnchorPath, StringComparer.Ordinal))
        {
            var composedPrimitives = new List<GlbComposedPrimitive>();
            foreach (var attachment in group.OrderBy(item => item.SelectionId, StringComparer.Ordinal))
            {
                if (!selectionById.TryGetValue(attachment.SelectionId, out var selection))
                {
                    warnings.Add($"Attachment references missing selection '{attachment.SelectionId}'.");
                    continue;
                }

                var primitiveById = selection.Scene.Primitives.ToDictionary(item => item.PrimitiveId, StringComparer.Ordinal);
                foreach (var primitiveId in attachment.PrimitiveIds.OrderBy(item => item, StringComparer.Ordinal))
                {
                    if (!primitiveById.TryGetValue(primitiveId, out var primitive))
                    {
                        warnings.Add($"Selection '{selection.SelectionId}' missing referenced primitive '{primitiveId}'.");
                        continue;
                    }

                    composedPrimitives.Add(new GlbComposedPrimitive(
                        selection.SelectionId,
                        attachment.RequestedSlot,
                        attachment.ResolvedSlot,
                        attachment.TargetAnchorPath,
                        primitive));
                }
            }

            anchorGroups.Add(new GlbAnchorComposition(group.Key, composedPrimitives));
        }

        var materialMap = new Dictionary<string, UnityRenderMaterial>(StringComparer.Ordinal);
        var textureMap = new Dictionary<string, UnityRenderTexture>(StringComparer.Ordinal);

        foreach (var material in avatarScene.RenderMaterials)
        {
            materialMap.TryAdd(material.ObjectId, material);
        }

        foreach (var texture in avatarScene.RenderTextures)
        {
            textureMap.TryAdd(texture.ObjectId, texture);
        }

        foreach (var selection in selections)
        {
            foreach (var material in selection.Scene.RenderMaterials)
            {
                materialMap.TryAdd(material.ObjectId, material);
            }

            foreach (var texture in selection.Scene.RenderTextures)
            {
                textureMap.TryAdd(texture.ObjectId, texture);
            }
        }

        return new GlbCompositionResult(
            avatarScene.SceneId,
            anchorGroups,
            plan.Attachments,
            materialMap.Values.ToArray(),
            textureMap.Values.ToArray(),
            warnings);
    }

    private static string ResolveAnchorPath(
        GlbCosmeticSelection selection,
        string resolvedSlot,
        IReadOnlyCollection<string> avatarPaths,
        ICollection<string> warnings,
        out bool fromCandidate)
    {
        fromCandidate = false;
        if (resolvedSlot == "world")
        {
            return "world";
        }

        if (selection.CandidateNodePaths is { Count: > 0 })
        {
            foreach (var candidate in selection.CandidateNodePaths)
            {
                var normalizedCandidate = NormalizePath(candidate);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate)
                    && avatarPaths.Contains(normalizedCandidate, StringComparer.OrdinalIgnoreCase))
                {
                    fromCandidate = true;
                    return normalizedCandidate;
                }
            }

            warnings.Add($"Selection '{selection.SelectionId}' did not match any candidate node path on avatar; using slot anchor fallback.");
        }

        var slotAnchor = NormalizePath(SlotAnchorMap[resolvedSlot]);
        var resolvedAnchor = ResolveNearestExistingAnchor(slotAnchor, avatarPaths);
        if (!string.Equals(slotAnchor, resolvedAnchor, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Selection '{selection.SelectionId}' slot anchor '{slotAnchor}' was not found; using nearest existing '{resolvedAnchor}'.");
        }

        return resolvedAnchor;
    }

    private static string ResolveNearestExistingAnchor(string slotAnchorPath, IReadOnlyCollection<string> avatarPaths)
    {
        if (avatarPaths.Contains(slotAnchorPath, StringComparer.OrdinalIgnoreCase))
        {
            return slotAnchorPath;
        }

        var current = slotAnchorPath;
        while (true)
        {
            var separatorIndex = current.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                return slotAnchorPath;
            }

            current = current[..separatorIndex];
            if (avatarPaths.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                return current;
            }
        }
    }

    private static IReadOnlyCollection<string> BuildAvatarPathIndex(ConverterScene avatarScene)
    {
        if (avatarScene.Nodes.Count == 0)
        {
            return [];
        }

        var nodeById = avatarScene.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);

        string BuildPath(string nodeId)
        {
            if (cache.TryGetValue(nodeId, out var cachedPath))
            {
                return cachedPath;
            }

            if (!nodeById.TryGetValue(nodeId, out var node))
            {
                return string.Empty;
            }

            var nodeName = string.IsNullOrWhiteSpace(node.Name) ? node.NodeId : node.Name.Trim();
            if (string.IsNullOrWhiteSpace(node.ParentNodeId) || !nodeById.ContainsKey(node.ParentNodeId))
            {
                cache[nodeId] = nodeName;
                return nodeName;
            }

            var parentPath = BuildPath(node.ParentNodeId);
            var fullPath = string.IsNullOrWhiteSpace(parentPath)
                ? nodeName
                : $"{parentPath}/{nodeName}";
            cache[nodeId] = fullPath;
            return fullPath;
        }

        return avatarScene.Nodes
            .Select(node => NormalizePath(BuildPath(node.NodeId)))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Replace('\\', '/');
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
