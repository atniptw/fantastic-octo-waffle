using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class ScaleAndAnchoringDiagnosticsTests
{
    private const string FixturesRoot = "fixtures";

    [Fact]
    public void DiagnoseScaleAndAnchoringIssues()
    {
        var packagePath = Path.Combine(FixturesRoot, "MoreHead-UnityPackage", "MoreHead-Asset-Pack_v1.3.unitypackage");
        var decorationsDir = Path.Combine(FixturesRoot, "MoreHead-UnityAssets");

        if (!File.Exists(packagePath))
        {
            return; // Skip if fixture not found
        }

        // Load the package (skeleton + avatar)
        Console.WriteLine("\n=== LOADING UNITY PACKAGE (Avatar Skeleton) ===");
        var packageBytes = File.ReadAllBytes(packagePath);
        var packageParser = new UnityPackageParser();
        var baseContext = packageParser.Parse(packageBytes);

        Console.WriteLine($"Package: {baseContext.SemanticGameObjects.Count} GameObjects, {baseContext.SemanticTransforms.Count} Transforms, {baseContext.SemanticMeshes.Count} Meshes");

        // Analyze skeleton structure
        Console.WriteLine("\n=== SKELETON ANALYSIS ===");
        var boneNames = new[] { "armature", "skeleton", "rig", "root", "hips", "spine", "chest", "neck", "head", 
                                "leftarm", "rightarm", "leftleg", "rightleg", "shoulder", "elbow", "wrist" };
        var bones = baseContext.SemanticGameObjects
            .Where(go => boneNames.Any(bone => go.Name.Contains(bone, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(go => go.Name)
            .ToList();

        Console.WriteLine($"Found {bones.Count} skeleton bones:");
        foreach (var bone in bones)
        {
            var transform = baseContext.SemanticTransforms.FirstOrDefault(t => t.GameObjectPathId == bone.PathId);
            if (transform != null)
            {
                var hasChildren = transform.ChildrenPathIds.Count > 0 ? $"({transform.ChildrenPathIds.Count} children)" : "(leaf)";
                var parent = transform.ParentPathId.HasValue ? baseContext.SemanticGameObjects.FirstOrDefault(g => g.PathId == transform.ParentPathId)?.Name ?? $"[{transform.ParentPathId}]" : "ROOT";
                Console.WriteLine($"  {bone.Name:30} - Parent: {parent:20} - Scale: ({transform.LocalScale.X:F2}, {transform.LocalScale.Y:F2}, {transform.LocalScale.Z:F2}) {hasChildren}");
            }
        }

        // Find avatar root
        var avatarRoot = baseContext.SemanticGameObjects.FirstOrDefault(go => go.Name.Contains("PlayerAvatar", StringComparison.OrdinalIgnoreCase))
            ?? baseContext.SemanticGameObjects.FirstOrDefault(go => go.Name.Contains("Avatar", StringComparison.OrdinalIgnoreCase));
        
        if (avatarRoot != null)
        {
            Console.WriteLine($"\nAvatar Root: {avatarRoot.Name}");
            var avatarTransform = baseContext.SemanticTransforms.FirstOrDefault(t => t.GameObjectPathId == avatarRoot.PathId);
            if (avatarTransform != null)
            {
                Console.WriteLine($"  Position: ({avatarTransform.LocalPosition.X:F2}, {avatarTransform.LocalPosition.Y:F2}, {avatarTransform.LocalPosition.Z:F2})");
                Console.WriteLine($"  Scale: ({avatarTransform.LocalScale.X:F2}, {avatarTransform.LocalScale.Y:F2}, {avatarTransform.LocalScale.Z:F2})");
                Console.WriteLine($"  Children: {avatarTransform.ChildrenPathIds.Count}");
            }
        }

        // Load decorations
        Console.WriteLine("\n=== LOADING DECORATIONS (.hhh files) ===");
        var hhhFiles = Directory.GetFiles(decorationsDir, "*.hhh").OrderBy(f => new FileInfo(f).Length).ToList();
        Console.WriteLine($"Found {hhhFiles.Count} decoration files");

        foreach (var hhhFile in hhhFiles.Take(3))  // Analyze first 3
        {
            Console.WriteLine($"\n--- {Path.GetFileName(hhhFile)} ({new FileInfo(hhhFile).Length:N0} bytes) ---");
            
            var decorationBytes = File.ReadAllBytes(hhhFile);
            var decorationParser = new HhhParser();
            var decorationContext = new BaseAssetsContext();
            var glb = decorationParser.ConvertToGlb(decorationBytes, decorationContext);

            Console.WriteLine($"Decoration: {decorationContext.SemanticGameObjects.Count} GameObjects, {decorationContext.SemanticTransforms.Count} Transforms, {decorationContext.SemanticMeshes.Count} Meshes");

            // Show root transforms
            var rootTransforms = decorationContext.SemanticTransforms
                .Where(t => !t.ParentPathId.HasValue)
                .ToList();

            Console.WriteLine($"Root transforms: {rootTransforms.Count}");
            foreach (var rootTxf in rootTransforms)
            {
                var gameObj = decorationContext.SemanticGameObjects.FirstOrDefault(g => g.PathId == rootTxf.GameObjectPathId);
                Console.WriteLine($"  [{rootTxf.PathId}] {gameObj?.Name ?? "unknown"}");
                Console.WriteLine($"    Position: ({rootTxf.LocalPosition.X:F2}, {rootTxf.LocalPosition.Y:F2}, {rootTxf.LocalPosition.Z:F2})");
                Console.WriteLine($"    Scale: ({rootTxf.LocalScale.X:F2}, {rootTxf.LocalScale.Y:F2}, {rootTxf.LocalScale.Z:F2})");
                Console.WriteLine($"    Children: {rootTxf.ChildrenPathIds.Count}");

                // Show children
                foreach (var childId in rootTxf.ChildrenPathIds.Take(5))
                {
                    var childTxf = decorationContext.SemanticTransforms.FirstOrDefault(t => t.PathId == childId);
                    if (childTxf != null)
                    {
                        var childGo = decorationContext.SemanticGameObjects.FirstOrDefault(g => g.PathId == childTxf.GameObjectPathId);
                        Console.WriteLine($"      -> {childGo?.Name ?? $"[{childId}]"} Scale: ({childTxf.LocalScale.X:F2}, {childTxf.LocalScale.Y:F2}, {childTxf.LocalScale.Z:F2})");
                    }
                }
            }

            // Check for extreme scales
            var allScales = decorationContext.SemanticTransforms.Select(t => Math.Max(Math.Max(t.LocalScale.X, t.LocalScale.Y), t.LocalScale.Z)).ToList();
            if (allScales.Any(s => s > 100))
            {
                Console.WriteLine($"  ⚠️  WARNING: Found extreme scales (max: {allScales.Max():F2})");
                var extremes = decorationContext.SemanticTransforms
                    .Where(t => Math.Max(Math.Max(t.LocalScale.X, t.LocalScale.Y), t.LocalScale.Z) > 100)
                    .ToList();
                foreach (var extreme in extremes)
                {
                    var go = decorationContext.SemanticGameObjects.FirstOrDefault(g => g.PathId == extreme.GameObjectPathId);
                    Console.WriteLine($"     -> {go?.Name}: ({extreme.LocalScale.X:F2}, {extreme.LocalScale.Y:F2}, {extreme.LocalScale.Z:F2})");
                }
            }
        }

        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine("Issues to investigate:");
        Console.WriteLine("1. Why are cosmetics appearing at center (0,0,0) instead of attached to bones?");
        Console.WriteLine("2. Why are cosmetics too large?");
        Console.WriteLine("3. Are decoration root transforms in global space or local to their intended bones?");
    }
}
