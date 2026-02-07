using System.Numerics;
using UnityAssetParser.Bundle;
using UnityAssetParser.Export;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Services;

/// <summary>
/// Extracts minimal material + texture info from Unity bundles for preview rendering.
/// </summary>
public sealed class MaterialExtractionService
{
    public MaterialInfo? ExtractPrimaryMaterialFromBundle(byte[] bundleBytes)
    {
        using var ms = new MemoryStream(bundleBytes, writable: false);
        var parseResult = BundleFile.TryParse(ms);
        if (!parseResult.Success || parseResult.Bundle == null)
        {
            return null;
        }

        var bundle = parseResult.Bundle;
        if (!TryFindSerializedFile(bundle, out var serializedFile, out _))
        {
            return null;
        }

        return ExtractPrimaryMaterial(serializedFile, bundle.Nodes, bundle.DataRegion);
    }

    public MaterialInfo? ExtractPrimaryMaterialFromSerializedFile(byte[] serializedFileBytes)
    {
        var serializedFile = SerializedFile.SerializedFile.Parse(serializedFileBytes);
        return ExtractPrimaryMaterial(serializedFile, nodes: null, dataRegion: null);
    }

    public List<MaterialInfo> ExtractMaterialsFromBundle(byte[] bundleBytes)
    {
        using var ms = new MemoryStream(bundleBytes, writable: false);
        var parseResult = BundleFile.TryParse(ms);
        if (!parseResult.Success || parseResult.Bundle == null)
        {
            return new List<MaterialInfo>();
        }

        var bundle = parseResult.Bundle;
        if (!TryFindSerializedFile(bundle, out var serializedFile, out _))
        {
            return new List<MaterialInfo>();
        }

        return ExtractMaterialsWithTextures(serializedFile, bundle.Nodes, bundle.DataRegion);
    }

    public List<MaterialInfo> ExtractMaterialsFromSerializedFile(byte[] serializedFileBytes)
    {
        var serializedFile = SerializedFile.SerializedFile.Parse(serializedFileBytes);
        return ExtractMaterialsWithTextures(serializedFile, nodes: null, dataRegion: null);
    }

    private static MaterialInfo? ExtractPrimaryMaterial(
        SerializedFile.SerializedFile serializedFile,
        IReadOnlyList<NodeInfo>? nodes,
        DataRegion? dataRegion)
    {
        var materials = ExtractMaterialsWithTextures(serializedFile, nodes, dataRegion);

        // Prefer a material that references a texture we can decode
        foreach (var material in materials)
        {
            if (material.BaseColorTexture != null)
            {
                return material;
            }
        }

        // Otherwise, return first material or a texture-only fallback
        if (materials.Count > 0)
        {
            return materials[0];
        }

        if (materials.Count == 0)
        {
            var textures = ExtractTextures(serializedFile, nodes, dataRegion);
            if (textures.Count > 0)
            {
                return new MaterialInfo
                {
                    Name = "texture-only",
                    BaseColor = new Vector4(1f, 1f, 1f, 1f),
                    BaseColorTexture = textures.Values.First()
                };
            }
        }

        return null;
    }

    private static List<MaterialInfo> ExtractMaterialsWithTextures(
        SerializedFile.SerializedFile serializedFile,
        IReadOnlyList<NodeInfo>? nodes,
        DataRegion? dataRegion)
    {
        var materials = ExtractMaterials(serializedFile);
        Console.WriteLine($"DEBUG: Materials found: {materials.Count}");

        var referenced = new HashSet<long>();
        foreach (var material in materials)
        {
            if (material.BaseColorTexturePathId.HasValue)
            {
                referenced.Add(material.BaseColorTexturePathId.Value);
            }
        }

        var textures = ExtractTextures(serializedFile, nodes, dataRegion, referenced);
        Console.WriteLine($"DEBUG: Textures decoded: {textures.Count} (referenced: {referenced.Count})");

        foreach (var material in materials)
        {
            if (material.BaseColorTexturePathId.HasValue &&
                textures.TryGetValue(material.BaseColorTexturePathId.Value, out var tex))
            {
                material.BaseColorTexture = tex;
            }
        }

        return materials;
    }

    private static Dictionary<long, TextureInfo> ExtractTextures(
        SerializedFile.SerializedFile serializedFile,
        IReadOnlyList<NodeInfo>? nodes,
        DataRegion? dataRegion,
        IReadOnlySet<long>? allowedPathIds = null)
    {
        var results = new Dictionary<long, TextureInfo>();
        var resolver = nodes != null && dataRegion != null ? new StreamingInfoResolver() : null;
        int totalTextures = 0;

        foreach (var obj in serializedFile.Objects)
        {
            if (obj.ClassId != 28) // Texture2D
            {
                continue;
            }
            totalTextures++;

            if (allowedPathIds != null && allowedPathIds.Count > 0 && !allowedPathIds.Contains(obj.PathId))
            {
                continue;
            }

            var tree = ReadObjectTree(serializedFile, obj);
            if (tree == null)
            {
                continue;
            }

            var name = GetString(tree, "m_Name") ?? $"Texture_{obj.PathId}";
            int width = GetInt(tree, "m_Width");
            int height = GetInt(tree, "m_Height");
            int formatValue = GetInt(tree, "m_TextureFormat");
            var format = (TextureFormat)formatValue;

            byte[] imageData = GetByteArray(tree, "m_ImageData");
            if ((imageData == null || imageData.Length == 0) && resolver != null)
            {
                var streamInfo = GetStreamingInfo(tree, "m_StreamData");
                if (streamInfo != null && streamInfo.Size > 0)
                {
                    try
                    {
                        imageData = resolver.Resolve(nodes!, dataRegion!, streamInfo).ToArray();
                        Console.WriteLine($"DEBUG: Texture '{name}' streamed {streamInfo.Size} bytes from {streamInfo.Path}");
                    }
                    catch
                    {
                        imageData = Array.Empty<byte>();
                    }
                }
            }

            if (imageData == null || imageData.Length == 0)
            {
                Console.WriteLine($"DEBUG: Texture '{name}' has no image data");
                continue;
            }

            if (!IsReasonableTextureSize(width, height, imageData.Length))
            {
                Console.WriteLine($"DEBUG: Skipping large texture '{name}' {width}x{height}, data={imageData.Length}");
                continue;
            }

            try
            {
                Console.WriteLine($"DEBUG: Decoding texture '{name}' pathId={obj.PathId} {width}x{height} format={formatValue} bytes={imageData.Length}");
                var rgba = TextureDecoder.DecodeToRgba32(format, imageData, width, height);
                results[obj.PathId] = new TextureInfo
                {
                    Name = name,
                    Width = width,
                    Height = height,
                    Rgba32 = rgba
                };
            }
            catch
            {
                Console.WriteLine($"DEBUG: Unsupported or malformed texture '{name}' format={formatValue} size={imageData.Length}");
                // Skip unsupported or malformed textures
            }
        }

        return results;
    }

    private static List<MaterialInfo> ExtractMaterials(SerializedFile.SerializedFile serializedFile)
    {
        var materials = new List<MaterialInfo>();

        foreach (var obj in serializedFile.Objects)
        {
            if (obj.ClassId != 21) // Material
            {
                continue;
            }

            var tree = ReadObjectTree(serializedFile, obj);
            if (tree == null)
            {
                continue;
            }

            var material = new MaterialInfo
            {
                Name = GetString(tree, "m_Name") ?? $"Material_{obj.PathId}",
                BaseColor = GetBaseColor(tree)
            };

            var texturePathId = GetMainTexturePathId(tree);
            if (texturePathId.HasValue)
            {
                material.BaseColorTexturePathId = texturePathId.Value;
                Console.WriteLine($"DEBUG: Material '{material.Name}' base texture pathId={texturePathId.Value}");
            }
            else
            {
                Console.WriteLine($"DEBUG: Material '{material.Name}' has no local base texture");
            }

            materials.Add(material);
        }

        return materials;
    }

    private static Dictionary<string, object?>? ReadObjectTree(SerializedFile.SerializedFile serializedFile, ObjectInfo obj)
    {
        var type = serializedFile.TypeTree.GetType(obj.TypeId);
        var nodes = type?.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            return null;
        }

        var objectData = serializedFile.ReadObjectData(obj);
        using var ms = new MemoryStream(objectData.ToArray(), writable: false);
        using var reader = new EndianBinaryReader(ms, serializedFile.Header.Endianness == 1);
        var typeTreeReader = TypeTreeReader.CreateFromFlatList(reader, nodes);
        return typeTreeReader.ReadObject();
    }

    private static bool TryFindSerializedFile(
        BundleFile bundle,
        out SerializedFile.SerializedFile serializedFile,
        out NodeInfo node)
    {
        serializedFile = null!;
        node = default;

        var candidates = bundle.Nodes
            .Where(n => !IsResourceNode(n.Path))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = bundle.Nodes.ToList();
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var data = bundle.ExtractNode(candidate);
                if (SerializedFile.SerializedFile.TryParse(data.Span, out var parsed, out _))
                {
                    serializedFile = parsed!;
                    node = candidate;
                    return true;
                }
            }
            catch
            {
                // ignore
            }
        }

        return false;
    }

    private static bool IsResourceNode(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value as string;
        }

        return null;
    }

    private static bool IsReasonableTextureSize(int width, int height, int dataLength)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        const int maxDim = 4096;
        if (width > maxDim || height > maxDim)
        {
            return false;
        }

        long pixelCount = (long)width * height;
        if (pixelCount > 4096L * 4096L)
        {
            return false;
        }

        // Reject absurdly large payloads to avoid WASM OOM
        const int maxDataBytes = 64 * 1024 * 1024;
        return dataLength <= maxDataBytes;
    }

    private static int GetInt(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            uint u => (int)u,
            short s => s,
            ushort us => us,
            byte b => b,
            _ => 0
        };
    }

    private static byte[] GetByteArray(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
        {
            return Array.Empty<byte>();
        }

        if (value is List<object?> list)
        {
            return list.Where(v => v != null).Select(v => Convert.ToByte(v)).ToArray();
        }

        if (value is Dictionary<string, object?> map &&
            map.TryGetValue("Array", out var arr) &&
            arr is List<object?> arrayList)
        {
            return arrayList.Where(v => v != null).Select(v => Convert.ToByte(v)).ToArray();
        }

        return Array.Empty<byte>();
    }

    private static StreamingInfo? GetStreamingInfo(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is not Dictionary<string, object?> streamDict)
        {
            return null;
        }

        var path = GetString(streamDict, "path") ?? string.Empty;
        long offset = GetLong(streamDict, "offset");
        long size = GetLong(streamDict, "size");

        if (string.IsNullOrEmpty(path) || size <= 0)
        {
            return null;
        }

        return new StreamingInfo
        {
            Path = path,
            Offset = offset,
            Size = size
        };
    }

    private static long GetLong(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            long l => l,
            uint u => u,
            ulong ul => (long)ul,
            _ => 0
        };
    }

    private static Vector4 GetBaseColor(Dictionary<string, object?> materialTree)
    {
        if (!materialTree.TryGetValue("m_SavedProperties", out var props) || props is not Dictionary<string, object?> propsDict)
        {
            return new Vector4(1f, 1f, 1f, 1f);
        }

        var colorEntries = GetColorEntries(propsDict);
        if (colorEntries.Count == 0)
        {
            return new Vector4(1f, 1f, 1f, 1f);
        }

        string[] preferredKeys =
        {
            "_BaseColor",
            "_Color",
            "_TintColor",
            "_MainColor",
            "_Diffuse",
            "_AlbedoColor",
            "_EmissionColor"
        };

        foreach (var key in preferredKeys)
        {
            if (colorEntries.TryGetValue(key, out var color))
            {
                Console.WriteLine($"DEBUG: Material base color from '{key}'");
                return color;
            }
        }

        var first = colorEntries.First();
        Console.WriteLine($"DEBUG: Material base color fallback to '{first.Key}'");
        return first.Value;
    }

    private static float GetFloat(Dictionary<string, object?> dict, string key, float fallback)
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

    private static Dictionary<string, Vector4> GetColorEntries(Dictionary<string, object?> propsDict)
    {
        var result = new Dictionary<string, Vector4>(StringComparer.OrdinalIgnoreCase);
        if (!propsDict.TryGetValue("m_Colors", out var colorsObj))
        {
            return result;
        }

        var entries = GetArray(colorsObj);
        if (entries == null)
        {
            return result;
        }

        foreach (var entryObj in entries)
        {
            if (entryObj is not Dictionary<string, object?> entry)
            {
                continue;
            }

            var key = GetString(entry, "first");
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (entry.TryGetValue("second", out var secondObj) && secondObj is Dictionary<string, object?> colorDict)
            {
                float r = GetFloat(colorDict, "r", 1f);
                float g = GetFloat(colorDict, "g", 1f);
                float b = GetFloat(colorDict, "b", 1f);
                float a = GetFloat(colorDict, "a", 1f);
                result[key] = new Vector4(r, g, b, a);
            }
        }

        return result;
    }

    private static long? GetMainTexturePathId(Dictionary<string, object?> materialTree)
    {
        if (!materialTree.TryGetValue("m_SavedProperties", out var props) || props is not Dictionary<string, object?> propsDict)
        {
            return null;
        }

        if (!propsDict.TryGetValue("m_TexEnvs", out var texObj))
        {
            return null;
        }

        var entries = GetArray(texObj);
        if (entries == null)
        {
            return null;
        }

        Console.WriteLine("DEBUG: Material texture properties:");

        long? fallback = null;

        foreach (var entryObj in entries)
        {
            if (entryObj is not Dictionary<string, object?> entry)
            {
                continue;
            }

            var key = GetString(entry, "first");
            if (!string.IsNullOrEmpty(key))
            {
                Console.WriteLine($"DEBUG:   TexEnv '{key}'");
            }

            if (!string.Equals(key, "_MainTex", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "_BaseMap", StringComparison.OrdinalIgnoreCase))
            {
                // Keep as fallback if we can parse a texture pointer
                var fallbackPathId = TryGetTexturePathId(entry);
                if (fallbackPathId.HasValue && fallback == null)
                {
                    fallback = fallbackPathId;
                }
                continue;
            }

            var pathId = TryGetTexturePathId(entry);
            if (pathId.HasValue)
            {
                return pathId.Value;
            }
            else if (string.Equals(key, "_MainTex", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(key, "_BaseMap", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"DEBUG: '{key}' texture pointer missing or external");
            }
        }

        return fallback;
    }

    private static List<object?>? GetArray(object? value)
    {
        if (value is List<object?> list)
        {
            return list;
        }

        if (value is Dictionary<string, object?> dict &&
            dict.TryGetValue("Array", out var arr) &&
            arr is List<object?> arrayList)
        {
            return arrayList;
        }

        return null;
    }

    private static long? TryGetTexturePathId(Dictionary<string, object?> entry)
    {
        if (!entry.TryGetValue("second", out var secondObj) || secondObj is not Dictionary<string, object?> texEnvDict)
        {
            return null;
        }

        if (!texEnvDict.TryGetValue("m_Texture", out var texPtrObj) || texPtrObj is not Dictionary<string, object?> texPtr)
        {
            return null;
        }

        var fileId = GetInt(texPtr, "m_FileID");
        var pathId = GetLong(texPtr, "m_PathID");
        if (fileId != 0)
        {
            Console.WriteLine($"DEBUG: Texture reference points to external fileId={fileId}, pathId={pathId}");
            return null;
        }
        if (pathId == 0)
        {
            Console.WriteLine("DEBUG: Texture reference has pathId=0");
            return null;
        }
        if (fileId == 0 && pathId != 0)
        {
            return pathId;
        }

        return null;
    }
}
