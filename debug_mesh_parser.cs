#!/usr/bin/env dotnet-script
// Direct test of MeshParser against real bundle data
// Run with: dotnet script debug_mesh_parser.cs

using System;
using System.IO;
using System.Linq;
using UnityAssetParser.Bundle;
using UnityAssetParser.Services;

var bundlePath = "Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";

if (!File.Exists(bundlePath))
{
    Console.WriteLine($"Bundle not found: {bundlePath}");
    return;
}

try
{
    var bundleData = File.ReadAllBytes(bundlePath);
    Console.WriteLine($"Loaded bundle: {bundleData.Length} bytes");

    // Parse bundle
    var bundleFile = BundleFile.Parse(bundleData);
    Console.WriteLine($"Parsed bundle with {bundleFile.Nodes.Count} nodes");

    // Extract SerializedFile from node 0
    var node0Data = bundleFile.Nodes[0].Data;
    Console.WriteLine($"Node 0 data: {node0Data.Length} bytes");

    // Parse SerializedFile
    var sf = SerializedFile.Parse(node0Data);
    Console.WriteLine($"Parsed SerializedFile with {sf.Objects.Count} objects");

    // Find Mesh object
    var meshObject = sf.Objects.FirstOrDefault(o => o.ClassID == 43);
    if (meshObject == null)
    {
        Console.WriteLine("No Mesh object found!");
        return;
    }

    Console.WriteLine($"Found Mesh object: {meshObject.PathId}");
    Console.WriteLine($"Mesh object data: {meshObject.Data.Length} bytes");

    // Try to parse Mesh
    Console.WriteLine("\n=== Attempting MeshParser.Parse ===");
    try
    {
        var mesh = MeshParser.Parse(
            meshObject.Data,
            (sf.UnityVersion.Major, sf.UnityVersion.Minor, sf.UnityVersion.Patch, 0),
            sf.IsBigEndian);

        if (mesh == null)
        {
            Console.WriteLine("MeshParser returned null");
        }
        else
        {
            Console.WriteLine($"Success! Parsed Mesh:");
            Console.WriteLine($"  Name: {mesh.Name}");
            Console.WriteLine($"  SubMeshes: {mesh.SubMeshes?.Count ?? 0}");
            Console.WriteLine($"  IndexBuffer: {mesh.IndexBuffer?.Count ?? 0}");
        }
    }
    catch (Exception meshEx)
    {
        Console.WriteLine($"MeshParser threw exception: {meshEx.GetType().Name}");
        Console.WriteLine($"Message: {meshEx.Message}");
        Console.WriteLine($"Stack: {meshEx.StackTrace}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
