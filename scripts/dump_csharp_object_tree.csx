#!/usr/bin/env dotnet-script
// Dump C# SerializedFile object trees using TypeTreeReader
// Usage: dotnet script scripts/dump_csharp_object_tree.csx path/to/bundle.hhh [-o output.json]

#r "nuget: System.Text.Json, 8.0.4"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityAssetParser.Bundle;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;

if (Args.Count == 0)
{
    Console.WriteLine("Usage: dotnet script scripts/dump_csharp_object_tree.csx <input> [-o output.json]");
    return;
}

var inputPath = Args[0];
string? outputPath = null;
for (int i = 1; i < Args.Count - 1; i++)
{
    if (Args[i] == "-o" || Args[i] == "--output")
    {
        outputPath = Args[i + 1];
        break;
    }
}

outputPath ??= $"{inputPath}_csharp_tree.json";

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

var serializedFile = LoadSerializedFile(inputPath);

var objects = new List<Dictionary<string, object?>>();

foreach (var obj in serializedFile.Objects)
{
    var type = serializedFile.TypeTree.GetType(obj.TypeId);
    var nodes = type?.Nodes;

    Dictionary<string, object?> tree;
    if (nodes == null || nodes.Count == 0)
    {
        tree = new Dictionary<string, object?> { ["__error__"] = "TypeTree nodes missing" };
    }
    else
    {
        var objectData = serializedFile.ReadObjectData(obj);
        using var ms = new MemoryStream(objectData.ToArray(), writable: false);
        using var reader = new EndianBinaryReader(ms, serializedFile.Header.Endianness == 1);
        var typeTreeReader = new TypeTreeReader(reader, nodes);
        try
        {
            tree = typeTreeReader.ReadObject();
        }
        catch (Exception ex)
        {
            tree = new Dictionary<string, object?> { ["__error__"] = ex.Message };
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
