using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityAssetParser;

public static class PackageInventoryReport
{
    public static string Build(BaseAssetsContext context, int maxItemsPerSection = 20)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var inventory = context.Inventory;
        var builder = new StringBuilder();

        AppendHeader(builder);
        AppendContainers(builder, context);
        AppendEntries(builder, inventory, maxItemsPerSection);
        AppendResolvedPaths(builder, inventory, maxItemsPerSection);
        AppendSemanticCounts(builder, context);
        AppendAnchors(builder, context);
        AppendGameObjects(builder, context, maxItemsPerSection);
        AppendMeshes(builder, context, maxItemsPerSection);
        AppendMaterials(builder, context, maxItemsPerSection);
        AppendTextures(builder, context, maxItemsPerSection);

        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("Unity Package Inventory Report");
        builder.AppendLine(new string('=', 30));
        builder.AppendLine();
    }

    private static void AppendContainers(StringBuilder builder, BaseAssetsContext context)
    {
        builder.AppendLine("Containers");
        builder.AppendLine("----------");
        if (context.Containers.Count == 0)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
            return;
        }

        foreach (var container in context.Containers)
        {
            builder.AppendLine($"- {container.SourceName} | {container.Kind} | size={container.Size} | entries={container.Entries.Count}");
        }

        builder.AppendLine();
    }

    private static void AppendEntries(StringBuilder builder, PackageInventory inventory, int maxItems)
    {
        builder.AppendLine("Entries");
        builder.AppendLine("-------");
        builder.AppendLine($"Total entries: {inventory.Entries.Count}");
        builder.AppendLine($"Asset entries: {inventory.Entries.Count(entry => entry.Path.EndsWith("/asset", StringComparison.OrdinalIgnoreCase))}");
        builder.AppendLine($"Pathname entries: {inventory.Entries.Count(entry => entry.Path.EndsWith("/pathname", StringComparison.OrdinalIgnoreCase))}");

        var byExtension = inventory.Entries
            .GroupBy(entry => GetExtensionLabel(entry.Path))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (byExtension.Count > 0)
        {
            builder.AppendLine("Top extensions:");
            foreach (var group in byExtension)
            {
                builder.AppendLine($"- {group.Key}: {group.Count()}");
            }
        }
        else
        {
            builder.AppendLine("Top extensions: (none)");
        }

        builder.AppendLine();
    }

    private static void AppendResolvedPaths(StringBuilder builder, PackageInventory inventory, int maxItems)
    {
        builder.AppendLine("Resolved asset paths");
        builder.AppendLine("--------------------");

        var resolved = inventory.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ResolvedPath))
            .Select(entry => entry.ResolvedPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine($"Total resolved paths: {resolved.Count}");

        var byExtension = resolved
            .GroupBy(GetExtensionLabel)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (byExtension.Count > 0)
        {
            builder.AppendLine("Top path extensions:");
            foreach (var group in byExtension)
            {
                builder.AppendLine($"- {group.Key}: {group.Count()}");
            }
        }
        else
        {
            builder.AppendLine("Top path extensions: (none)");
        }

        var modelCandidates = resolved
            .Where(path => LooksLikeModelPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        builder.AppendLine("Model candidates (path heuristic):");
        if (modelCandidates.Count == 0)
        {
            builder.AppendLine("- (none found)");
        }
        else
        {
            foreach (var candidate in modelCandidates)
            {
                builder.AppendLine($"- {candidate}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendSemanticCounts(StringBuilder builder, BaseAssetsContext context)
    {
        builder.AppendLine("Semantic object counts");
        builder.AppendLine("----------------------");
        var counts = context.BuildSemanticObjectTypeCounts();
        if (counts.Count == 0)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
            return;
        }

        foreach (var entry in counts)
        {
            builder.AppendLine($"- {entry.Key}: {entry.Value}");
        }

        builder.AppendLine();
    }

    private static void AppendGameObjects(StringBuilder builder, BaseAssetsContext context, int maxItems)
    {
        builder.AppendLine("GameObjects");
        builder.AppendLine("-----------");
        builder.AppendLine($"Total: {context.SemanticGameObjects.Count}");
        builder.AppendLine($"Transforms: {context.SemanticTransforms.Count}");

        var meshFilterCounts = context.SemanticMeshFilters
            .GroupBy(filter => filter.GameObjectPathId)
            .ToDictionary(group => group.Key, group => group.Count());

        var meshRendererMaterialCounts = context.SemanticMeshRenderers
            .ToDictionary(renderer => renderer.GameObjectPathId, renderer => renderer.MaterialPathIds.Count);

        var transformByGameObject = context.SemanticTransforms
            .GroupBy(transform => transform.GameObjectPathId)
            .ToDictionary(group => group.Key, group => group.First());

        var transformChildCounts = transformByGameObject
            .ToDictionary(pair => pair.Key, pair => pair.Value.ChildrenPathIds.Count);

        var ranked = context.SemanticGameObjects
            .Select(gameObject => new
            {
                gameObject.PathId,
                gameObject.Name,
                gameObject.IsActive,
                gameObject.Layer,
                MeshFilters = GetCount(meshFilterCounts, gameObject.PathId),
                Materials = GetCount(meshRendererMaterialCounts, gameObject.PathId),
                Children = GetCount(transformChildCounts, gameObject.PathId)
            })
            .OrderByDescending(item => item.MeshFilters)
            .ThenByDescending(item => item.Children)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (ranked.Count > 0)
        {
            builder.AppendLine("Top GameObjects by mesh count:");
            foreach (var item in ranked)
            {
                builder.AppendLine($"- {item.Name} | path={item.PathId} | meshes={item.MeshFilters} | materials={item.Materials} | children={item.Children} | active={item.IsActive}");
            }
        }
        else
        {
            builder.AppendLine("(none)");
        }

        AppendPlayerCandidates(builder, ranked);
        AppendHeadCandidates(builder, ranked, transformByGameObject);
        builder.AppendLine();
    }

    private static void AppendAnchors(StringBuilder builder, BaseAssetsContext context)
    {
        builder.AppendLine("Anchor points");
        builder.AppendLine("-------------");
        if (context.SemanticAnchorPoints.Count == 0)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
            return;
        }

        var transformByGameObject = context.SemanticTransforms
            .GroupBy(transform => transform.GameObjectPathId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var anchor in context.SemanticAnchorPoints.OrderBy(item => item.Tag, StringComparer.OrdinalIgnoreCase))
        {
            var transformInfo = transformByGameObject.TryGetValue(anchor.GameObjectPathId, out SemanticTransformInfo? transform)
                ? $" | pos=({transform.LocalPosition.X:F3},{transform.LocalPosition.Y:F3},{transform.LocalPosition.Z:F3})"
                : string.Empty;
            builder.AppendLine($"- {anchor.Tag} | {anchor.Name} | path={anchor.GameObjectPathId}{transformInfo}");
        }

        builder.AppendLine();
    }

    private static void AppendPlayerCandidates(StringBuilder builder, IReadOnlyList<dynamic> ranked)
    {
        var candidates = ranked
            .Where(item => LooksLikePlayerModel(item.Name))
            .ToList();

        builder.AppendLine("Player model candidates (name heuristic):");
        if (candidates.Count == 0)
        {
            builder.AppendLine("- (none found; try expanding filters or inspect GameObjects with high mesh counts)");
            return;
        }

        foreach (var item in candidates)
        {
            builder.AppendLine($"- {item.Name} | path={item.PathId} | meshes={item.MeshFilters} | materials={item.Materials} | children={item.Children}");
        }
    }

    private static void AppendHeadCandidates(
        StringBuilder builder,
        IReadOnlyList<dynamic> ranked,
        IReadOnlyDictionary<long, SemanticTransformInfo> transformByGameObject)
    {
        var candidates = ranked
            .Where(item => LooksLikeHeadAttachPoint(item.Name))
            .ToList();

        builder.AppendLine("Head/attach-point candidates (name heuristic):");
        if (candidates.Count == 0)
        {
            builder.AppendLine("- (none found)");
            return;
        }

        foreach (var item in candidates)
        {
            SemanticTransformInfo? transform;
            var transformInfo = transformByGameObject.TryGetValue(item.PathId, out transform)
                ? $" | pos=({transform.LocalPosition.X:F3},{transform.LocalPosition.Y:F3},{transform.LocalPosition.Z:F3})"
                : string.Empty;
            builder.AppendLine($"- {item.Name} | path={item.PathId} | meshes={item.MeshFilters} | children={item.Children}{transformInfo}");
        }
    }

    private static void AppendMeshes(StringBuilder builder, BaseAssetsContext context, int maxItems)
    {
        builder.AppendLine("Meshes");
        builder.AppendLine("------");
        builder.AppendLine($"Total: {context.SemanticMeshes.Count}");

        var topMeshes = context.SemanticMeshes
            .OrderByDescending(mesh => mesh.VertexCount)
            .ThenBy(mesh => mesh.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (topMeshes.Count > 0)
        {
            builder.AppendLine("Top meshes by vertex count:");
            foreach (var mesh in topMeshes)
            {
                builder.AppendLine($"- {mesh.Name} | path={mesh.PathId} | vertices={mesh.VertexCount} | submeshes={mesh.SubMeshCount}");
            }
        }
        else
        {
            builder.AppendLine("(none)");
        }

        builder.AppendLine();
    }

    private static void AppendMaterials(StringBuilder builder, BaseAssetsContext context, int maxItems)
    {
        builder.AppendLine("Materials");
        builder.AppendLine("---------");
        builder.AppendLine($"Total: {context.SemanticMaterials.Count}");

        var topMaterials = context.SemanticMaterials
            .OrderBy(material => material.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (topMaterials.Count > 0)
        {
            foreach (var material in topMaterials)
            {
                builder.AppendLine($"- {material.Name} | path={material.PathId} | shader={material.ShaderPathId}");
            }
        }
        else
        {
            builder.AppendLine("(none)");
        }

        builder.AppendLine();
    }

    private static void AppendTextures(StringBuilder builder, BaseAssetsContext context, int maxItems)
    {
        builder.AppendLine("Textures");
        builder.AppendLine("--------");
        builder.AppendLine($"Total: {context.SemanticTextures.Count}");

        var topTextures = context.SemanticTextures
            .OrderByDescending(texture => texture.Width * texture.Height)
            .ThenBy(texture => texture.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        if (topTextures.Count > 0)
        {
            foreach (var texture in topTextures)
            {
                builder.AppendLine($"- {texture.Name} | path={texture.PathId} | size={texture.Width}x{texture.Height} | format={texture.Format} | mips={texture.MipCount}");
            }
        }
        else
        {
            builder.AppendLine("(none)");
        }

        builder.AppendLine();
    }

    private static string GetExtensionLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(none)";
        }

        var extension = System.IO.Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "(none)";
        }

        return extension.TrimStart('.');
    }

    private static int GetCount<TKey>(IReadOnlyDictionary<TKey, int> map, TKey key)
        where TKey : notnull
    {
        return map.TryGetValue(key, out var value) ? value : 0;
    }

    private static bool LooksLikePlayerModel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.ToLowerInvariant();
        return normalized.Contains("player")
            || normalized.Contains("character")
            || normalized.Contains("avatar")
            || normalized.Contains("body")
            || normalized.Contains("human")
            || normalized.Contains("pawn")
            || normalized.Contains("mannequin");
    }

    private static bool LooksLikeModelPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.ToLowerInvariant();
        if (normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.Contains("player")
            || normalized.Contains("character")
            || normalized.Contains("avatar")
            || normalized.Contains("body")
            || normalized.Contains("head");
    }

    private static bool LooksLikeHeadAttachPoint(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.ToLowerInvariant();
        return normalized.Contains("head")
            || normalized.Contains("attach point")
            || normalized.Contains("attach")
            || normalized.Contains("jaw")
            || normalized.Contains("hat");
    }
}
