using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityAssetParser;

namespace BlazorApp.Services;

/// <summary>
/// Handles on-the-fly composition of avatar + decorations into merged GLB
/// Avatar is stored as raw .unitypackage bytes and decorations as .hhh bytes
/// When user toggles decorations on/off, this service generates a fresh merged GLB
/// </summary>
public sealed class CompositionService
{
    private readonly AssetStoreService _assetStore;
    private readonly HhhParser _hhhParser = new();

    public CompositionService(AssetStoreService assetStore)
    {
        _assetStore = assetStore;
    }

    /// <summary>
    /// Generate a merged GLB containing avatar + selected decorations
    /// </summary>
    /// <param name="avatarId">ID of the avatar (stored as raw .unitypackage bytes)</param>
    /// <param name="decorationShas">List of decoration SHAs to merge (stored as raw .hhh bytes)</param>
    /// <param name="cancellationToken"></param>
    /// <returns>GLB bytes if successful, null if composition failed</returns>
    public async Task<byte[]?> ComposeAvatarWithDecorationsAsync(
        string avatarId,
        IReadOnlyList<string> decorationShas,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load avatar
            var avatar = await _assetStore.GetAvatarByIdAsync(avatarId, cancellationToken);
            if (avatar?.Glb is null || avatar.Glb.Length == 0)
            {
                return null;
            }

            // Parse avatar to context (raw .unitypackage bytes)
            var parser = new UnityPackageParser();
            var context = parser.Parse(avatar.Glb);

            Console.WriteLine($"[CompositionService] Avatar context before merge - Materials: {context.SemanticMaterials.Count}, Textures: {context.SemanticTextures.Count}, Objects: {context.SemanticObjects.Count}");

            // Merge each selected decoration into context
            foreach (var sha256 in decorationShas)
            {
                var decorationAsset = await GetDecorationAsync(sha256, cancellationToken);
                if (decorationAsset?.Glb is null || decorationAsset.Glb.Length == 0)
                {
                    continue;  // Skip decorations that can't be loaded
                }

                // Extract bone name from decoration source path (which contains the original filename with body part tag)
                var boneName = ExtractBoneName(decorationAsset.SourcePath);
                Console.WriteLine($"[CompositionService] Merging decoration '{decorationAsset.Name}' (source: {decorationAsset.SourcePath}) -> bone tag '{boneName ?? "null"}'");

                // Merge decoration into context
                var merged = _hhhParser.TryMergeDecorationIntoContext(
                    decorationAsset.Glb,  // Raw .hhh bytes
                    context,
                    boneName);
                
                if (!merged)
                {
                    Console.WriteLine($"[CompositionService] Failed to merge decoration '{decorationAsset.Name}'");
                }
            }

            Console.WriteLine($"[CompositionService] Avatar context after merge - Materials: {context.SemanticMaterials.Count}, Textures: {context.SemanticTextures.Count}, Objects: {context.SemanticObjects.Count}");

            // Generate GLB from merged context
            var glbBytes = _hhhParser.ConvertToGlbFromContext(context);
            return glbBytes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CompositionService] Composition failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generate GLB for avatar-only (no decorations)
    /// </summary>
    /// <param name="avatarId">ID of the avatar (stored as raw .unitypackage bytes)</param>
    /// <param name="cancellationToken"></param>
    /// <returns>GLB bytes if successful, null if composition failed</returns>
    public async Task<byte[]?> GetAvatarGlbAsync(
        string avatarId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine($"[CompositionService] Loading avatar: {avatarId}");
            
            // Load avatar
            var avatar = await _assetStore.GetAvatarByIdAsync(avatarId, cancellationToken);
            if (avatar?.Glb is null || avatar.Glb.Length == 0)
            {
                Console.WriteLine($"[CompositionService] Avatar not found or empty");
                return null;
            }

            Console.WriteLine($"[CompositionService] Avatar loaded: {avatar.Glb.Length} bytes");

            // Parse avatar to context (raw .unitypackage bytes)
            var parser = new UnityPackageParser();
            var context = parser.Parse(avatar.Glb);
            
            Console.WriteLine($"[CompositionService] Context parsed: {context.SemanticGameObjects.Count} GameObjects, {context.SemanticMeshes.Count} Meshes, {context.SemanticMaterials.Count} Materials, {context.SemanticTextures.Count} Textures");

            // Generate GLB from context
            var glbBytes = _hhhParser.ConvertToGlbFromContext(context);
            Console.WriteLine($"[CompositionService] GLB generated: {glbBytes.Length} bytes");
            return glbBytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CompositionService] Avatar GLB generation failed: {ex.Message}");
            Console.WriteLine($"[CompositionService] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Get a decoration asset by SHA256
    /// </summary>
    private async Task<StoredAsset?> GetDecorationAsync(string sha256, CancellationToken cancellationToken)
    {
        try
        {
            return await _assetStore.GetAssetByIdAsync(sha256, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract bone name from decoration filename
    /// Expected format: "DisplayName_bonename.hhh" or "DisplayName-bonename.hhh"
    /// Examples: "Cigar_neck.hhh" → "neck", "Glasses-head.hhh" → "head"
    /// </summary>
    private static string? ExtractBoneName(string decorationName)
    {
        if (string.IsNullOrWhiteSpace(decorationName))
        {
            return null;
        }

        // Match pattern: anything followed by underscore or dash, then word characters, then optional .hhh
        var match = Regex.Match(decorationName, @"[_\-](\w+)(?:\.hhh)?$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant();
        }

        return null;
    }
}
