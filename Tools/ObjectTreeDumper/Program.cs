using System.Text.Json;
using UnityAssetParser.Bundle;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;

if (args.Length == 0)
{
    Console.WriteLine("Usage: ObjectTreeDumper <input.hhh> [-o output.json]");
    Console.WriteLine("  Dumps full object trees from Unity bundle files for comparison with UnityPy");
    return 1;
}

var inputPath = args[0];
string? outputPath = null;
for (int i = 1; i < args.Length - 1; i++)
{
    if (args[i] is "-o" or "--output")
    {
        outputPath = args[i + 1];
        break;
    }
}

outputPath ??= $"{Path.GetFileNameWithoutExtension(inputPath)}_csharp_tree.json";

static bool IsUnityFs(ReadOnlySpan<byte> header)
{
    return header.Length >= 7 &&
           (header.SequenceEqual("UnityFS"u8) || header.SequenceEqual("UnityWeb"u8));
}

static SerializedFile LoadSerializedFile(string path)
{
    var data = File.ReadAllBytes(path);
    if (IsUnityFs(data.AsSpan(0, Math.Min(7, data.Length))))
    {
        using var ms = new MemoryStream(data, writable: false);
        var bundle = BundleFile.Parse(ms);
        if (bundle.Nodes.Count == 0)
        {
            throw new InvalidDataException("UnityFS bundle has no nodes.");
        }
        var node0 = bundle.Nodes[0];
        var node0Data = bundle.ExtractNode(node0);
        return SerializedFile.Parse(node0Data.Span);
    }

    return SerializedFile.Parse(data);
}

static object? Normalize(object? value)
{
    if (value == null)
        return null;

    switch (value)
    {
        case string s:
            return s;
        case bool b:
            return b;
        case int i:
            return i;
        case long l:
            return l;
        case uint ui:
            return ui;
        case ulong ul:
            return ul;
        case short sh:
            return sh;
        case ushort ush:
            return ush;
        case byte by:
            return by;
        case sbyte sby:
            return sby;
        case float f:
            return f;
        case double d:
            return d;
        case byte[] bytes:
            return new Dictionary<string, object> { ["__bytes__"] = Convert.ToBase64String(bytes) };
        case ReadOnlyMemory<byte> rom:
            return new Dictionary<string, object> { ["__bytes__"] = Convert.ToBase64String(rom.ToArray()) };
        case Memory<byte> mem:
            return new Dictionary<string, object> { ["__bytes__"] = Convert.ToBase64String(mem.ToArray()) };
        case Dictionary<string, object?> dict:
            return dict.ToDictionary(kv => kv.Key, kv => Normalize(kv.Value));
        case List<object?> list:
            return list.Select(Normalize).ToList();
        default:
            return value.ToString();
    }
}

Console.WriteLine($"Loading {inputPath}...");
var serializedFile = LoadSerializedFile(inputPath);
Console.WriteLine($"Loaded SerializedFile with {serializedFile.Objects.Count} objects");
Console.WriteLine($"TypeTree: HasTypeTree={serializedFile.TypeTree.HasTypeTree}, TypeCount={serializedFile.TypeTree.Types.Count}");
for (int i = 0; i < serializedFile.TypeTree.Types.Count; i++)
{
    var t = serializedFile.TypeTree.Types[i];
    Console.WriteLine($"  Type[{i}]: ClassId={t.ClassId}, Nodes={t.Nodes?.Count ?? 0}");
}

var objects = new List<Dictionary<string, object?>>();

foreach (var obj in serializedFile.Objects)
{
    var type = serializedFile.TypeTree.GetType(obj.TypeId);
    var treeRoot = type?.TreeRoot;
    var nodes = type?.Nodes;

    Console.WriteLine($"  Object PathId={obj.PathId} ClassId={obj.ClassId} TypeId={obj.TypeId} Size={obj.ByteSize}");

    Dictionary<string, object?> tree;
    if ((treeRoot == null || treeRoot.Children.Count == 0) && (nodes == null || nodes.Count == 0))
    {
        tree = new Dictionary<string, object?> { ["__error__"] = "TypeTree nodes missing" };
        Console.WriteLine($"    WARNING: TypeTree nodes missing");
    }
    else
    {
        Console.WriteLine($"    TypeTree nodes: {nodes?.Count ?? 0}");
        var objectData = serializedFile.ReadObjectData(obj);
        using var ms = new MemoryStream(objectData.ToArray(), writable: false);
        using var reader = new EndianBinaryReader(ms, serializedFile.Header.Endianness == 1);
        
        TypeTreeReader typeTreeReader;
        if (treeRoot != null && treeRoot.Children.Count > 0)
        {
            // Use new tree-based reader
            typeTreeReader = new TypeTreeReader(reader, treeRoot);
        }
        else
        {
            // Fallback to flat list reader for backwards compatibility
            typeTreeReader = TypeTreeReader.CreateFromFlatList(reader, nodes!);
        }
        
        try
        {
            tree = typeTreeReader.ReadObject();
            Console.WriteLine($"    Read {tree.Count} fields");
        }
        catch (Exception ex)
        {
            tree = new Dictionary<string, object?> { ["__error__"] = ex.Message };
            Console.WriteLine($"    ERROR: {ex.Message}");
        }
    }

    objects.Add(new Dictionary<string, object?>
    {
        ["pathId"] = obj.PathId,
        ["classId"] = obj.ClassId,
        ["typeId"] = obj.TypeId,
        ["byteSize"] = obj.ByteSize,
        ["tree"] = Normalize(tree)
    });
}

var output = new Dictionary<string, object?>
{
    ["source"] = inputPath,
    ["unityVersion"] = serializedFile.Header.UnityVersionString,
    ["objectCount"] = objects.Count,
    ["objects"] = objects
};

var options = new JsonSerializerOptions
{
    WriteIndented = true
};

File.WriteAllText(outputPath, JsonSerializer.Serialize(output, options));
Console.WriteLine($"Wrote C# object tree: {outputPath}");

return 0;
