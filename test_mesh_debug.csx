#!/usr/bin/env dotnet-script
#r "nuget: System.IO"
#r "/workspaces/fantastic-octo-waffle/src/UnityAssetParser/bin/Debug/net10.0/UnityAssetParser.dll"

using UnityAssetParser.Bundle;
using UnityAssetParser.Services;
using System.IO;

var bundlePath = "/workspaces/fantastic-octo-waffle/Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";
var bundleData = File.ReadAllBytes(bundlePath);
Console.WriteLine($"Loaded bundle: {bundlePath}, Size: {bundleData.Length} bytes");

// Try to extract meshes
try
{
    var service = new MeshExtractionService();
    var meshes = service.ExtractMeshes(bundleData);
    Console.WriteLine($"SUCCESS: Extracted {meshes.Count} meshes");
    foreach (var mesh in meshes)
    {
        Console.WriteLine($"  - Name: {mesh.Name}, Vertices: {mesh.VertexCount}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
