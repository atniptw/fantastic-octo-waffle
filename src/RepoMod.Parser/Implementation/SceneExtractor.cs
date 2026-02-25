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

                refs.AddRange(BuildObjectRefsFromSerializedAsset(
                    assetId,
                    containerId,
                    packageGuid,
                    discovered.FileName,
                    packageItem?.AssetBytes,
                    warnings));

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

    private static IReadOnlyList<UnityObjectRef> BuildObjectRefsFromSerializedAsset(
        string assetId,
        string containerId,
        string? packageGuid,
        string fallbackName,
        byte[]? assetBytes,
        List<string> warnings)
    {
        if (assetBytes is not { Length: > 0 })
        {
            return [
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
            ];
        }

        try
        {
            using var stream = new MemoryStream(assetBytes, writable: false);
            using var fileReader = new FileReader(fallbackName, stream);
            if (fileReader.FileType != FileType.AssetsFile)
            {
                return [
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
                ];
            }

            var assetsManager = new AssetsManager();
            var serializedFile = new SerializedFile(fileReader, assetsManager);
            if (serializedFile.m_Objects.Count == 0)
            {
                return [
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
                ];
            }

            var objectRefs = new List<UnityObjectRef>(serializedFile.m_Objects.Count);
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
            }

            return objectRefs;
        }
        catch (Exception ex)
        {
            warnings.Add($"Object-level read failed for '{fallbackName}': {ex.Message}");
            return [
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
            ];
        }
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

        pointer = new UnityObjectPointer(fileId, pathId.ToString(CultureInfo.InvariantCulture));
        return true;
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
                    var targetNodeId = ResolveObjectReferenceNodeId(asset.AssetId, outboundRef);
                    if (!nodes.ContainsKey(targetNodeId))
                    {
                        nodes[targetNodeId] = new SceneNode(
                            targetNodeId,
                            outboundRef.FileId == 0 ? "object-unresolved" : "object-external",
                            outboundRef.FileId == 0
                                ? $"PathId:{outboundRef.PathId}"
                                : $"External[{outboundRef.FileId}]:{outboundRef.PathId}",
                            outboundRef.FileId == 0 ? asset.AssetId : null,
                            null);
                    }

                    edges.Add(new SceneEdge(
                        BuildEdgeId(objectNodeId, targetNodeId, "object-ref"),
                        objectNodeId,
                        targetNodeId,
                        "object-ref",
                        outboundRef.FileId == 0 ? "high" : "medium"));

                    refLinks.Add(new RefLink(
                        objectRef.AssetId,
                        objectRef.ContainerId,
                        $"object:{outboundRef.FileId}:{outboundRef.PathId}",
                        outboundRef.FileId == 0 ? objectRef.AssetId : null,
                        outboundRef.FileId == 0 ? $"{objectRef.AssetId}:obj:{outboundRef.PathId}" : null,
                        outboundRef.FileId.ToString(CultureInfo.InvariantCulture),
                        outboundRef.FileId == 0
                            ? (nodes.ContainsKey(BuildObjectNodeId($"{objectRef.AssetId}:obj:{outboundRef.PathId}")) ? "object-resolved" : "object-unresolved")
                            : "object-external"));
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

    private static string ResolveObjectReferenceNodeId(string assetId, UnityObjectPointer pointer)
    {
        if (pointer.FileId == 0)
        {
            var localObjectId = $"{assetId}:obj:{pointer.PathId}";
            return BuildObjectNodeId(localObjectId);
        }

        return $"objectref:file{pointer.FileId}:path{pointer.PathId}";
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
