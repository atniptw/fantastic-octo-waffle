using System;
using System.IO;
using System.Linq;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;

var path = args.Length > 0 ? args[0] : "../../Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";
var data = File.ReadAllBytes(path);
using var ms = new MemoryStream(data, writable: false);
var bundle = BundleFile.Parse(ms);
var node0 = bundle.Nodes[0];
var node0Data = bundle.ExtractNode(node0);
var sf = SerializedFile.Parse(node0Data.Span);

// Find mesh type (ClassId 43)
var meshType = sf.TypeTree.Types.FirstOrDefault(t => t.ClassId == 43);
if (meshType == null || meshType.Nodes == null)
{
    Console.WriteLine("No mesh type found");
    return;
}

Console.WriteLine($"Mesh TypeTree has {meshType.Nodes.Count} nodes:");
Console.WriteLine($"{"Index",-6} {"Level",-6} {"Type",-30} {"Name",-30} {"ByteSize",-10} {"TypeFlags",-10}");
Console.WriteLine(new string('-', 100));

for (int i = 0; i < Math.Min(50, meshType.Nodes.Count); i++)
{
    var node = meshType.Nodes[i];
    Console.WriteLine($"{i,-6} {node.Level,-6} {node.Type,-30} {node.Name,-30} {node.ByteSize,-10} 0x{node.TypeFlags:X2}");
}

// Find m_IndexBuffer node
var indexBufferIdx = -1;
for (int i = 0; i < meshType.Nodes.Count; i++)
{
    if (meshType.Nodes[i].Name == "m_IndexBuffer")
    {
        indexBufferIdx = i;
        break;
    }
}

if (indexBufferIdx >= 0)
{
    Console.WriteLine($"\nm_IndexBuffer structure starting at index {indexBufferIdx}:");
    for (int i = indexBufferIdx; i < Math.Min(indexBufferIdx + 5, meshType.Nodes.Count); i++)
    {
        var node = meshType.Nodes[i];
        Console.WriteLine($"  [{i}] Level={node.Level} Type={node.Type,-20} Name={node.Name,-20} TypeFlags=0x{node.TypeFlags:X2}");
    }
}
