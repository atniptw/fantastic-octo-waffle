#!/bin/bash
set -e

cd /workspaces/fantastic-octo-waffle

echo "=== Testing Scale & Anchoring Issues ==="
echo ""

# Create a temp directory for test output
TEMP_DIR="/tmp/scale-test-$$"
mkdir -p "$TEMP_DIR"

# Create a test program that will:
# 1. Load the unitypackage
# 2. Load all .hhh files
# 3. Show the hierarchy

cat > "$TEMP_DIR/test-scale.cs" << 'CSHARP_EOF'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityAssetParser;

// Load fixtures
var packagePath = "/workspaces/fantastic-octo-waffle/tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityPackage/MoreHead-Asset-Pack_v1.3.unitypackage";
var decorationsDir = "/workspaces/fantastic-octo-waffle/tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityAssets";

Console.WriteLine("=== Loading Unity Package ===");
var packageBytes = File.ReadAllBytes(packagePath);
var packageParser = new UnityPackageParser();
var baseContext = new BaseAssetsContext();
packageParser.Parse(packageBytes, baseContext, packagePath);

Console.WriteLine($"Package parsed: {baseContext.SemanticGameObjects.Count} GameObjects, {baseContext.SemanticTransforms.Count} Transforms, {baseContext.SemanticMeshes.Count} Meshes");
Console.WriteLine("");

// Load all .hhh files
var hhhFiles = Directory.GetFiles(decorationsDir, "*.hhh");
Console.WriteLine($"Found {hhhFiles.Length} .hhh files:");

foreach (var hhhFile in hhhFiles.OrderBy(f => new FileInfo(f).Length))
{
    Console.WriteLine($"  - {Path.GetFileName(hhhFile)} ({new FileInfo(hhhFile).Length:N0} bytes)");
}
Console.WriteLine("");

// Parse first decoration and show hierarchy
if (hhhFiles.Length > 0)
{
    Console.WriteLine($"=== Loading decoration: {Path.GetFileName(hhhFiles[0])} ===");
    var decorationBytes = File.ReadAllBytes(hhhFiles[0]);
    var decorationParser = new HhhParser();
    var decorationContext = new BaseAssetsContext();
    var glb = decorationParser.ConvertToGlb(decorationBytes, decorationContext);
    
    Console.WriteLine($"Decoration: {decorationContext.SemanticGameObjects.Count} GameObjects, {decorationContext.SemanticTransforms.Count} Transforms");
    
    // Show transforms and scales
    foreach (var transform in decorationContext.SemanticTransforms.Take(20))
    {
        var gameObj = decorationContext.SemanticGameObjects.FirstOrDefault(g => g.PathId == transform.GameObjectPathId);
        Console.WriteLine($"  [{transform.PathId}] {gameObj?.Name} - Scale: ({transform.LocalScale.X:F2}, {transform.LocalScale.Y:F2}, {transform.LocalScale.Z:F2})");
    }
}

Console.WriteLine("");
Console.WriteLine("=== Analysis ===");

// Check if avatar skeleton has expected bones
var boneNames = new[] { "armature", "skeleton", "rig", "root", "hips", "spine", "chest", "neck", "head" };
var hasSkeleton = baseContext.SemanticGameObjects
    .Where(go => boneNames.Any(bone => go.Name.Contains(bone, StringComparison.OrdinalIgnoreCase)))
    .ToList();

Console.WriteLine($"Found {hasSkeleton.Count} skeleton-like GameObjects in package:");
foreach (var bone in hasSkeleton)
{
    var transform = baseContext.SemanticTransforms.FirstOrDefault(t => t.GameObjectPathId == bone.PathId);
    if (transform != null)
    {
        Console.WriteLine($"  - {bone.Name} @ scale ({transform.LocalScale.X:F2}, {transform.LocalScale.Y:F2}, {transform.LocalScale.Z:F2})");
    }
}
CSHARP_EOF

# Run the test using dotnet script if available, or compile and run
dotnet script "$TEMP_DIR/test-scale.cs" 2>&1 || {
    echo "Script execution failed, trying with a compiled test..."
}

rm -rf "$TEMP_DIR"
