using System;
using System.IO;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;

var path = args[0];
var data = File.ReadAllBytes(path);
using var ms = new MemoryStream(data, writable: false);
var bundle = BundleFile.Parse(ms);
var node0Data = bundle.ExtractNode(bundle.Nodes[0]);
var sf = SerializedFile.Parse(node0Data.Span);

Console.WriteLine($"TypeTree: HasTypeTree={sf.TypeTree.HasTypeTree}, TypeCount={sf.TypeTree.Types.Count}");
for (int i = 0; i < sf.TypeTree.Types.Count; i++)
{
    var type = sf.TypeTree.Types[i];
    Console.WriteLine($"  Type[{i}]: ClassId={type.ClassId}, Nodes={type.Nodes?.Count ?? 0}, Stripped={type.IsStrippedType}");
}

Console.WriteLine($"\nObjects:");
foreach (var obj in sf.Objects)
{
    var type = sf.TypeTree.GetType(obj.TypeId);
    Console.WriteLine($"  PathId={obj.PathId}, TypeId={obj.TypeId}, ClassId={obj.ClassId}, TypeNodes={type?.Nodes?.Count ?? 0}");
}
