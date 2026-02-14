using System;
using System.IO;
using UnityAssetParser.Bundle;
using UnityAssetParser.Helpers;

// Debug script - run with: dotnet run --project Tests/UnityAssetParser.Tests/
namespace Debug;

class Program
{
    static void Main()
    {
        var bundlePath = "Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";

        using var fs = File.OpenRead(bundlePath);
        using var br = new BinaryReader(fs);

        // Read header
        string sig = new string(br.ReadChars(4));
        Console.WriteLine($"Signature: {sig}");

        // Try using BundleFile parser directly with some diagnostics
        fs.Seek(0, SeekOrigin.Begin);
        var reader = new EndianBinaryReader(fs, isBigEndian: true);

        try
        {
            // Read the bundle
            var bundleFile = BundleFile.Parse(fs);
            Console.WriteLine($"Successfully parsed bundle");
            Console.WriteLine($"Nodes: {bundleFile.Nodes.Count}");
            foreach (var node in bundleFile.Nodes)
            {
                Console.WriteLine($"  - {node.Path}: offset={node.Offset}, size={node.Size}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
