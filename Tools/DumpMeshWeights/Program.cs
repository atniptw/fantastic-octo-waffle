using System.Text.Json;
using UnityAssetParser.Bundle;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;
using UnityAssetParser.Classes;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project Tools/DumpMeshWeights -- <bundle.hhh>");
    return;
}

var inputPath = args[0];
if (!File.Exists(inputPath))
{
    Console.WriteLine($"File not found: {inputPath}");
    return;
}

var data = File.ReadAllBytes(inputPath);
SerializedFile serialized;
byte[]? externalResourceData = null;

if (IsUnityFs(data))
{
    using var ms = new MemoryStream(data, writable: false);
    var bundle = BundleFile.Parse(ms);
    if (bundle.Nodes.Count == 0)
    {
        Console.WriteLine("Bundle has no nodes.");
        return;
    }

    var node = FindSerializedNode(bundle);
    var nodeData = bundle.ExtractNode(node);
    serialized = SerializedFile.Parse(nodeData.Span);

    var resourceNode = bundle.Nodes.FirstOrDefault(n =>
        n.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
        n.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase));
    if (resourceNode != null)
    {
        externalResourceData = bundle.ExtractNode(resourceNode).ToArray();
    }
}
else
{
    serialized = SerializedFile.Parse(data);
}

var meshObj = serialized.Objects.FirstOrDefault(o => o.ClassId == 43);
if (meshObj == null)
{
    Console.WriteLine("No Mesh object found.");
    return;
}

Console.WriteLine($"Mesh objects: {serialized.Objects.Count(o => o.ClassId == 43)}");
foreach (var obj in serialized.Objects.Where(o => o.ClassId == 43))
{
    var m = TryParseMesh(serialized, obj);
    if (m != null)
    {
        Console.WriteLine($"  Mesh PathId={obj.PathId} Name='{m.Name}' SubMeshes={m.SubMeshes?.Length ?? 0} Vertices={m.Vertices?.Length ?? 0}");
    }
    else
    {
        Console.WriteLine($"  Mesh PathId={obj.PathId} (parse failed)");
    }
}

DumpRendererRefs(serialized, 23, "MeshRenderer");
DumpRendererRefs(serialized, 33, "MeshFilter");
DumpRendererRefs(serialized, 137, "SkinnedMeshRenderer");

DumpExternals(serialized);

Console.WriteLine($"Material objects: {serialized.Objects.Count(o => o.ClassId == 21)}");
foreach (var matObj in serialized.Objects.Where(o => o.ClassId == 21))
{
    DumpMaterialDetails(serialized, matObj);
}

Console.WriteLine($"Texture2D objects: {serialized.Objects.Count(o => o.ClassId == 28)}");
foreach (var texObj in serialized.Objects.Where(o => o.ClassId == 28))
{
    DumpTextureDetails(serialized, texObj);
}

var type = serialized.TypeTree.GetType(meshObj.TypeId);
var nodes = type?.Nodes;
if (nodes == null || nodes.Count == 0)
{
    Console.WriteLine("Mesh TypeTree nodes missing.");
    return;
}

var objectData = serialized.ReadObjectData(meshObj);
using var objStream = new MemoryStream(objectData.ToArray(), writable: false);
using var reader = new EndianBinaryReader(objStream, serialized.Header.Endianness == 1);
var typeTreeReader = TypeTreeReader.CreateFromFlatList(reader, nodes);
var tree = typeTreeReader.ReadObject();

// Parse Mesh object for extra diagnostics
var version = ParseUnityVersion(serialized.Header.UnityVersionString);
var mesh = MeshParser.ParseWithTypeTree(objectData.Span, nodes, version, serialized.Header.Endianness == 1);
if (mesh != null)
{
    DumpMeshDiagnostics(mesh, externalResourceData);
}

if (!tree.TryGetValue("m_VariableBoneCountWeights", out var vbwObj))
{
    Console.WriteLine("m_VariableBoneCountWeights not present.");
    return;
}

Console.WriteLine("m_VariableBoneCountWeights:");
DumpValue(vbwObj, 0);

static bool IsUnityFs(ReadOnlySpan<byte> header)
{
    return header.Length >= 7 &&
           (header.Slice(0, 7).SequenceEqual("UnityFS"u8) ||
            header.Slice(0, 7).SequenceEqual("UnityWeb"u8));
}

static NodeInfo FindSerializedNode(BundleFile bundle)
{
    foreach (var node in bundle.Nodes)
    {
        if (node.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
            node.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }
        return node;
    }
    return bundle.Nodes[0];
}

static void DumpValue(object? value, int indent)
{
    var pad = new string(' ', indent * 2);
    if (value == null)
    {
        Console.WriteLine($"{pad}null");
        return;
    }

    switch (value)
    {
        case Dictionary<string, object?> dict:
            Console.WriteLine($"{pad}Object (keys: {string.Join(", ", dict.Keys)})");
            foreach (var (k, v) in dict)
            {
                Console.Write($"{pad}- {k}: ");
                DumpValueInline(v, indent + 1);
            }
            break;
        case List<object?> list:
            Console.WriteLine($"{pad}List (count: {list.Count})");
            if (list.Count > 0)
            {
                Console.Write($"{pad}- [0]: ");
                DumpValueInline(list[0], indent + 1);
            }
            break;
        case byte[] bytes:
            Console.WriteLine($"{pad}byte[] (len: {bytes.Length})");
            break;
        case ReadOnlyMemory<byte> rom:
            Console.WriteLine($"{pad}ReadOnlyMemory<byte> (len: {rom.Length})");
            break;
        case Memory<byte> mem:
            Console.WriteLine($"{pad}Memory<byte> (len: {mem.Length})");
            break;
        default:
            Console.WriteLine($"{pad}{value} ({value.GetType().Name})");
            break;
    }
}

static void DumpValueInline(object? value, int indent)
{
    if (value == null)
    {
        Console.WriteLine("null");
        return;
    }

    if (value is Dictionary<string, object?> or List<object?>)
    {
        Console.WriteLine();
        DumpValue(value, indent);
        return;
    }

    if (value is byte[] bytes)
    {
        Console.WriteLine($"byte[] (len: {bytes.Length})");
        return;
    }

    if (value is ReadOnlyMemory<byte> rom)
    {
        Console.WriteLine($"ReadOnlyMemory<byte> (len: {rom.Length})");
        return;
    }

    if (value is Memory<byte> mem)
    {
        Console.WriteLine($"Memory<byte> (len: {mem.Length})");
        return;
    }

    Console.WriteLine($"{value} ({value.GetType().Name})");
}

static (int, int, int, int) ParseUnityVersion(string? versionString)
{
    if (string.IsNullOrEmpty(versionString))
    {
        return (2020, 0, 0, 0);
    }

    var parts = versionString.Split('.');
    if (parts.Length < 2)
    {
        return (2020, 0, 0, 0);
    }

    int.TryParse(parts[0], out int major);
    int.TryParse(parts[1], out int minor);
    int patch = 0;
    if (parts.Length >= 3)
    {
        var patchStr = new string(parts[2].TakeWhile(char.IsDigit).ToArray());
        int.TryParse(patchStr, out patch);
    }

    return (major, minor, patch, 0);
}

static void DumpMeshDiagnostics(Mesh mesh, byte[]? externalResourceData)
{
    Console.WriteLine($"Mesh name: {mesh.Name}");
    if (mesh.StreamData != null)
    {
        Console.WriteLine($"StreamData: path='{mesh.StreamData.Path ?? ""}' offset={mesh.StreamData.Offset} size={mesh.StreamData.Size}");
    }
    else
    {
        Console.WriteLine("StreamData: none");
    }

    var indexLen = mesh.IndexBuffer?.Length ?? 0;
    Console.WriteLine($"IndexBuffer length: {indexLen}");

    if (mesh.VertexData?.Channels != null)
    {
        Console.WriteLine($"VertexData channels: {mesh.VertexData.Channels.Length}");
        for (int i = 0; i < mesh.VertexData.Channels.Length; i++)
        {
            var ch = mesh.VertexData.Channels[i];
            if (ch.Dimension == 0) continue;
            Console.WriteLine($"  Channel[{i}] dim={ch.Dimension} fmt={ch.Format} stream={ch.Stream} offset={ch.Offset}");
        }
    }

    if (mesh.CompressedMesh != null)
    {
        var cm = mesh.CompressedMesh;
        Console.WriteLine($"CompressedMesh: weights={cm.Weights.NumItems} boneIndices={cm.BoneIndices.NumItems} bindPoses={cm.BindPoses?.NumItems ?? 0}");
    }

    var positions = VertexDataExtractor.ExtractVertexPositions(mesh, externalResourceData);
    Console.WriteLine($"Extracted positions: {positions.Length / 3} vertices");
    if (positions.Length >= 3)
    {
        Console.WriteLine($"First vertex: {positions[0]:0.###}, {positions[1]:0.###}, {positions[2]:0.###}");
    }
}

static Mesh? TryParseMesh(SerializedFile serialized, ObjectInfo obj)
{
    var type = serialized.TypeTree.GetType(obj.TypeId);
    var nodes = type?.Nodes;
    if (nodes == null || nodes.Count == 0) return null;
    var objectData = serialized.ReadObjectData(obj);
    var version = ParseUnityVersion(serialized.Header.UnityVersionString);
    return MeshParser.ParseWithTypeTree(objectData.Span, nodes, version, serialized.Header.Endianness == 1);
}

static void DumpRendererRefs(SerializedFile serialized, int classId, string label)
{
    var objs = serialized.Objects.Where(o => o.ClassId == classId).ToList();
    if (objs.Count == 0) return;
    Console.WriteLine($"{label} objects: {objs.Count}");
    foreach (var obj in objs)
    {
        var tree = ReadObjectTree(serialized, obj);
        if (tree == null)
        {
            Console.WriteLine($"  {label} PathId={obj.PathId} (no tree)");
            continue;
        }

        var meshPtr = FindPtr(tree, "m_Mesh");
        if (meshPtr != null)
        {
            Console.WriteLine($"  {label} PathId={obj.PathId} m_Mesh PathId={meshPtr?.pathId} FileId={meshPtr?.fileId}");
        }

        var materials = FindMaterialPtrs(tree);
        if (materials.Count > 0)
        {
            Console.WriteLine($"  {label} PathId={obj.PathId} materials: {string.Join(", ", materials.Select(m => $"{m.fileId}:{m.pathId}"))}");
        }

        if (label == "SkinnedMeshRenderer")
        {
            DumpSkinnedMeshRefs(tree, obj.PathId);
        }
    }
}

static Dictionary<string, object?>? ReadObjectTree(SerializedFile serialized, ObjectInfo obj)
{
    var type = serialized.TypeTree.GetType(obj.TypeId);
    var nodes = type?.Nodes;
    if (nodes == null || nodes.Count == 0) return null;
    var objectData = serialized.ReadObjectData(obj);
    using var ms = new MemoryStream(objectData.ToArray(), writable: false);
    using var reader = new EndianBinaryReader(ms, serialized.Header.Endianness == 1);
    var typeTreeReader = TypeTreeReader.CreateFromFlatList(reader, nodes);
    return typeTreeReader.ReadObject();
}

static (int fileId, long pathId)? FindPtr(Dictionary<string, object?> tree, string key)
{
    if (!tree.TryGetValue(key, out var value) || value is not Dictionary<string, object?> ptr)
    {
        return null;
    }

    int fileId = ptr.TryGetValue("m_FileID", out var f) ? Convert.ToInt32(f) : 0;
    long pathId = ptr.TryGetValue("m_PathID", out var p) ? Convert.ToInt64(p) : 0;
    return (fileId, pathId);
}

static List<(int fileId, long pathId)> FindMaterialPtrs(Dictionary<string, object?> tree)
{
    var results = new List<(int, long)>();
    if (!tree.TryGetValue("m_Materials", out var matsObj)) return results;
    if (matsObj is not List<object?> matsList) return results;
    foreach (var item in matsList)
    {
        if (item is not Dictionary<string, object?> ptr) continue;
        int fileId = ptr.TryGetValue("m_FileID", out var f) ? Convert.ToInt32(f) : 0;
        long pathId = ptr.TryGetValue("m_PathID", out var p) ? Convert.ToInt64(p) : 0;
        results.Add((fileId, pathId));
    }
    return results;
}

static void DumpSkinnedMeshRefs(Dictionary<string, object?> tree, long pathId)
{
    var root = FindPtr(tree, "m_RootBone");
    if (root != null)
    {
        Console.WriteLine($"  SkinnedMeshRenderer PathId={pathId} rootBone: {root?.fileId}:{root?.pathId}");
    }

    if (tree.TryGetValue("m_Bones", out var bonesObj) && bonesObj is List<object?> bones)
    {
        Console.WriteLine($"  SkinnedMeshRenderer PathId={pathId} bones: {bones.Count}");
        var sample = bones.Take(3).OfType<Dictionary<string, object?>>()
            .Select(b => $"{(b.TryGetValue("m_FileID", out var f) ? Convert.ToInt32(f) : 0)}:{(b.TryGetValue("m_PathID", out var p) ? Convert.ToInt64(p) : 0)}");
        if (sample.Any())
        {
            Console.WriteLine($"  SkinnedMeshRenderer PathId={pathId} bones sample: {string.Join(", ", sample)}");
        }
    }
}

static void DumpMaterialDetails(SerializedFile serialized, ObjectInfo matObj)
{
    var tree = ReadObjectTree(serialized, matObj);
    if (tree == null) return;
    var name = tree.TryGetValue("m_Name", out var n) ? n?.ToString() : "(unnamed)";
    Console.WriteLine($"  Material PathId={matObj.PathId} Name='{name}'");

    var shader = FindPtr(tree, "m_Shader");
    if (shader != null)
    {
        Console.WriteLine($"    Shader: {shader?.fileId}:{shader?.pathId}");
    }

    DumpShaderKeywords(tree);

    var texEnvs = FindTexEnvs(tree);
    if (texEnvs.Count == 0)
    {
        Console.WriteLine("    TexEnvs: none");
    }
    else
    {
        Console.WriteLine($"    TexEnvs: {texEnvs.Count}");
        foreach (var env in texEnvs)
        {
            Console.WriteLine($"      {env.name}: {env.fileId}:{env.pathId}");
        }
    }

    DumpMaterialColors(tree);
    DumpMaterialFloats(tree);
    DumpMaterialInts(tree);
}

static void DumpTextureDetails(SerializedFile serialized, ObjectInfo texObj)
{
    var tree = ReadObjectTree(serialized, texObj);
    if (tree == null) return;
    var name = tree.TryGetValue("m_Name", out var n) ? n?.ToString() : "(unnamed)";
    var width = tree.TryGetValue("m_Width", out var w) ? w?.ToString() : "?";
    var height = tree.TryGetValue("m_Height", out var h) ? h?.ToString() : "?";
    var format = tree.TryGetValue("m_TextureFormat", out var f) ? f?.ToString() : "?";
    Console.WriteLine($"  Texture2D PathId={texObj.PathId} Name='{name}' {width}x{height} format={format}");
}

static List<(string name, int fileId, long pathId)> FindTexEnvs(Dictionary<string, object?> tree)
{
    if (tree.TryGetValue("m_SavedProperties", out var saved) && saved is Dictionary<string, object?> savedDict)
    {
        if (savedDict.TryGetValue("m_TexEnvs", out var texEnvs))
        {
            return ParseTexEnvs(texEnvs);
        }
    }

    if (tree.TryGetValue("m_TexEnvs", out var rootTexEnvs))
    {
        return ParseTexEnvs(rootTexEnvs);
    }

    return new List<(string, int, long)>();
}

static List<(string name, int fileId, long pathId)> ParseTexEnvs(object? texEnvs)
{
    var results = new List<(string, int, long)>();
    if (texEnvs is not List<object?> list) return results;

    foreach (var item in list)
    {
        if (item is not Dictionary<string, object?> pair) continue;
        var name = pair.TryGetValue("first", out var first) ? first?.ToString() ?? "(null)" : "(null)";
        if (!pair.TryGetValue("second", out var secondObj) || secondObj is not Dictionary<string, object?> second)
        {
            results.Add((name, 0, 0));
            continue;
        }

        if (!second.TryGetValue("m_Texture", out var texObj) || texObj is not Dictionary<string, object?> texPtr)
        {
            results.Add((name, 0, 0));
            continue;
        }

        int fileId = texPtr.TryGetValue("m_FileID", out var f) ? Convert.ToInt32(f) : 0;
        long pathId = texPtr.TryGetValue("m_PathID", out var p) ? Convert.ToInt64(p) : 0;
        results.Add((name, fileId, pathId));
    }

    return results;
}

static void DumpExternals(SerializedFile serialized)
{
    if (serialized.Externals.Count == 0)
    {
        Console.WriteLine("Externals: none");
        return;
    }

    Console.WriteLine($"Externals: {serialized.Externals.Count}");
    for (int i = 0; i < serialized.Externals.Count; i++)
    {
        var ext = serialized.Externals[i];
        Console.WriteLine($"  [{i}] Path='{ext.PathName}' Type={ext.Type} Guid={ext.Guid}");
    }
}

static void DumpMaterialColors(Dictionary<string, object?> tree)
{
    if (!tree.TryGetValue("m_SavedProperties", out var propsObj) || propsObj is not Dictionary<string, object?> props)
    {
        return;
    }

    if (!props.TryGetValue("m_Colors", out var colorsObj) || colorsObj is not List<object?> colorsList)
    {
        Console.WriteLine("    Colors: none");
        return;
    }

    if (colorsList.Count == 0)
    {
        Console.WriteLine("    Colors: empty");
        return;
    }

    Console.WriteLine($"    Colors: {colorsList.Count}");
    foreach (var entryObj in colorsList)
    {
        if (entryObj is not Dictionary<string, object?> entry)
        {
            continue;
        }

        var key = entry.TryGetValue("first", out var first) ? first?.ToString() ?? "(null)" : "(null)";
        if (!entry.TryGetValue("second", out var secondObj) || secondObj is not Dictionary<string, object?> color)
        {
            Console.WriteLine($"      {key}: (no value)");
            continue;
        }

        var r = GetFloat(color, "r", 1f);
        var g = GetFloat(color, "g", 1f);
        var b = GetFloat(color, "b", 1f);
        var a = GetFloat(color, "a", 1f);
        Console.WriteLine($"      {key}: {r}, {g}, {b}, {a}");
    }
}

static void DumpMaterialFloats(Dictionary<string, object?> tree)
{
    if (!tree.TryGetValue("m_SavedProperties", out var propsObj) || propsObj is not Dictionary<string, object?> props)
    {
        return;
    }

    if (!props.TryGetValue("m_Floats", out var floatsObj) || floatsObj is not List<object?> floatsList)
    {
        Console.WriteLine("    Floats: none");
        return;
    }

    if (floatsList.Count == 0)
    {
        Console.WriteLine("    Floats: empty");
        return;
    }

    Console.WriteLine($"    Floats: {floatsList.Count}");
    foreach (var entryObj in floatsList)
    {
        if (entryObj is not Dictionary<string, object?> entry)
        {
            continue;
        }

        var key = entry.TryGetValue("first", out var first) ? first?.ToString() ?? "(null)" : "(null)";
        var value = entry.TryGetValue("second", out var second) ? second : null;
        Console.WriteLine($"      {key}: {FormatNumber(value)}");
    }
}

static void DumpMaterialInts(Dictionary<string, object?> tree)
{
    if (!tree.TryGetValue("m_SavedProperties", out var propsObj) || propsObj is not Dictionary<string, object?> props)
    {
        return;
    }

    if (!props.TryGetValue("m_Ints", out var intsObj) || intsObj is not List<object?> intsList)
    {
        Console.WriteLine("    Ints: none");
        return;
    }

    if (intsList.Count == 0)
    {
        Console.WriteLine("    Ints: empty");
        return;
    }

    Console.WriteLine($"    Ints: {intsList.Count}");
    foreach (var entryObj in intsList)
    {
        if (entryObj is not Dictionary<string, object?> entry)
        {
            continue;
        }

        var key = entry.TryGetValue("first", out var first) ? first?.ToString() ?? "(null)" : "(null)";
        var value = entry.TryGetValue("second", out var second) ? second : null;
        Console.WriteLine($"      {key}: {FormatNumber(value)}");
    }
}

static void DumpShaderKeywords(Dictionary<string, object?> tree)
{
    if (!tree.TryGetValue("m_ShaderKeywords", out var kwObj) || kwObj == null)
    {
        Console.WriteLine("    Keywords: none");
        return;
    }

    Console.WriteLine($"    Keywords: {kwObj}");
}

static float GetFloat(Dictionary<string, object?> dict, string key, float fallback)
{
    if (!dict.TryGetValue(key, out var value) || value == null)
    {
        return fallback;
    }

    return value switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        _ => fallback
    };
}

static string FormatNumber(object? value)
{
    return value switch
    {
        float f => f.ToString("0.###"),
        double d => d.ToString("0.###"),
        int i => i.ToString(),
        uint u => u.ToString(),
        long l => l.ToString(),
        ulong ul => ul.ToString(),
        null => "null",
        _ => value.ToString() ?? "null"
    };
}
