using System;
using System.IO;
using UnityAssetParser.Services;

class Program
{
    static void Main()
    {
        var bundlePath = "Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";

        if (!File.Exists(bundlePath))
        {
            Console.WriteLine($"ERROR: File not found: {bundlePath}");
            Environment.Exit(1);
        }

        Console.WriteLine($"Loading bundle: {bundlePath}");
        var bundleData = File.ReadAllBytes(bundlePath);
        Console.WriteLine($"Bundle size: {bundleData.Length} bytes");

        var service = new MeshExtractionService();

        try
        {
            var meshes = service.ExtractMeshes(bundleData);
            Console.WriteLine($"\n✓ Extracted {meshes.Count} meshes");

            foreach (var mesh in meshes)
            {
                Console.WriteLine($"\nMesh: {mesh.Name}");
                Console.WriteLine($"  VertexCount: {mesh.VertexCount}");
                Console.WriteLine($"  Positions: {mesh.Positions?.Length ?? 0} floats");
                Console.WriteLine($"  Indices: {mesh.Indices?.Length ?? 0} uint");
                Console.WriteLine($"  Normals: {mesh.Normals?.Length ?? 0} floats");
                Console.WriteLine($"  UVs: {mesh.UVs?.Length ?? 0} floats");
                Console.WriteLine($"  Groups: {mesh.Groups.Count}");
                foreach (var group in mesh.Groups)
                {
                    Console.WriteLine($"    - Start={group.Start}, Count={group.Count}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}
