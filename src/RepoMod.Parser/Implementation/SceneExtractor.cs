using System.Formats.Tar;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AssetStudio;
using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public sealed class SceneExtractor(IArchiveScanner archiveScanner, IModParser modParser) : ISceneExtractor
{
    private static readonly Regex MetaGuidRegex = new(@"^guid:\s*([0-9a-fA-F]+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ReferenceGuidRegex = new(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
    private static readonly Regex GenericGuidRegex = new(@"\b[0-9a-fA-F]{32}\b", RegexOptions.Compiled);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public ParseSceneResult ParseUnityPackage(string unityPackagePath)
    {
        if (string.IsNullOrWhiteSpace(unityPackagePath))
        {
            return ParseSceneResult.Failed("Unitypackage path is required.");
        }

        if (!File.Exists(unityPackagePath))
        {
            return ParseSceneResult.Failed($"Unitypackage not found: {unityPackagePath}");
        }

        var scanResult = archiveScanner.ScanUnityPackage(unityPackagePath);
        if (!scanResult.Success)
        {
            return ParseSceneResult.Failed(scanResult.Error ?? "Unitypackage scan failed.");
        }

        try
        {
            var containerId = BuildContainerId("unitypackage", unityPackagePath);
            var container = new ContainerDescriptor(containerId, Path.GetFullPath(unityPackagePath), "unitypackage", Path.GetFileName(unityPackagePath));

            var packageItems = ReadUnityPackageItems(unityPackagePath);
            var byPath = packageItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Pathname))
                .ToDictionary(item => item.Pathname!, item => item, PathComparer);

            var assets = new List<ParsedAssetRecord>();
            var refs = new List<UnityObjectRef>();
            var renderObjects = new List<UnityRenderObject>();
            var renderMeshes = new List<UnityRenderMesh>();
            var renderMaterials = new List<UnityRenderMaterial>();
            var renderTextures = new List<UnityRenderTexture>();
            var avatarAssetIds = new List<string>();
            var warnings = new List<string>(scanResult.Warnings);

            foreach (var discovered in scanResult.Bundles)
            {
                byPath.TryGetValue(discovered.FullPath, out var packageItem);
                var packageGuid = packageItem?.Guid;
                var metaGuid = TryExtractMetaGuid(packageItem?.MetaText);
                var assetId = BuildAssetId(containerId, packageGuid, discovered.FullPath);
                var isAvatar = IsAvatarPath(discovered.FullPath);
                var slotTag = modParser.ExtractMetadata(discovered.FileName).SlotTag;
                var assetKind = InferAssetKind(discovered.FullPath, discovered.Extension);
                var referencedGuids = ExtractReferencedGuids(packageItem?.AssetBytes, packageItem?.MetaText);

                assets.Add(new ParsedAssetRecord(
                    assetId,
                    containerId,
                    discovered.FullPath,
                    discovered.FileName,
                    discovered.Extension,
                    assetKind,
                    discovered.SizeBytes,
                    packageGuid,
                    metaGuid,
                    referencedGuids,
                    isAvatar,
                    false,
                    slotTag));

                var extracted = BuildObjectRefsFromSerializedAsset(
                    assetId,
                    containerId,
                    packageGuid,
                    discovered.FileName,
                    packageItem?.AssetBytes,
                    warnings);

                refs.AddRange(extracted.ObjectRefs);
                renderObjects.AddRange(extracted.RenderObjects);
                renderMeshes.AddRange(extracted.RenderMeshes);
                renderMaterials.AddRange(extracted.RenderMaterials);
                renderTextures.AddRange(extracted.RenderTextures);

                if (isAvatar)
                {
                    avatarAssetIds.Add(assetId);
                }
            }

            if (avatarAssetIds.Count == 0)
            {
                warnings.Add("No avatar candidate assets were detected in unitypackage output.");
            }

            var scene = new ParsedModScene(
                BuildSceneId(containerId),
                container,
                assets,
                refs,
                renderObjects,
                renderMeshes,
                renderMaterials,
                renderTextures,
                [],
                avatarAssetIds,
                BuildGraph(container, assets, refs, [], warnings),
                warnings);

            return ParseSceneResult.Succeeded(scene);
        }
        catch (Exception ex)
        {
            return ParseSceneResult.Failed($"Failed to parse unitypackage scene metadata: {ex.Message}");
        }
    }

    public ParseSceneResult ParseCosmeticBundle(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            return ParseSceneResult.Failed("Bundle path is required.");
        }

        if (!File.Exists(bundlePath))
        {
            return ParseSceneResult.Failed($"Bundle not found: {bundlePath}");
        }

        var fileInfo = new FileInfo(bundlePath);
        var containerId = BuildContainerId("hhh", bundlePath);
        var container = new ContainerDescriptor(containerId, fileInfo.FullName, "hhh", fileInfo.Name);
        var slotTag = modParser.ExtractMetadata(fileInfo.Name).SlotTag;
        var assetId = BuildAssetId(containerId, null, fileInfo.Name);
        var bundleBytes = File.ReadAllBytes(fileInfo.FullName);
        var externalReferenceGuids = ExtractExternalGuids(bundleBytes);

        var assets = new[]
        {
            new ParsedAssetRecord(
                assetId,
                containerId,
                fileInfo.FullName,
                fileInfo.Name,
                fileInfo.Extension.ToLowerInvariant(),
                "cosmetic-bundle",
                fileInfo.Length,
                null,
                null,
                [],
                false,
                true,
                slotTag)
        };

        var refs = new[]
        {
            new UnityObjectRef(
                BuildObjectId(assetId, null),
                assetId,
                containerId,
                null,
                null,
                null,
                null,
                fileInfo.Name,
                [])
        };

        var hint = new AttachmentHint(
            assetId,
            slotTag,
            BuildCandidateBoneNames(slotTag),
            BuildCandidateNodePaths(slotTag),
            externalReferenceGuids);

        var warnings = new List<string>
        {
            "Cosmetic bundle requires avatar-package context for downstream attachment/linking."
        };

        if (externalReferenceGuids.Count == 0)
        {
            warnings.Add("No explicit external GUID references were detected in cosmetic bundle bytes.");
        }

        var scene = new ParsedModScene(
            BuildSceneId(containerId),
            container,
            assets,
            refs,
                [],
                [],
                [],
                [],
            [hint],
            [],
            BuildGraph(container, assets, refs, [hint], warnings),
            warnings);

        return ParseSceneResult.Succeeded(scene);
    }

    private static List<UnityPackageItem> ReadUnityPackageItems(string unityPackagePath)
    {
        var itemsByGuid = new Dictionary<string, UnityPackageItem>(StringComparer.Ordinal);

        using var packageStream = File.OpenRead(unityPackagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress, leaveOpen: false);
        using var tarReader = new TarReader(gzipStream, leaveOpen: false);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType is TarEntryType.Directory || entry.DataStream is null || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var separatorIndex = entry.Name.IndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= entry.Name.Length - 1)
            {
                continue;
            }

            var guid = entry.Name[..separatorIndex];
            var memberName = entry.Name[(separatorIndex + 1)..];

            if (!itemsByGuid.TryGetValue(guid, out var item))
            {
                item = new UnityPackageItem { Guid = guid };
                itemsByGuid[guid] = item;
            }

            switch (memberName)
            {
                case "pathname":
                    item.Pathname = ReadUtf8(entry.DataStream);
                    break;
                case "asset":
                    item.AssetBytes = ReadAllBytes(entry.DataStream);
                    break;
                case "asset.meta":
                    item.MetaText = ReadUtf8(entry.DataStream);
                    break;
            }
        }

        return itemsByGuid.Values.ToList();
    }

    private static string? TryExtractMetaGuid(string? metaText)
    {
        if (string.IsNullOrWhiteSpace(metaText))
        {
            return null;
        }

        var match = MetaGuidRegex.Match(metaText);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string BuildContainerId(string sourceType, string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceType}:{fullPath}")));
        return $"{sourceType}:{hash[..16].ToLowerInvariant()}";
    }

    private static string BuildAssetId(string containerId, string? packageGuid, string path)
    {
        var identity = packageGuid is null ? path : $"{packageGuid}:{path}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return $"{containerId}:asset:{hash[..16].ToLowerInvariant()}";
    }

    private static string BuildObjectId(string assetId, string? packageGuid)
        => packageGuid is null ? $"{assetId}:obj:root" : $"{assetId}:obj:{packageGuid}";

    private static string BuildSceneId(string containerId)
        => $"{containerId}:scene";

    private static bool IsAvatarPath(string path)
        => path.Contains("PlayerAvatar", StringComparison.OrdinalIgnoreCase)
           || path.Contains("avatar", StringComparison.OrdinalIgnoreCase);

    private static string InferAssetKind(string path, string extension)
    {
        if (path.Contains("/Meshes/", StringComparison.OrdinalIgnoreCase) || path.Contains("\\Meshes\\", StringComparison.OrdinalIgnoreCase))
        {
            return "mesh";
        }

        if (path.Contains("/Textures/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Textures\\", StringComparison.OrdinalIgnoreCase)
            || extension is ".png" or ".jpg" or ".jpeg" or ".tga" or ".dds")
        {
            return "texture";
        }

        if (path.Contains("/Materials/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Materials\\", StringComparison.OrdinalIgnoreCase)
            || extension == ".mat")
        {
            return "material";
        }

        if (extension == ".shader")
        {
            return "shader";
        }

        if (extension == ".prefab")
        {
            return "prefab";
        }

        if (extension == ".hhh")
        {
            return "cosmetic-bundle";
        }

        return "asset";
    }

    private static IReadOnlyList<string> ExtractReferencedGuids(byte[]? assetBytes, string? metaText)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(metaText))
        {
            foreach (Match match in ReferenceGuidRegex.Matches(metaText))
            {
                set.Add(match.Groups[1].Value.ToLowerInvariant());
            }
        }

        if (assetBytes is { Length: > 0 })
        {
            try
            {
                var text = Encoding.UTF8.GetString(assetBytes);
                foreach (Match match in ReferenceGuidRegex.Matches(text))
                {
                    set.Add(match.Groups[1].Value.ToLowerInvariant());
                }
            }
            catch
            {
            }
        }

        return set.ToArray();
    }

    private static IReadOnlyList<string> ExtractExternalGuids(byte[] bundleBytes)
    {
        if (bundleBytes.Length == 0)
        {
            return [];
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var utf8Text = Encoding.UTF8.GetString(bundleBytes);
        foreach (Match match in GenericGuidRegex.Matches(utf8Text))
        {
            set.Add(match.Value.ToLowerInvariant());
        }

        var asciiText = Encoding.ASCII.GetString(bundleBytes);
        foreach (Match match in GenericGuidRegex.Matches(asciiText))
        {
            set.Add(match.Value.ToLowerInvariant());
        }

        return set.Take(64).ToArray();
    }

    private static (
        IReadOnlyList<UnityObjectRef> ObjectRefs,
        IReadOnlyList<UnityRenderObject> RenderObjects,
        IReadOnlyList<UnityRenderMesh> RenderMeshes,
        IReadOnlyList<UnityRenderMaterial> RenderMaterials,
        IReadOnlyList<UnityRenderTexture> RenderTextures) BuildObjectRefsFromSerializedAsset(
        string assetId,
        string containerId,
        string? packageGuid,
        string fallbackName,
        byte[]? assetBytes,
        List<string> warnings)
    {
        if (assetBytes is not { Length: > 0 })
        {
            return (
                [
                    new UnityObjectRef(
                        BuildObjectId(assetId, packageGuid),
                        assetId,
                        containerId,
                        packageGuid,
                        null,
                        null,
                        null,
                        fallbackName,
                        [])
                ],
                [],
                [],
                [],
                []);
        }

        try
        {
            using var stream = new MemoryStream(assetBytes, writable: false);
            using var fileReader = new FileReader(fallbackName, stream);
            if (fileReader.FileType != FileType.AssetsFile)
            {
                return (
                    [
                        new UnityObjectRef(
                            BuildObjectId(assetId, packageGuid),
                            assetId,
                            containerId,
                            packageGuid,
                            null,
                            null,
                            null,
                            fallbackName,
                            [])
                    ],
                        [],
                        [],
                        [],
                        []);
            }

            var assetsManager = new AssetsManager();
            var serializedFile = new SerializedFile(fileReader, assetsManager);
            if (serializedFile.m_Objects.Count == 0)
            {
                return (
                    [
                        new UnityObjectRef(
                            BuildObjectId(assetId, packageGuid),
                            assetId,
                            containerId,
                            packageGuid,
                            null,
                            null,
                            null,
                            fallbackName,
                            [])
                    ],
                        [],
                        [],
                        [],
                        []);
            }

            var objectRefs = new List<UnityObjectRef>(serializedFile.m_Objects.Count);
            var renderObjects = new List<UnityRenderObject>();
                    var renderMeshes = new List<UnityRenderMesh>();
                    var renderMaterials = new List<UnityRenderMaterial>();
                    var renderTextures = new List<UnityRenderTexture>();
            foreach (var objectInfo in serializedFile.m_Objects)
            {
                var pathIdText = objectInfo.m_PathID.ToString(CultureInfo.InvariantCulture);
                var objectName = BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID);
                var outboundRefs = ExtractOutboundObjectPointers(serializedFile, objectInfo);
                objectRefs.Add(new UnityObjectRef(
                    $"{assetId}:obj:{pathIdText}",
                    assetId,
                    containerId,
                    packageGuid,
                    serializedFile.fileName,
                    pathIdText,
                    objectInfo.classID,
                    objectName,
                    outboundRefs));

                var renderObject = TryBuildRenderObject(assetId, objectInfo, serializedFile);
                if (renderObject is not null)
                {
                    renderObjects.Add(renderObject);
                }

                var renderMesh = TryBuildRenderMesh(assetId, objectInfo, serializedFile);
                if (renderMesh is not null)
                {
                    renderMeshes.Add(renderMesh);
                }

                var renderMaterial = TryBuildRenderMaterial(assetId, objectInfo, serializedFile);
                if (renderMaterial is not null)
                {
                    renderMaterials.Add(renderMaterial);
                }

                var renderTexture = TryBuildRenderTexture(assetId, objectInfo, serializedFile);
                if (renderTexture is not null)
                {
                    renderTextures.Add(renderTexture);
                }
            }

            return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
        }
        catch (Exception ex)
        {
            warnings.Add($"Object-level read failed for '{fallbackName}': {ex.Message}");
            return (
                [
                    new UnityObjectRef(
                        BuildObjectId(assetId, packageGuid),
                        assetId,
                        containerId,
                        packageGuid,
                        null,
                        null,
                        null,
                        fallbackName,
                        [])
                ],
                [],
                [],
                [],
                []);
        }
    }

    private static UnityRenderMesh? TryBuildRenderMesh(string assetId, ObjectInfo objectInfo, SerializedFile serializedFile)
    {
        if (objectInfo.classID != (int)ClassIDType.Mesh)
        {
            return null;
        }

        var objectId = $"{assetId}:obj:{objectInfo.m_PathID.ToString(CultureInfo.InvariantCulture)}";
        var data = ReadObjectTypeData(serializedFile, objectInfo);
        var vertexCount = ReadIntValue(data, "m_VertexCount")
            ?? ReadNestedIntValue(data, "m_VertexData", "m_VertexCount");
        var subMeshCount = ReadArrayCount(data, "m_SubMeshes");
        var blendShapeCount = ReadNestedArrayCount(data, "m_Shapes", "channels");
        var vertexChannelCount = ReadNestedArrayCount(data, "m_VertexData", "m_Channels");
        var vertexDataByteCount = ReadNestedArrayCount(data, "m_VertexData", "m_DataSize");
        var indexBufferElementCount = ReadArrayCount(data, "m_IndexBuffer");
        var indexFormat = ReadIntValue(data, "m_IndexFormat");
        var vertexDataBytes = ReadNestedByteArray(data, "m_VertexData", "m_DataSize");
        var indexBufferBytes = ReadByteArray(data, "m_IndexBuffer");
        var indexValues = DecodeIndexValues(indexBufferBytes, indexFormat);
        var decodedChannels = DecodeVertexChannels(data, vertexDataBytes, vertexCount);

        return new UnityRenderMesh(
            objectId,
            assetId,
            BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
            vertexCount,
            subMeshCount,
            blendShapeCount,
            vertexChannelCount,
            vertexDataByteCount,
            indexBufferElementCount,
            indexFormat,
            vertexDataBytes is { Length: > 0 } ? Convert.ToBase64String(vertexDataBytes) : null,
            indexValues.Count > 0 ? indexValues : null,
            decodedChannels.Positions,
            decodedChannels.Normals,
            decodedChannels.Uv0);
    }

    private static UnityRenderMaterial? TryBuildRenderMaterial(string assetId, ObjectInfo objectInfo, SerializedFile serializedFile)
    {
        if (objectInfo.classID != (int)ClassIDType.Material)
        {
            return null;
        }

        var objectId = $"{assetId}:obj:{objectInfo.m_PathID.ToString(CultureInfo.InvariantCulture)}";
        var data = ReadObjectTypeData(serializedFile, objectInfo);

        string? shaderObjectId = null;
        if (TryGetDictionaryValue(data, "m_Shader", out var shaderValue)
            && shaderValue is OrderedDictionary shaderDict
            && TryReadObjectPointer(shaderDict, out var shaderPointer))
        {
            shaderObjectId = ResolvePointerObjectId(assetId, shaderPointer.FileId, ParsePathId(shaderPointer.PathId));
        }

        var textureBindings = ExtractTextureBindings(assetId, data);
        var floatProperties = ExtractFloatProperties(data);
        var colorProperties = ExtractColorProperties(data);
        return new UnityRenderMaterial(
            objectId,
            assetId,
            BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
            shaderObjectId,
            textureBindings,
            floatProperties,
            colorProperties);
    }

    private static UnityRenderTexture? TryBuildRenderTexture(string assetId, ObjectInfo objectInfo, SerializedFile serializedFile)
    {
        if (objectInfo.classID != (int)ClassIDType.Texture2D)
        {
            return null;
        }

        var objectId = $"{assetId}:obj:{objectInfo.m_PathID.ToString(CultureInfo.InvariantCulture)}";
        var data = ReadObjectTypeData(serializedFile, objectInfo);
        var streamInfo = ExtractTextureStreamInfo(data);
        return new UnityRenderTexture(
            objectId,
            assetId,
            BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
            ReadIntValue(data, "m_Width"),
            ReadIntValue(data, "m_Height"),
            ReadIntValue(data, "m_TextureFormat"),
            ReadArrayCount(data, "m_ImageData"),
            streamInfo.Path,
            streamInfo.Offset,
            streamInfo.Size);
    }

    private static OrderedDictionary ReadObjectTypeData(SerializedFile serializedFile, ObjectInfo objectInfo)
    {
        if (objectInfo.serializedType?.m_Type?.m_Nodes is not { Count: > 0 })
        {
            return new OrderedDictionary();
        }

        try
        {
            using var objectReader = new ObjectReader(serializedFile.reader, serializedFile, objectInfo);
            return TypeTreeHelper.ReadType(objectInfo.serializedType.m_Type, objectReader);
        }
        catch
        {
            return new OrderedDictionary();
        }
    }

    private static IReadOnlyList<UnityRenderTextureBinding> ExtractTextureBindings(string assetId, OrderedDictionary data)
    {
        if (!TryGetDictionaryValue(data, "m_SavedProperties", out var savedProperties)
            || savedProperties is not OrderedDictionary savedPropertiesDictionary
            || !TryGetDictionaryValue(savedPropertiesDictionary, "m_TexEnvs", out var texEnvs)
            || texEnvs is not Array texEnvArray)
        {
            return [];
        }

        var bindings = new List<UnityRenderTextureBinding>();
        foreach (var entry in texEnvArray)
        {
            if (entry is not OrderedDictionary pair)
            {
                continue;
            }

            if (!TryGetDictionaryValue(pair, "first", out var slotNameValue)
                || slotNameValue is not string slotName
                || !TryGetDictionaryValue(pair, "second", out var secondValue)
                || secondValue is not OrderedDictionary texEnv
                || !TryGetDictionaryValue(texEnv, "m_Texture", out var textureValue)
                || textureValue is not OrderedDictionary texturePointer)
            {
                continue;
            }

            if (!TryReadObjectPointer(texturePointer, out var pointer))
            {
                continue;
            }

            var textureObjectId = ResolvePointerObjectId(assetId, pointer.FileId, ParsePathId(pointer.PathId));
            if (string.IsNullOrWhiteSpace(textureObjectId))
            {
                continue;
            }

            bindings.Add(new UnityRenderTextureBinding(slotName, textureObjectId));
        }

        return bindings;
    }

    private static IReadOnlyList<UnityRenderFloatProperty> ExtractFloatProperties(OrderedDictionary data)
    {
        if (!TryGetDictionaryValue(data, "m_SavedProperties", out var savedProperties)
            || savedProperties is not OrderedDictionary savedPropertiesDictionary
            || !TryGetDictionaryValue(savedPropertiesDictionary, "m_Floats", out var floatsValue)
            || floatsValue is not Array floatsArray)
        {
            return [];
        }

        var properties = new List<UnityRenderFloatProperty>();
        foreach (var entry in floatsArray)
        {
            if (entry is not OrderedDictionary pair)
            {
                continue;
            }

            if (!TryGetDictionaryValue(pair, "first", out var nameValue)
                || nameValue is not string name
                || !TryGetDictionaryValue(pair, "second", out var floatValue)
                || !TryConvertToFloat(floatValue, out var parsedFloat))
            {
                continue;
            }

            properties.Add(new UnityRenderFloatProperty(name, parsedFloat));
        }

        return properties;
    }

    private static IReadOnlyList<UnityRenderColorProperty> ExtractColorProperties(OrderedDictionary data)
    {
        if (!TryGetDictionaryValue(data, "m_SavedProperties", out var savedProperties)
            || savedProperties is not OrderedDictionary savedPropertiesDictionary
            || !TryGetDictionaryValue(savedPropertiesDictionary, "m_Colors", out var colorsValue)
            || colorsValue is not Array colorsArray)
        {
            return [];
        }

        var properties = new List<UnityRenderColorProperty>();
        foreach (var entry in colorsArray)
        {
            if (entry is not OrderedDictionary pair)
            {
                continue;
            }

            if (!TryGetDictionaryValue(pair, "first", out var nameValue)
                || nameValue is not string name
                || !TryGetDictionaryValue(pair, "second", out var colorValue)
                || colorValue is not OrderedDictionary colorDictionary)
            {
                continue;
            }

            var r = ReadFloatValue(colorDictionary, "r");
            var g = ReadFloatValue(colorDictionary, "g");
            var b = ReadFloatValue(colorDictionary, "b");
            var a = ReadFloatValue(colorDictionary, "a");

            if (r is null || g is null || b is null || a is null)
            {
                continue;
            }

            properties.Add(new UnityRenderColorProperty(name, r.Value, g.Value, b.Value, a.Value));
        }

        return properties;
    }

    private static (string? Path, long? Offset, int? Size) ExtractTextureStreamInfo(OrderedDictionary data)
    {
        if (TryGetDictionaryValue(data, "m_StreamData", out var streamValue)
            && streamValue is OrderedDictionary streamDictionary)
        {
            var path = ReadStringValue(streamDictionary, "path");
            var offset = ReadLongValue(streamDictionary, "offset");
            var size = ReadIntValue(streamDictionary, "size");
            if (!string.IsNullOrWhiteSpace(path) || offset is not null || size is not null)
            {
                return (path, offset, size);
            }
        }

        if (TryGetDictionaryValue(data, "m_DataStreamData", out var dataStreamValue)
            && dataStreamValue is OrderedDictionary dataStreamDictionary)
        {
            var path = ReadStringValue(dataStreamDictionary, "path");
            var offset = ReadLongValue(dataStreamDictionary, "offset");
            var size = ReadIntValue(dataStreamDictionary, "size");
            return (path, offset, size);
        }

        return (null, null, null);
    }

    private static int? ReadIntValue(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value))
        {
            return null;
        }

        return TryConvertToInt(value, out var parsed) ? parsed : null;
    }

    private static int? ReadNestedIntValue(OrderedDictionary dictionary, string outerKey, string innerKey)
    {
        if (!TryGetDictionaryValue(dictionary, outerKey, out var outerValue)
            || outerValue is not OrderedDictionary outerDictionary)
        {
            return null;
        }

        return ReadIntValue(outerDictionary, innerKey);
    }

    private static int? ReadArrayCount(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value)
            || value is not Array array)
        {
            return null;
        }

        return array.Length;
    }

    private static float? ReadFloatValue(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value)
            || !TryConvertToFloat(value, out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static long? ReadLongValue(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value)
            || !TryConvertToLong(value, out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static string? ReadStringValue(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value))
        {
            return null;
        }

        return value as string;
    }

    private static int? ReadNestedArrayCount(OrderedDictionary dictionary, string outerKey, string innerKey)
    {
        if (!TryGetDictionaryValue(dictionary, outerKey, out var outerValue)
            || outerValue is not OrderedDictionary outerDictionary)
        {
            return null;
        }

        return ReadArrayCount(outerDictionary, innerKey);
    }

    private static byte[]? ReadByteArray(OrderedDictionary dictionary, string key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out var value)
            || value is not Array array)
        {
            return null;
        }

        var bytes = new byte[array.Length];
        for (var index = 0; index < array.Length; index++)
        {
            if (!TryConvertToInt(array.GetValue(index), out var intValue)
                || intValue < byte.MinValue
                || intValue > byte.MaxValue)
            {
                return null;
            }

            bytes[index] = (byte)intValue;
        }

        return bytes;
    }

    private static byte[]? ReadNestedByteArray(OrderedDictionary dictionary, string outerKey, string innerKey)
    {
        if (!TryGetDictionaryValue(dictionary, outerKey, out var outerValue)
            || outerValue is not OrderedDictionary outerDictionary)
        {
            return null;
        }

        return ReadByteArray(outerDictionary, innerKey);
    }

    private static IReadOnlyList<int> DecodeIndexValues(byte[]? indexBufferBytes, int? indexFormat)
    {
        if (indexBufferBytes is not { Length: > 0 })
        {
            return [];
        }

        if (indexFormat == 1)
        {
            var values = new List<int>(indexBufferBytes.Length / 4);
            for (var offset = 0; offset + 3 < indexBufferBytes.Length; offset += 4)
            {
                values.Add(BitConverter.ToInt32(indexBufferBytes, offset));
            }

            return values;
        }

        var defaultValues = new List<int>(indexBufferBytes.Length / 2);
        for (var offset = 0; offset + 1 < indexBufferBytes.Length; offset += 2)
        {
            defaultValues.Add(BitConverter.ToUInt16(indexBufferBytes, offset));
        }

        return defaultValues;
    }

    private static (IReadOnlyList<float>? Positions, IReadOnlyList<float>? Normals, IReadOnlyList<float>? Uv0) DecodeVertexChannels(
        OrderedDictionary meshData,
        byte[]? vertexDataBytes,
        int? vertexCount)
    {
        if (vertexDataBytes is not { Length: > 0 }
            || vertexCount is not > 0
            || !TryGetDictionaryValue(meshData, "m_VertexData", out var vertexDataValue)
            || vertexDataValue is not OrderedDictionary vertexDataDictionary
            || !TryGetDictionaryValue(vertexDataDictionary, "m_Channels", out var channelsValue)
            || channelsValue is not Array channelsArray
            || channelsArray.Length == 0)
        {
            return (null, null, null);
        }

        var channelInfos = ParseVertexChannels(channelsArray);
        if (channelInfos.Count == 0)
        {
            return (null, null, null);
        }

        var streamLayouts = BuildStreamLayouts(channelInfos);
        if (streamLayouts.Count == 0)
        {
            return (null, null, null);
        }

        var streamBaseOffsets = BuildStreamBaseOffsets(streamLayouts, vertexCount.Value, vertexDataBytes.Length);

        var positions = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 0, expectedDimension: 3);
        var normals = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 1, expectedDimension: 3);
        var uv0 = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 4, expectedDimension: 2);

        return (
            positions.Count > 0 ? positions : null,
            normals.Count > 0 ? normals : null,
            uv0.Count > 0 ? uv0 : null);
    }

    private static Dictionary<int, VertexChannelInfo> ParseVertexChannels(Array channelsArray)
    {
        var result = new Dictionary<int, VertexChannelInfo>();
        for (var index = 0; index < channelsArray.Length; index++)
        {
            if (channelsArray.GetValue(index) is not OrderedDictionary channel)
            {
                continue;
            }

            if (!TryGetDictionaryValue(channel, "stream", out var streamValue)
                && !TryGetDictionaryValue(channel, "m_Stream", out streamValue))
            {
                continue;
            }

            if (!TryGetDictionaryValue(channel, "offset", out var offsetValue)
                && !TryGetDictionaryValue(channel, "m_Offset", out offsetValue))
            {
                continue;
            }

            if (!TryGetDictionaryValue(channel, "format", out var formatValue)
                && !TryGetDictionaryValue(channel, "m_Format", out formatValue))
            {
                continue;
            }

            if (!TryGetDictionaryValue(channel, "dimension", out var dimensionValue)
                && !TryGetDictionaryValue(channel, "m_Dimension", out dimensionValue))
            {
                continue;
            }

            if (!TryConvertToInt(streamValue, out var stream)
                || !TryConvertToInt(offsetValue, out var offset)
                || !TryConvertToInt(formatValue, out var format)
                || !TryConvertToInt(dimensionValue, out var dimension)
                || dimension <= 0)
            {
                continue;
            }

            result[index] = new VertexChannelInfo(stream, offset, format, dimension);
        }

        return result;
    }

    private static Dictionary<int, int> BuildStreamLayouts(IReadOnlyDictionary<int, VertexChannelInfo> channels)
    {
        var streamStrides = new Dictionary<int, int>();
        foreach (var channel in channels.Values)
        {
            var componentSize = ResolveVertexFormatSize(channel.Format);
            if (componentSize <= 0)
            {
                continue;
            }

            var requiredStride = channel.Offset + componentSize * channel.Dimension;
            if (!streamStrides.TryGetValue(channel.Stream, out var existingStride)
                || requiredStride > existingStride)
            {
                streamStrides[channel.Stream] = requiredStride;
            }
        }

        return streamStrides;
    }

    private static Dictionary<int, int> BuildStreamBaseOffsets(
        IReadOnlyDictionary<int, int> streamStrides,
        int vertexCount,
        int bufferLength)
    {
        var baseOffsets = new Dictionary<int, int>();
        var runningOffset = 0;
        foreach (var stream in streamStrides.Keys.OrderBy(key => key))
        {
            baseOffsets[stream] = runningOffset;

            var stride = streamStrides[stream];
            var streamSize = checked(stride * vertexCount);
            runningOffset += streamSize;
            if (runningOffset >= bufferLength)
            {
                break;
            }
        }

        return baseOffsets;
    }

    private static List<float> DecodeFloatChannel(
        byte[] vertexDataBytes,
        int vertexCount,
        IReadOnlyDictionary<int, VertexChannelInfo> channels,
        IReadOnlyDictionary<int, int> streamStrides,
        IReadOnlyDictionary<int, int> streamBaseOffsets,
        int channelIndex,
        int expectedDimension)
    {
        if (!channels.TryGetValue(channelIndex, out var channelInfo)
            || channelInfo.Dimension < expectedDimension
            || !streamBaseOffsets.TryGetValue(channelInfo.Stream, out var streamBaseOffset))
        {
            return [];
        }

        if (!streamStrides.TryGetValue(channelInfo.Stream, out var stride))
        {
            return [];
        }

        if (stride <= 0)
        {
            return [];
        }

        var values = new List<float>(vertexCount * expectedDimension);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var baseOffset = streamBaseOffset + vertexIndex * stride + channelInfo.Offset;
            var componentSize = ResolveVertexFormatSize(channelInfo.Format);
            if (componentSize <= 0)
            {
                return [];
            }

            var endOffset = baseOffset + expectedDimension * componentSize;
            if (baseOffset < 0 || endOffset > vertexDataBytes.Length)
            {
                return [];
            }

            for (var component = 0; component < expectedDimension; component++)
            {
                var componentOffset = baseOffset + component * componentSize;
                if (!TryReadVertexComponentAsFloat(vertexDataBytes, componentOffset, channelInfo.Format, out var componentValue))
                {
                    return [];
                }

                values.Add(componentValue);
            }
        }

        return values;
    }

    private static bool TryReadVertexComponentAsFloat(byte[] data, int offset, int format, out float value)
    {
        value = default;
        switch (format)
        {
            case 0:
                if (offset + 4 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToSingle(data, offset);
                return true;
            case 1:
                if (offset + 2 > data.Length)
                {
                    return false;
                }

                var halfBits = BitConverter.ToUInt16(data, offset);
                value = (float)BitConverter.UInt16BitsToHalf(halfBits);
                return true;
            case 2:
                if (offset + 1 > data.Length)
                {
                    return false;
                }

                value = data[offset] / 255f;
                return true;
            case 3:
                if (offset + 1 > data.Length)
                {
                    return false;
                }

                value = Math.Clamp((sbyte)data[offset] / 127f, -1f, 1f);
                return true;
            case 4:
                if (offset + 2 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToUInt16(data, offset) / 65535f;
                return true;
            case 5:
                if (offset + 2 > data.Length)
                {
                    return false;
                }

                var signed16 = BitConverter.ToInt16(data, offset);
                value = Math.Clamp(signed16 / 32767f, -1f, 1f);
                return true;
            case 6:
                if (offset + 1 > data.Length)
                {
                    return false;
                }

                value = data[offset];
                return true;
            case 7:
                if (offset + 1 > data.Length)
                {
                    return false;
                }

                value = (sbyte)data[offset];
                return true;
            case 8:
                if (offset + 2 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToUInt16(data, offset);
                return true;
            case 9:
                if (offset + 2 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToInt16(data, offset);
                return true;
            case 10:
                if (offset + 4 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToUInt32(data, offset);
                return true;
            case 11:
                if (offset + 4 > data.Length)
                {
                    return false;
                }

                value = BitConverter.ToInt32(data, offset);
                return true;
            default:
                return false;
        }
    }

    private static int ResolveVertexFormatSize(int format)
    {
        return format switch
        {
            0 => 4,
            1 => 2,
            2 => 1,
            3 => 1,
            4 => 2,
            5 => 2,
            6 => 1,
            7 => 1,
            8 => 2,
            9 => 2,
            10 => 4,
            11 => 4,
            _ => 0
        };
    }

    private sealed record VertexChannelInfo(int Stream, int Offset, int Format, int Dimension);

    private static long ParsePathId(string pathId)
    {
        return long.TryParse(pathId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static UnityRenderObject? TryBuildRenderObject(string assetId, ObjectInfo objectInfo, SerializedFile serializedFile)
    {
        var objectId = $"{assetId}:obj:{objectInfo.m_PathID.ToString(CultureInfo.InvariantCulture)}";

        try
        {
            using var objectReader = new ObjectReader(serializedFile.reader, serializedFile, objectInfo);
            objectReader.Reset();

            if (objectReader.type == ClassIDType.Transform)
            {
                var transform = new Transform(objectReader);
                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "transform",
                    BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
                    ResolvePointerObjectId(assetId, transform.m_Father.m_FileID, transform.m_Father.m_PathID),
                    transform.m_Children
                        .Select(child => ResolvePointerObjectId(assetId, child.m_FileID, child.m_PathID))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    null,
                    [],
                    [transform.m_LocalPosition.X, transform.m_LocalPosition.Y, transform.m_LocalPosition.Z],
                    [transform.m_LocalRotation.X, transform.m_LocalRotation.Y, transform.m_LocalRotation.Z, transform.m_LocalRotation.W],
                    [transform.m_LocalScale.X, transform.m_LocalScale.Y, transform.m_LocalScale.Z]);
            }

            if (objectReader.type == ClassIDType.GameObject)
            {
                var gameObject = new GameObject(objectReader);
                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "gameobject",
                    string.IsNullOrWhiteSpace(gameObject.m_Name) ? BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID) : gameObject.m_Name,
                    null,
                    gameObject.m_Components
                        .Select(component => ResolvePointerObjectId(assetId, component.m_FileID, component.m_PathID))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    null,
                    [],
                    null,
                    null,
                    null);
            }

            if (objectReader.type == ClassIDType.MeshFilter)
            {
                var meshFilter = new MeshFilter(objectReader);
                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "meshfilter",
                    BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
                    ResolvePointerObjectId(assetId, meshFilter.m_GameObject.m_FileID, meshFilter.m_GameObject.m_PathID),
                    [],
                    ResolvePointerObjectId(assetId, meshFilter.m_Mesh.m_FileID, meshFilter.m_Mesh.m_PathID),
                    [],
                    null,
                    null,
                    null);
            }

            if (objectReader.type == ClassIDType.MeshRenderer)
            {
                var meshRenderer = new MeshRenderer(objectReader);
                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "meshrenderer",
                    BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
                    ResolvePointerObjectId(assetId, meshRenderer.m_GameObject.m_FileID, meshRenderer.m_GameObject.m_PathID),
                    [],
                    null,
                    meshRenderer.m_Materials
                        .Select(material => ResolvePointerObjectId(assetId, material.m_FileID, material.m_PathID))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    null,
                    null,
                    null);
            }

            if (objectReader.type == ClassIDType.SkinnedMeshRenderer)
            {
                var skinnedMeshRenderer = new SkinnedMeshRenderer(objectReader);
                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "skinnedmeshrenderer",
                    BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
                    ResolvePointerObjectId(assetId, skinnedMeshRenderer.m_GameObject.m_FileID, skinnedMeshRenderer.m_GameObject.m_PathID),
                    skinnedMeshRenderer.m_Bones
                        .Select(bone => ResolvePointerObjectId(assetId, bone.m_FileID, bone.m_PathID))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    ResolvePointerObjectId(assetId, skinnedMeshRenderer.m_Mesh.m_FileID, skinnedMeshRenderer.m_Mesh.m_PathID),
                    skinnedMeshRenderer.m_Materials
                        .Select(material => ResolvePointerObjectId(assetId, material.m_FileID, material.m_PathID))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    null,
                    null,
                    null);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ResolvePointerObjectId(string assetId, int fileId, long pathId)
    {
        if (pathId == 0)
        {
            return null;
        }

        if (fileId == 0)
        {
            return $"{assetId}:obj:{pathId.ToString(CultureInfo.InvariantCulture)}";
        }

        return $"external-file:{fileId.ToString(CultureInfo.InvariantCulture)}:obj:{pathId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static IReadOnlyList<UnityObjectPointer> ExtractOutboundObjectPointers(SerializedFile serializedFile, ObjectInfo objectInfo)
    {
        if (objectInfo.serializedType?.m_Type?.m_Nodes is not { Count: > 0 })
        {
            return [];
        }

        try
        {
            using var objectReader = new ObjectReader(serializedFile.reader, serializedFile, objectInfo);
            var typeData = TypeTreeHelper.ReadType(objectInfo.serializedType.m_Type, objectReader);
            var pointers = new List<UnityObjectPointer>();
            CollectObjectPointers(typeData, pointers);

            return pointers
                .Where(pointer => pointer.PathId != "0" && pointer.FileId >= 0)
                .Select(pointer => ResolveExternalObjectPointer(serializedFile, pointer))
                .Distinct()
                .Take(128)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void CollectObjectPointers(object? value, List<UnityObjectPointer> pointers)
    {
        switch (value)
        {
            case null:
                return;
            case OrderedDictionary dictionary:
                if (TryReadObjectPointer(dictionary, out var pointer))
                {
                    pointers.Add(pointer);
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    CollectObjectPointers(entry.Value, pointers);
                }

                return;
            case Array array:
                foreach (var item in array)
                {
                    CollectObjectPointers(item, pointers);
                }

                return;
            case IEnumerable enumerable when value is not string:
                foreach (var item in enumerable)
                {
                    CollectObjectPointers(item, pointers);
                }

                return;
            default:
                return;
        }
    }

    private static bool TryReadObjectPointer(OrderedDictionary dictionary, out UnityObjectPointer pointer)
    {
        pointer = default!;
        if (!TryGetDictionaryValue(dictionary, "m_FileID", out var fileIdValue)
            || !TryGetDictionaryValue(dictionary, "m_PathID", out var pathIdValue)
            || !TryConvertToInt(fileIdValue, out var fileId)
            || !TryConvertToLong(pathIdValue, out var pathId))
        {
            return false;
        }

        pointer = new UnityObjectPointer(fileId, pathId.ToString(CultureInfo.InvariantCulture), null);
        return true;
    }

    private static UnityObjectPointer ResolveExternalObjectPointer(SerializedFile serializedFile, UnityObjectPointer pointer)
    {
        if (pointer.FileId <= 0)
        {
            return pointer;
        }

        var externalIndex = pointer.FileId - 1;
        if (externalIndex < 0 || externalIndex >= serializedFile.m_Externals.Count)
        {
            return pointer;
        }

        var external = serializedFile.m_Externals[externalIndex];
        var externalAssetId = ResolveExternalAssetId(external);
        return pointer with { ExternalAssetId = externalAssetId };
    }

    private static string? ResolveExternalAssetId(FileIdentifier external)
    {
        if (external.guid != Guid.Empty)
        {
            return external.guid.ToString("N").ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(external.fileName))
        {
            return $"file:{external.fileName.ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(external.pathName))
        {
            return $"path:{external.pathName.ToLowerInvariant()}";
        }

        return null;
    }

    private static bool TryGetDictionaryValue(OrderedDictionary dictionary, string key, out object? value)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string currentKey && string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryConvertToInt(object? value, out int result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                result = (int)longValue;
                return true;
            case uint uintValue when uintValue <= int.MaxValue:
                result = (int)uintValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryConvertToLong(object? value, out long result)
    {
        switch (value)
        {
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                result = (long)ulongValue;
                return true;
            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryConvertToFloat(object? value, out float result)
    {
        switch (value)
        {
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue when doubleValue <= float.MaxValue && doubleValue >= float.MinValue:
                result = (float)doubleValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue <= float.MaxValue && longValue >= float.MinValue:
                result = longValue;
                return true;
            case string text when float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static string BuildObjectDisplayName(int classId, long pathId)
    {
        if (Enum.IsDefined(typeof(ClassIDType), classId))
        {
            return $"{(ClassIDType)classId}:{pathId}";
        }

        return $"Class{classId}:{pathId}";
    }

    private static ParsedSceneGraph BuildGraph(
        ContainerDescriptor container,
        IReadOnlyList<ParsedAssetRecord> assets,
        IReadOnlyList<UnityObjectRef> refs,
        IReadOnlyList<AttachmentHint> hints,
        IReadOnlyList<string> warnings)
    {
        var nodes = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
        var edges = new List<SceneEdge>();
        var refLinks = new List<RefLink>();
        var assetIdentityIndex = BuildAssetIdentityIndex(assets);
        var objectIdentityIndex = BuildObjectIdentityIndex(refs);

        var containerNodeId = BuildContainerNodeId(container.ContainerId);
        nodes[containerNodeId] = new SceneNode(containerNodeId, "container", container.DisplayName, null, null);

        foreach (var asset in assets)
        {
            var assetNodeId = BuildAssetNodeId(asset.AssetId);
            nodes[assetNodeId] = new SceneNode(assetNodeId, "asset", asset.FileName, asset.AssetId, asset.PackageGuid ?? asset.MetaGuid);

            edges.Add(new SceneEdge(
                BuildEdgeId(containerNodeId, assetNodeId, "contains"),
                containerNodeId,
                assetNodeId,
                "contains",
                "high"));

            foreach (var objectRef in refs.Where(item => item.AssetId == asset.AssetId))
            {
                var objectNodeId = BuildObjectNodeId(objectRef.ObjectId);
                nodes[objectNodeId] = new SceneNode(
                    objectNodeId,
                    BuildObjectKind(objectRef.ClassId),
                    objectRef.ObjectName,
                    objectRef.AssetId,
                    objectRef.PackageGuid);

                edges.Add(new SceneEdge(
                    BuildEdgeId(assetNodeId, objectNodeId, "describes"),
                    assetNodeId,
                    objectNodeId,
                    "describes",
                    "high"));

                if (objectRef.ClassId is int classId)
                {
                    var classNodeId = BuildClassNodeId(classId);
                    if (!nodes.ContainsKey(classNodeId))
                    {
                        nodes[classNodeId] = new SceneNode(
                            classNodeId,
                            "unity-class",
                            ResolveClassLabel(classId),
                            null,
                            null);
                    }

                    edges.Add(new SceneEdge(
                        BuildEdgeId(objectNodeId, classNodeId, "typed-as"),
                        objectNodeId,
                        classNodeId,
                        "typed-as",
                        "high"));
                }

                foreach (var outboundRef in objectRef.OutboundObjectRefs)
                {
                    var (targetNodeId, targetGuid, targetAssetId, targetObjectId, status) = ResolveObjectReference(
                        objectRef.AssetId,
                        outboundRef,
                        assetIdentityIndex,
                        objectIdentityIndex);

                    if (!nodes.ContainsKey(targetNodeId))
                    {
                        nodes[targetNodeId] = new SceneNode(
                            targetNodeId,
                            ResolveObjectReferenceKind(status),
                            ResolveObjectReferenceLabel(outboundRef, targetAssetId),
                            targetAssetId,
                            outboundRef.ExternalAssetId);
                    }

                    edges.Add(new SceneEdge(
                        BuildEdgeId(objectNodeId, targetNodeId, "object-ref"),
                        objectNodeId,
                        targetNodeId,
                        "object-ref",
                        status is "object-resolved" or "object-asset-resolved" ? "high" : "medium"));

                    refLinks.Add(new RefLink(
                        objectRef.AssetId,
                        objectRef.ContainerId,
                        targetGuid,
                        targetAssetId,
                        targetObjectId,
                        outboundRef.FileId.ToString(CultureInfo.InvariantCulture),
                        status));
                }
            }
        }

        var guidToAsset = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.MetaGuid))
            .GroupBy(asset => asset.MetaGuid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            var assetNodeId = BuildAssetNodeId(asset.AssetId);
            foreach (var referencedGuid in asset.ReferencedGuids)
            {
                if (guidToAsset.TryGetValue(referencedGuid, out var targetAsset))
                {
                    var targetNodeId = BuildAssetNodeId(targetAsset.AssetId);
                    edges.Add(new SceneEdge(
                        BuildEdgeId(assetNodeId, targetNodeId, "guid-ref"),
                        assetNodeId,
                        targetNodeId,
                        "guid-ref",
                        "high"));

                    refLinks.Add(new RefLink(asset.AssetId, asset.ContainerId, referencedGuid, targetAsset.AssetId, null, null, "resolved"));
                }
                else
                {
                    var unresolvedNodeId = BuildGuidNodeId(referencedGuid);
                    if (!nodes.ContainsKey(unresolvedNodeId))
                    {
                        nodes[unresolvedNodeId] = new SceneNode(unresolvedNodeId, "external-guid", referencedGuid, null, referencedGuid);
                    }

                    edges.Add(new SceneEdge(
                        BuildEdgeId(assetNodeId, unresolvedNodeId, "guid-ref"),
                        assetNodeId,
                        unresolvedNodeId,
                        "guid-ref",
                        "medium"));

                    refLinks.Add(new RefLink(asset.AssetId, asset.ContainerId, referencedGuid, null, null, null, "unresolved"));
                }
            }
        }

        foreach (var hint in hints)
        {
            var sourceNodeId = BuildAssetNodeId(hint.SourceAssetId);
            foreach (var externalGuid in hint.ExternalReferenceGuids)
            {
                var externalNodeId = BuildGuidNodeId(externalGuid);
                if (!nodes.ContainsKey(externalNodeId))
                {
                    nodes[externalNodeId] = new SceneNode(externalNodeId, "external-guid", externalGuid, null, externalGuid);
                }

                edges.Add(new SceneEdge(
                    BuildEdgeId(sourceNodeId, externalNodeId, "attach-hint"),
                    sourceNodeId,
                    externalNodeId,
                    "attach-hint",
                    "medium"));

                refLinks.Add(new RefLink(hint.SourceAssetId, container.ContainerId, externalGuid, null, null, null, "hint"));
            }
        }

        if (warnings.Count > 0)
        {
            var warningNodeId = $"warning:{container.ContainerId}";
            nodes[warningNodeId] = new SceneNode(warningNodeId, "warning", "parse-warnings", null, null);
            edges.Add(new SceneEdge(
                BuildEdgeId(containerNodeId, warningNodeId, "has-warning"),
                containerNodeId,
                warningNodeId,
                "has-warning",
                "high"));
        }

        return new ParsedSceneGraph(nodes.Values.ToArray(), edges, refLinks);
    }

    private static string BuildContainerNodeId(string containerId)
        => $"container:{containerId}";

    private static string BuildAssetNodeId(string assetId)
        => $"asset:{assetId}";

    private static string BuildObjectNodeId(string objectId)
        => $"object:{objectId}";

    private static string BuildClassNodeId(int classId)
        => $"class:{classId}";

    private static string BuildGuidNodeId(string guid)
        => $"guid:{guid.ToLowerInvariant()}";

    private static (string TargetNodeId, string TargetGuid, string? TargetAssetId, string? TargetObjectId, string Status) ResolveObjectReference(
        string sourceAssetId,
        UnityObjectPointer pointer,
        IReadOnlyDictionary<string, string> assetIdentityIndex,
        ISet<string> objectIdentityIndex)
    {
        var pathId = pointer.PathId;
        if (pointer.FileId == 0)
        {
            var localObjectId = $"{sourceAssetId}:obj:{pathId}";
            var status = objectIdentityIndex.Contains(BuildObjectIdentityKey(sourceAssetId, pathId))
                ? "object-resolved"
                : "object-unresolved";

            return (
                BuildObjectNodeId(localObjectId),
                localObjectId,
                sourceAssetId,
                localObjectId,
                status);
        }

        var resolvedTargetAssetId = ResolveTargetAssetId(pointer.ExternalAssetId, assetIdentityIndex);
        if (!string.IsNullOrWhiteSpace(resolvedTargetAssetId))
        {
            var resolvedObjectId = $"{resolvedTargetAssetId}:obj:{pathId}";
            var status = objectIdentityIndex.Contains(BuildObjectIdentityKey(resolvedTargetAssetId, pathId))
                ? "object-resolved"
                : "object-asset-resolved";

            return (
                BuildObjectNodeId(resolvedObjectId),
                resolvedObjectId,
                resolvedTargetAssetId,
                resolvedObjectId,
                status);
        }

        return (
            BuildExternalObjectReferenceNodeId(pointer.FileId, pathId, pointer.ExternalAssetId),
            BuildOutboundReferenceTargetKey(pointer),
            null,
            null,
            "object-external");
    }

    private static IReadOnlyDictionary<string, string> BuildAssetIdentityIndex(IReadOnlyList<ParsedAssetRecord> assets)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            AddAssetIdentity(index, asset.MetaGuid, asset.AssetId);
            AddAssetIdentity(index, asset.PackageGuid, asset.AssetId);
            AddAssetIdentity(index, $"guid:{asset.MetaGuid}", asset.AssetId);
            AddAssetIdentity(index, $"guid:{asset.PackageGuid}", asset.AssetId);
            AddAssetIdentity(index, $"file:{asset.FileName}", asset.AssetId);
            AddAssetIdentity(index, $"path:{NormalizePath(asset.Pathname)}", asset.AssetId);
        }

        return index;
    }

    private static ISet<string> BuildObjectIdentityIndex(IReadOnlyList<UnityObjectRef> refs)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in refs)
        {
            if (!string.IsNullOrWhiteSpace(reference.PathId))
            {
                set.Add(BuildObjectIdentityKey(reference.AssetId, reference.PathId));
            }
        }

        return set;
    }

    private static void AddAssetIdentity(IDictionary<string, string> index, string? identity, string assetId)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return;
        }

        var normalized = identity.Trim().ToLowerInvariant();
        if (!index.ContainsKey(normalized))
        {
            index[normalized] = assetId;
        }
    }

    private static string? ResolveTargetAssetId(string? externalAssetId, IReadOnlyDictionary<string, string> assetIdentityIndex)
    {
        if (string.IsNullOrWhiteSpace(externalAssetId))
        {
            return null;
        }

        var normalized = externalAssetId.Trim().ToLowerInvariant();
        if (assetIdentityIndex.TryGetValue(normalized, out var directAssetId))
        {
            return directAssetId;
        }

        if (!normalized.Contains(':'))
        {
            if (assetIdentityIndex.TryGetValue($"guid:{normalized}", out var guidAssetId))
            {
                return guidAssetId;
            }
        }

        if (normalized.StartsWith("path:", StringComparison.Ordinal))
        {
            var pathText = normalized[5..];
            var fileName = Path.GetFileName(pathText);
            if (!string.IsNullOrWhiteSpace(fileName)
                && assetIdentityIndex.TryGetValue($"file:{fileName}", out var fileAssetId))
            {
                return fileAssetId;
            }
        }

        return null;
    }

    private static string BuildObjectIdentityKey(string assetId, string pathId)
        => $"{assetId}|{pathId}";

    private static string ResolveObjectReferenceKind(string status)
    {
        return status switch
        {
            "object-resolved" => "object",
            "object-asset-resolved" => "object-unresolved",
            "object-unresolved" => "object-unresolved",
            _ => "object-external"
        };
    }

    private static string ResolveObjectReferenceLabel(UnityObjectPointer pointer, string? targetAssetId)
    {
        if (pointer.FileId == 0)
        {
            return $"PathId:{pointer.PathId}";
        }

        if (!string.IsNullOrWhiteSpace(targetAssetId))
        {
            return $"Resolved[{targetAssetId}]:{pointer.PathId}";
        }

        return $"External[{pointer.FileId}]:{pointer.ExternalAssetId ?? "unknown"}:{pointer.PathId}";
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').Trim().ToLowerInvariant();

    private static string ResolveObjectReferenceNodeId(string assetId, UnityObjectPointer pointer)
    {
        if (pointer.FileId == 0)
        {
            var localObjectId = $"{assetId}:obj:{pointer.PathId}";
            return BuildObjectNodeId(localObjectId);
        }

        return BuildExternalObjectReferenceNodeId(pointer.FileId, pointer.PathId, pointer.ExternalAssetId);
    }

    private static string BuildExternalObjectReferenceNodeId(int fileId, string pathId, string? externalAssetId)
    {
        var raw = $"{fileId}|{pathId}|{externalAssetId ?? "unknown"}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return $"objectref:{hash[..16].ToLowerInvariant()}";
    }

    private static string BuildOutboundReferenceTargetKey(UnityObjectPointer pointer)
    {
        if (pointer.FileId == 0)
        {
            return $"object:{pointer.FileId}:{pointer.PathId}";
        }

        var externalIdentity = pointer.ExternalAssetId
            ?? $"external-file:{pointer.FileId.ToString(CultureInfo.InvariantCulture)}";

        return $"{externalIdentity}:obj:{pointer.PathId}";
    }

    private static string BuildEdgeId(string fromNodeId, string toNodeId, string edgeKind)
    {
        var raw = $"{fromNodeId}|{toNodeId}|{edgeKind}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return $"edge:{hash[..16].ToLowerInvariant()}";
    }

    private static string BuildObjectKind(int? classId)
    {
        if (classId is null)
        {
            return "object";
        }

        return Enum.IsDefined(typeof(ClassIDType), classId.Value)
            ? $"object:{((ClassIDType)classId.Value).ToString().ToLowerInvariant()}"
            : "object";
    }

    private static string ResolveClassLabel(int classId)
    {
        return Enum.IsDefined(typeof(ClassIDType), classId)
            ? ((ClassIDType)classId).ToString()
            : $"Class{classId}";
    }

    private static string[] BuildCandidateBoneNames(string slotTag)
    {
        return slotTag switch
        {
            "head" => ["head", "Head", "mixamorig:Head", "Bip001 Head"],
            "neck" => ["neck", "Neck", "mixamorig:Neck", "Bip001 Neck"],
            "body" => ["spine", "Spine", "mixamorig:Spine", "Bip001 Spine"],
            "leftarm" => ["leftarm", "LeftArm", "mixamorig:LeftArm", "Bip001 L UpperArm"],
            "rightarm" => ["rightarm", "RightArm", "mixamorig:RightArm", "Bip001 R UpperArm"],
            "leftleg" => ["leftleg", "LeftLeg", "mixamorig:LeftLeg", "Bip001 L Thigh"],
            "rightleg" => ["rightleg", "RightLeg", "mixamorig:RightLeg", "Bip001 R Thigh"],
            _ => [slotTag]
        };
    }

    private static string[] BuildCandidateNodePaths(string slotTag)
        => [$"PlayerAvatar/{slotTag}", $"Avatar/{slotTag}", slotTag];

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string ReadUtf8(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd().Trim();
    }

    private sealed class UnityPackageItem
    {
        public string Guid { get; set; } = string.Empty;
        public string? Pathname { get; set; }
        public byte[]? AssetBytes { get; set; }
        public string? MetaText { get; set; }
    }
}
