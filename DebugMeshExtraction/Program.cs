using System;
using System.IO;
using UnityAssetParser.Services;
using UnityAssetParser.Bundle;

var bundlePath = "/workspaces/fantastic-octo-waffle/Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";
var bundleData = File.ReadAllBytes(bundlePath);
Console.WriteLine($"Loaded bundle: {bundlePath}");
Console.WriteLine($"Size: {bundleData.Length} bytes");
Console.WriteLine();

// Try to extract meshes
try
{
    Console.WriteLine("Attempting to extract meshes...");
    var service = new MeshExtractionService();
    var meshes = service.ExtractMeshes(bundleData);

    Console.WriteLine($"✓ SUCCESS: Extracted {meshes.Count} meshes");
    Console.WriteLine();

    foreach (var mesh in meshes)
    {
        Console.WriteLine($"Mesh: {mesh.Name}");
        Console.WriteLine($"  Vertices: {mesh.VertexCount}");
        Console.WriteLine($"  Positions: {mesh.Positions?.Length ?? 0} floats");
        Console.WriteLine($"  Normals: {mesh.Normals?.Length ?? 0} floats");
        Console.WriteLine($"  UVs: {mesh.UVs?.Length ?? 0} floats");
        Console.WriteLine($"  Indices: {mesh.Indices?.Length ?? 0}");
        Console.WriteLine($"  16-bit indices: {mesh.Use16BitIndices}");
        Console.WriteLine($"  Groups: {mesh.Groups.Count}");
        foreach (var grp in mesh.Groups)
        {
            Console.WriteLine($"    - MaterialIndex={grp.MaterialIndex}, Start={grp.Start}, Count={grp.Count}");
        }
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ ERROR: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
    Console.WriteLine();
    Console.WriteLine("Stack trace:");
    Console.WriteLine(ex.StackTrace);
}

