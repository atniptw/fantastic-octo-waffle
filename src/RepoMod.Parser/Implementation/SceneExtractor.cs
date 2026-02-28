using System.Buffers.Binary;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AssetStudio;
using AssetStudio.CustomOptions;
using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public sealed class SceneExtractor(IArchiveScanner archiveScanner, IModParser modParser) : ISceneExtractor
{
    private static readonly Regex MetaGuidRegex = new(@"^guid:\s*([0-9a-fA-F]+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ReferenceGuidRegex = new(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
    private static readonly Regex GenericGuidRegex = new(@"\b[0-9a-fA-F]{32}\b", RegexOptions.Compiled);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> UnityPackageExtensions = new(StringComparer.Ordinal)
    {
        ".asset",
        ".prefab",
        ".hhh",
        ".mat",
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".dds",
        ".shader"
    };

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

        var packageItems = ReadUnityPackageItems(unityPackagePath);
        return ParseUnityPackageCore(unityPackagePath, packageItems, scanResult.Bundles, scanResult.Warnings);
    }

    /// <summary>
    /// Parse a Unity unitypackage from in-memory bytes (primary workflow).
    /// This is the preferred entry point for browser-safe parsing.
    /// </summary>
    /// <remarks>
    /// Flow: byte[] -> read tar.gz entries -> discover bundles via FileReader magic byte probing ->
    /// extract render primitives (meshes, materials, textures) and metadata (GUID refs, avatar candidates) ->
    /// preserve metadata graph even if renders absent (synthetic root refs for fallback).
    /// No temporary files are written.
    /// </remarks>
    public ParseSceneResult ParseUnityPackage(byte[] unityPackageBytes, string sourceName)
    {
        if (unityPackageBytes is not { Length: > 0 })
        {
            return ParseSceneResult.Failed("Unitypackage payload is empty.");
        }

        var packageItems = ReadUnityPackageItems(unityPackageBytes);
        var discovery = DiscoverUnityPackageBundles(packageItems);
        return ParseUnityPackageCore(sourceName, packageItems, discovery.Bundles, discovery.Warnings);
    }

    private ParseSceneResult ParseUnityPackageCore(
        string sourceName,
        IReadOnlyList<UnityPackageItem> packageItems,
        IReadOnlyList<DiscoveredBundle> discoveredBundles,
        IReadOnlyList<string> initialWarnings)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return ParseSceneResult.Failed("Unitypackage source name is required.");
        }

        try
        {
            var containerId = BuildContainerId("unitypackage", sourceName);
            var container = new ContainerDescriptor(containerId, sourceName, "unitypackage", Path.GetFileName(sourceName));

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
            var warnings = new List<string>(initialWarnings);

            foreach (var discovered in discoveredBundles)
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
                    warnings,
                    includeFallbackRoot: true);

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

            var resolvedRenderObjects = ResolveGuidMeshPointers(renderObjects, assets, renderMeshes, warnings);
            var renderPrimitives = BuildRenderPrimitives(resolvedRenderObjects, renderMeshes);

            var scene = new ParsedModScene(
                BuildSceneId(containerId),
                container,
                assets,
                refs,
                resolvedRenderObjects,
                renderPrimitives,
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

        try
        {
            var bundleBytes = File.ReadAllBytes(bundlePath);
            return ParseCosmeticBundle(bundleBytes, bundlePath);
        }
        catch (Exception ex)
        {
            return ParseSceneResult.Failed($"Failed to read cosmetic bundle bytes: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse a cosmetic bundle (e.g., .hhh file) from in-memory bytes (primary workflow).
    /// Enforces fail-fast validation: render primitives must be non-empty or parse fails.
    /// </summary>
    /// <remarks>
    /// Flow: byte[] -> probe format via FileReader -> load SerializedFile or BundleFile in-memory ->
    /// extract render primitives (meshes, materials, textures) -> FAIL if no primitives found.
    /// Unlike unitypackage, cosmetic bundles do not provide synthetic metadata fallback (strict safety).
    /// No temporary files are written.
    /// </remarks>
    public ParseSceneResult ParseCosmeticBundle(byte[] bundleBytes, string sourceName)
    {
        if (bundleBytes is not { Length: > 0 })
        {
            return ParseSceneResult.Failed("Bundle payload is empty.");
        }

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return ParseSceneResult.Failed("Bundle source name is required.");
        }

        var fileName = Path.GetFileName(sourceName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var containerId = BuildContainerId("hhh", sourceName);
        var container = new ContainerDescriptor(containerId, sourceName, "hhh", fileName);
        var slotTag = modParser.ExtractMetadata(fileName).SlotTag;
        var assetId = BuildAssetId(containerId, null, fileName);
        var externalReferenceGuids = ExtractExternalGuids(bundleBytes);

        var assets = new[]
        {
            new ParsedAssetRecord(
                assetId,
                containerId,
                sourceName,
                fileName,
                extension,
                "cosmetic-bundle",
                bundleBytes.LongLength,
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
                fileName,
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

        var extracted = BuildObjectRefsFromSerializedAsset(
            assetId,
            containerId,
            null,
            fileName,
            bundleBytes,
            warnings,
            includeFallbackRoot: false);
        var objectRefs = refs.Concat(extracted.ObjectRefs).ToArray();
        var renderPrimitives = BuildRenderPrimitives(extracted.RenderObjects, extracted.RenderMeshes);

        if (renderPrimitives.Count == 0)
        {
            return ParseSceneResult.Failed("Cosmetic bundle did not produce render primitives required for GLB conversion.");
        }

        var scene = new ParsedModScene(
            BuildSceneId(containerId),
            container,
            assets,
            objectRefs,
            extracted.RenderObjects,
            renderPrimitives,
            extracted.RenderMeshes,
            extracted.RenderMaterials,
            extracted.RenderTextures,
            [hint],
            [],
            BuildGraph(container, assets, objectRefs, [hint], warnings),
            warnings);

        return ParseSceneResult.Succeeded(scene);
    }

    private static (
        IReadOnlyList<UnityObjectRef> ObjectRefs,
        IReadOnlyList<UnityRenderObject> RenderObjects,
        IReadOnlyList<UnityRenderMesh> RenderMeshes,
        IReadOnlyList<UnityRenderMaterial> RenderMaterials,
        IReadOnlyList<UnityRenderTexture> RenderTextures) BuildObjectRefsFromBundle(
        string bundlePath,
        string assetId,
        string containerId,
        List<string> warnings)
    {
        var objectRefs = new List<UnityObjectRef>();
        var renderObjects = new List<UnityRenderObject>();
        var renderMeshes = new List<UnityRenderMesh>();
        var renderMaterials = new List<UnityRenderMaterial>();
        var renderTextures = new List<UnityRenderTexture>();

        try
        {
            var assetsManager = new AssetsManager();
            assetsManager.LoadFilesAndFolders(bundlePath);

            foreach (var serializedFile in assetsManager.AssetsFileList)
            {
                AppendSerializedFileObjects(serializedFile, assetId, containerId, null, objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Bundle object-level read failed for '{Path.GetFileName(bundlePath)}': {ex.Message}");
        }

        return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
    }

    private static void AppendSerializedFileObjects(
        SerializedFile serializedFile,
        string assetId,
        string containerId,
        string? packageGuid,
        List<UnityObjectRef> objectRefs,
        List<UnityRenderObject> renderObjects,
        List<UnityRenderMesh> renderMeshes,
        List<UnityRenderMaterial> renderMaterials,
        List<UnityRenderTexture> renderTextures)
    {
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
    }

    private static (IReadOnlyList<DiscoveredBundle> Bundles, IReadOnlyList<string> Warnings) DiscoverUnityPackageBundles(
        IReadOnlyList<UnityPackageItem> packageItems)
    {
        var warnings = new List<string>();
        var bundles = new List<DiscoveredBundle>();

        foreach (var item in packageItems)
        {
            if (string.IsNullOrWhiteSpace(item.Pathname) || item.AssetBytes is null || item.AssetBytes.Length == 0)
            {
                continue;
            }

            try
            {
                var fileName = Path.GetFileName(item.Pathname);
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!UnityPackageExtensions.Contains(extension))
                {
                    continue;
                }

                using var assetStream = new MemoryStream(item.AssetBytes, writable: false);
                using var fileReader = new FileReader(item.Pathname, assetStream);

                if (fileReader.FileType is FileType.GZipFile or FileType.BrotliFile or FileType.ZipFile)
                {
                    warnings.Add($"Skipping compressed nested asset '{item.Pathname}' detected as {fileReader.FileType}.");
                    continue;
                }

                bundles.Add(new DiscoveredBundle(item.Pathname, fileName, item.AssetBytes.LongLength, extension));
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to probe unitypackage asset '{item.Pathname}': {ex.Message}");
            }
        }

        return (bundles, warnings);
    }

    private static List<UnityPackageItem> ReadUnityPackageItems(string unityPackagePath)
    {
        using var packageStream = File.OpenRead(unityPackagePath);
        return ReadUnityPackageItems(packageStream);
    }

    private static List<UnityPackageItem> ReadUnityPackageItems(byte[] unityPackageBytes)
    {
        using var packageStream = new MemoryStream(unityPackageBytes, writable: false);
        return ReadUnityPackageItems(packageStream);
    }

    private static List<UnityPackageItem> ReadUnityPackageItems(Stream packageStream)
    {
        var itemsByGuid = new Dictionary<string, UnityPackageItem>(StringComparer.Ordinal);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress, leaveOpen: true);
        foreach (var entry in UnityPackageTarReader.ReadEntries(gzipStream))
        {
            if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Name))
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
                    using (var stream = new MemoryStream(entry.Data, writable: false))
                    {
                        item.Pathname = ReadUtf8(stream);
                    }
                    break;
                case "asset":
                    item.AssetBytes = entry.Data;
                    break;
                case "asset.meta":
                    using (var stream = new MemoryStream(entry.Data, writable: false))
                    {
                        item.MetaText = ReadUtf8(stream);
                    }
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
        string sourceIdentity;
        try
        {
            sourceIdentity = File.Exists(sourcePath) ? Path.GetFullPath(sourcePath) : sourcePath;
        }
        catch
        {
            sourceIdentity = sourcePath;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceType}:{sourceIdentity}")));
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

    private static UnityObjectRef CreateFallbackObjectRef(string assetId, string containerId, string? packageGuid, string displayName)
        => new(
            BuildObjectId(assetId, packageGuid),
            assetId,
            containerId,
            packageGuid,
            null,
            null,
            null,
            displayName,
            []);

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
        List<string> warnings,
        bool includeFallbackRoot)
    {
        if (assetBytes is not { Length: > 0 })
        {
            warnings.Add($"Object-level read skipped for '{fallbackName}': asset payload is empty.");
            return includeFallbackRoot
                ? ([CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName)], [], [], [], [])
                : ([], [], [], [], []);
        }

        try
        {
            using var stream = new MemoryStream(assetBytes, writable: false);
            using var fileReader = new FileReader(fallbackName, stream);
            if (fileReader.FileType == FileType.BundleFile)
            {
                return BuildObjectRefsFromBundleBytes(assetBytes, fallbackName, assetId, containerId, warnings);
            }

            if (fileReader.FileType != FileType.AssetsFile)
            {
                if (TryBuildRenderMeshFromYaml(assetBytes, assetId, containerId, packageGuid, fallbackName, warnings, out var yamlMesh, out var yamlObjectRef))
                {
                    var yamlObjectRefs = new List<UnityObjectRef>();
                    if (includeFallbackRoot)
                    {
                        yamlObjectRefs.Add(CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName));
                    }

                    if (yamlObjectRef is not null)
                    {
                        yamlObjectRefs.Add(yamlObjectRef);
                    }

                    return (yamlObjectRefs, [], [yamlMesh], [], []);
                }

                if (TryBuildRenderObjectsFromPrefabYaml(assetBytes, assetId, containerId, packageGuid, fallbackName, out var prefabRenderObjects, out var prefabObjectRefs))
                {
                    var mergedObjectRefs = new List<UnityObjectRef>();
                    if (includeFallbackRoot)
                    {
                        mergedObjectRefs.Add(CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName));
                    }

                    mergedObjectRefs.AddRange(prefabObjectRefs);
                    return (mergedObjectRefs, prefabRenderObjects, [], [], []);
                }

                warnings.Add($"Object-level read skipped for '{fallbackName}': unsupported file type {fileReader.FileType}.");
                return includeFallbackRoot
                    ? ([CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName)], [], [], [], [])
                    : ([], [], [], [], []);
            }

            var assetsManager = new AssetsManager();
            var serializedFile = new SerializedFile(fileReader, assetsManager);
            if (serializedFile.m_Objects.Count == 0)
            {
                warnings.Add($"Object-level read returned no Unity objects for '{fallbackName}'.");
                return includeFallbackRoot
                    ? ([CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName)], [], [], [], [])
                    : ([], [], [], [], []);
            }

            var objectRefs = new List<UnityObjectRef>(serializedFile.m_Objects.Count);
            var renderObjects = new List<UnityRenderObject>();
            var renderMeshes = new List<UnityRenderMesh>();
            var renderMaterials = new List<UnityRenderMaterial>();
            var renderTextures = new List<UnityRenderTexture>();
            AppendSerializedFileObjects(serializedFile, assetId, containerId, packageGuid, objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);

            return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
        }
        catch (Exception ex)
        {
            warnings.Add($"Object-level read failed for '{fallbackName}': {ex.Message}");
            return includeFallbackRoot
                ? ([CreateFallbackObjectRef(assetId, containerId, packageGuid, fallbackName)], [], [], [], [])
                : ([], [], [], [], []);
        }
    }

    private static IReadOnlyList<UnityRenderObject> ResolveGuidMeshPointers(
        IReadOnlyList<UnityRenderObject> renderObjects,
        IReadOnlyList<ParsedAssetRecord> assets,
        IReadOnlyList<UnityRenderMesh> renderMeshes,
        ICollection<string> warnings)
    {
        if (renderObjects.Count == 0 || renderMeshes.Count == 0)
        {
            return renderObjects;
        }

        var meshObjectIdByAssetId = renderMeshes
            .GroupBy(mesh => mesh.AssetId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ObjectId, StringComparer.Ordinal);
        var meshObjectIdByAssetAndFileId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mesh in renderMeshes)
        {
            var fileId = TryExtractFileIdFromObjectId(mesh.ObjectId);
            if (!string.IsNullOrWhiteSpace(fileId))
            {
                meshObjectIdByAssetAndFileId[$"{mesh.AssetId}|{fileId}"] = mesh.ObjectId;
            }
        }

        var assetIdByGuid = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.PackageGuid))
            .GroupBy(asset => asset.PackageGuid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().AssetId, StringComparer.OrdinalIgnoreCase);

        var resolved = new List<UnityRenderObject>(renderObjects.Count);
        foreach (var renderObject in renderObjects)
        {
            var resolvedMeshObjectId = ResolveGuidReference(renderObject.MeshObjectId, assetIdByGuid, meshObjectIdByAssetId, meshObjectIdByAssetAndFileId, warnings);
            var resolvedMaterialObjectIds = renderObject.MaterialObjectIds
                .Select(materialObjectId => ResolveGuidReference(materialObjectId, assetIdByGuid, meshObjectIdByAssetId, meshObjectIdByAssetAndFileId, warnings) ?? materialObjectId)
                .ToArray();
            var resolvedAssignments = BuildMaterialAssignments(resolvedMaterialObjectIds);

            resolved.Add(renderObject with
            {
                MeshObjectId = resolvedMeshObjectId,
                MaterialObjectIds = resolvedMaterialObjectIds,
                MaterialAssignments = resolvedAssignments
            });
        }

        return resolved;
    }

    private static string? ResolveGuidReference(
        string? pointer,
        IReadOnlyDictionary<string, string> assetIdByGuid,
        IReadOnlyDictionary<string, string> meshObjectIdByAssetId,
        IReadOnlyDictionary<string, string> meshObjectIdByAssetAndFileId,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(pointer) || !pointer.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
        {
            return pointer;
        }

        var guid = pointer[5..].Trim();
        string? meshFileId = null;
        var separatorIndex = guid.IndexOf("#file:", StringComparison.OrdinalIgnoreCase);
        if (separatorIndex >= 0)
        {
            meshFileId = guid[(separatorIndex + 6)..].Trim();
            guid = guid[..separatorIndex].Trim();
        }

        if (guid.Length == 0)
        {
            return null;
        }

        if (!assetIdByGuid.TryGetValue(guid, out var assetId))
        {
            warnings.Add($"Unable to resolve GUID reference '{guid}' to a parsed asset.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(meshFileId)
            && meshObjectIdByAssetAndFileId.TryGetValue($"{assetId}|{meshFileId}", out var meshObjectIdByFile))
        {
            return meshObjectIdByFile;
        }

        if (!meshObjectIdByAssetId.TryGetValue(assetId, out var meshObjectId))
        {
            warnings.Add($"Unable to resolve GUID reference '{guid}' because parsed asset '{assetId}' has no mesh object.");
            return null;
        }

        return meshObjectId;
    }

    private static string? TryExtractFileIdFromObjectId(string objectId)
    {
        var marker = ":obj:";
        var markerIndex = objectId.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var suffix = objectId[(markerIndex + marker.Length)..];
        return suffix.All(char.IsDigit) ? suffix : null;
    }

    private static bool TryBuildRenderObjectsFromPrefabYaml(
        byte[] assetBytes,
        string assetId,
        string containerId,
        string? packageGuid,
        string fallbackName,
        out IReadOnlyList<UnityRenderObject> renderObjects,
        out IReadOnlyList<UnityObjectRef> objectRefs)
    {
        renderObjects = [];
        objectRefs = [];

        string text;
        try
        {
            text = Encoding.UTF8.GetString(assetBytes);
        }
        catch
        {
            return false;
        }

        if (!text.StartsWith("%YAML", StringComparison.Ordinal)
            || !text.Contains("\nGameObject:", StringComparison.Ordinal)
            || !text.Contains("\nTransform:", StringComparison.Ordinal))
        {
            return false;
        }

        var sections = ParseYamlObjectSections(text);
        if (sections.Count == 0)
        {
            return false;
        }

        var gameObjectNames = new Dictionary<long, string>();
        var transformByGameObjectId = new Dictionary<long, YamlTransformInfo>();
        var gameObjectIdByTransformId = new Dictionary<long, long>();
        var meshFilterByGameObjectId = new Dictionary<long, string?>();
        var rendererByGameObjectId = new Dictionary<long, YamlRendererInfo>();

        foreach (var section in sections)
        {
            switch (section.ClassId)
            {
                case 1:
                {
                    var name = TryReadYamlPropertyValue(section.Content, "m_Name") ?? $"GameObject_{section.FileId.ToString(CultureInfo.InvariantCulture)}";
                    gameObjectNames[section.FileId] = name;
                    break;
                }
                case 4:
                {
                    var gameObjectId = ParseYamlPointerFileId(section.Content, "m_GameObject");
                    if (gameObjectId <= 0)
                    {
                        break;
                    }

                    var parentTransformId = ParseYamlPointerFileId(section.Content, "m_Father");
                    var childTransformIds = ParseYamlPointerListFileIds(section.Content, "m_Children");
                    var localPosition = ParseYamlVector3(section.Content, "m_LocalPosition");
                    var localRotation = ParseYamlVector4(section.Content, "m_LocalRotation");
                    var localScale = ParseYamlVector3(section.Content, "m_LocalScale");

                    transformByGameObjectId[gameObjectId] = new YamlTransformInfo(section.FileId, parentTransformId, childTransformIds, localPosition, localRotation, localScale);
                    gameObjectIdByTransformId[section.FileId] = gameObjectId;
                    break;
                }
                case 33:
                {
                    var gameObjectId = ParseYamlPointerFileId(section.Content, "m_GameObject");
                    if (gameObjectId <= 0)
                    {
                        break;
                    }

                    var meshPointer = ParseYamlMeshPointer(section.Content);
                    meshFilterByGameObjectId[gameObjectId] = meshPointer;
                    break;
                }
                case 23:
                case 137:
                {
                    var gameObjectId = ParseYamlPointerFileId(section.Content, "m_GameObject");
                    if (gameObjectId <= 0)
                    {
                        break;
                    }

                    var materialPointers = ParseYamlMaterialPointers(section.Content);
                    var kind = section.ClassId == 137 ? "skinnedmeshrenderer" : "meshrenderer";
                    rendererByGameObjectId[gameObjectId] = new YamlRendererInfo(section.FileId, section.ClassId, kind, materialPointers);
                    break;
                }
            }
        }

        var parsedRenderObjects = new List<UnityRenderObject>();
        var parsedObjectRefs = new List<UnityObjectRef>();
        var transformObjectIdByGameObjectId = new Dictionary<long, string>();

        foreach (var (gameObjectId, transformInfo) in transformByGameObjectId)
        {
            var transformObjectId = BuildYamlObjectIdForFile(assetId, transformInfo.TransformFileId);
            transformObjectIdByGameObjectId[gameObjectId] = transformObjectId;
            var parentObjectId = transformInfo.ParentTransformFileId > 0
                ? BuildYamlObjectIdForFile(assetId, transformInfo.ParentTransformFileId)
                : null;
            var childObjectIds = transformInfo.ChildTransformFileIds
                .Select(childId => BuildYamlObjectIdForFile(assetId, childId))
                .ToArray();

            parsedRenderObjects.Add(new UnityRenderObject(
                transformObjectId,
                assetId,
                4,
                "transform",
                gameObjectNames.TryGetValue(gameObjectId, out var transformName)
                    ? transformName
                    : $"Transform_{transformInfo.TransformFileId.ToString(CultureInfo.InvariantCulture)}",
                parentObjectId,
                childObjectIds,
                null,
                [],
                [],
                transformInfo.LocalPosition,
                transformInfo.LocalRotation,
                transformInfo.LocalScale));

            parsedObjectRefs.Add(new UnityObjectRef(
                transformObjectId,
                assetId,
                containerId,
                packageGuid,
                null,
                transformInfo.TransformFileId.ToString(CultureInfo.InvariantCulture),
                4,
                gameObjectNames.TryGetValue(gameObjectId, out var objectName)
                    ? objectName
                    : fallbackName,
                []));
        }

        foreach (var (gameObjectId, name) in gameObjectNames)
        {
            var gameObjectObjectId = BuildYamlObjectIdForFile(assetId, gameObjectId);
            var transformInfo = transformByGameObjectId.TryGetValue(gameObjectId, out var info) ? info : null;
            var parentGameObjectId = transformInfo is not null
                && transformInfo.ParentTransformFileId > 0
                && gameObjectIdByTransformId.TryGetValue(transformInfo.ParentTransformFileId, out var parentGameObject)
                    ? parentGameObject
                    : 0;

            var childGameObjectIds = transformInfo?.ChildTransformFileIds
                .Select(childTransformId => gameObjectIdByTransformId.TryGetValue(childTransformId, out var childGameObject)
                    ? BuildYamlObjectIdForFile(assetId, childGameObject)
                    : null)
                .Where(childId => !string.IsNullOrWhiteSpace(childId))
                .Cast<string>()
                .ToArray() ?? [];

            parsedRenderObjects.Add(new UnityRenderObject(
                gameObjectObjectId,
                assetId,
                1,
                "gameobject",
                name,
                parentGameObjectId > 0 ? BuildYamlObjectIdForFile(assetId, parentGameObjectId) : null,
                childGameObjectIds,
                null,
                [],
                [],
                null,
                null,
                null));

            parsedObjectRefs.Add(new UnityObjectRef(
                gameObjectObjectId,
                assetId,
                containerId,
                packageGuid,
                null,
                gameObjectId.ToString(CultureInfo.InvariantCulture),
                1,
                name,
                []));

            if (meshFilterByGameObjectId.TryGetValue(gameObjectId, out var meshPointer))
            {
                var meshFilterObjectId = BuildYamlObjectIdForSuffix(assetId, $"meshfilter:{gameObjectId.ToString(CultureInfo.InvariantCulture)}");
                var meshFilterParentId = transformObjectIdByGameObjectId.TryGetValue(gameObjectId, out var transformObjectId)
                    ? transformObjectId
                    : gameObjectObjectId;
                parsedRenderObjects.Add(new UnityRenderObject(
                    meshFilterObjectId,
                    assetId,
                    33,
                    "meshfilter",
                    $"{name}_MeshFilter",
                    meshFilterParentId,
                    [],
                    meshPointer,
                    [],
                    [],
                    null,
                    null,
                    null));

                parsedObjectRefs.Add(new UnityObjectRef(
                    meshFilterObjectId,
                    assetId,
                    containerId,
                    packageGuid,
                    null,
                    gameObjectId.ToString(CultureInfo.InvariantCulture),
                    33,
                    $"{name}_MeshFilter",
                    []));
            }

            if (rendererByGameObjectId.TryGetValue(gameObjectId, out var rendererInfo))
            {
                var rendererObjectId = BuildYamlObjectIdForSuffix(assetId, $"renderer:{gameObjectId.ToString(CultureInfo.InvariantCulture)}");
                var rendererParentId = transformObjectIdByGameObjectId.TryGetValue(gameObjectId, out var transformObjectId)
                    ? transformObjectId
                    : gameObjectObjectId;
                parsedRenderObjects.Add(new UnityRenderObject(
                    rendererObjectId,
                    assetId,
                    rendererInfo.ClassId,
                    rendererInfo.Kind,
                    $"{name}_Renderer",
                    rendererParentId,
                    [],
                    null,
                    rendererInfo.MaterialPointers,
                    BuildMaterialAssignments(rendererInfo.MaterialPointers),
                    null,
                    null,
                    null));

                parsedObjectRefs.Add(new UnityObjectRef(
                    rendererObjectId,
                    assetId,
                    containerId,
                    packageGuid,
                    null,
                    rendererInfo.FileId.ToString(CultureInfo.InvariantCulture),
                    rendererInfo.ClassId,
                    $"{name}_Renderer",
                    []));
            }
        }

        if (parsedRenderObjects.Count == 0)
        {
            return false;
        }

        renderObjects = parsedRenderObjects;
        objectRefs = parsedObjectRefs;
        return true;
    }

    private static IReadOnlyList<YamlObjectSection> ParseYamlObjectSections(string text)
    {
        var matches = Regex.Matches(text, "---\\s*!u!(\\d+)\\s*&(-?\\d+)");
        if (matches.Count == 0)
        {
            return [];
        }

        var sections = new List<YamlObjectSection>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var start = match.Index + match.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classId)
                || !long.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId))
            {
                continue;
            }

            sections.Add(new YamlObjectSection(classId, fileId, text[start..end]));
        }

        return sections;
    }

    private static long ParseYamlPointerFileId(string content, string fieldName)
    {
        var match = Regex.Match(content, $"{Regex.Escape(fieldName)}\\s*:\\s*\\{{\\s*fileID\\s*:\\s*(-?\\d+)");
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId)
            ? fileId
            : 0;
    }

    private static string? ParseYamlMeshPointer(string content)
    {
        var guidMatch = Regex.Match(content, "m_Mesh\\s*:\\s*\\{[^}]*fileID\\s*:\\s*(-?\\d+)[^}]*guid\\s*:\\s*([0-9a-fA-F]{32})[^}]*\\}");
        if (guidMatch.Success)
        {
            var fileIdText = guidMatch.Groups[1].Value.Trim();
            var guid = guidMatch.Groups[2].Value;
            return $"guid:{guid}#file:{fileIdText}";
        }

        var localMatch = Regex.Match(content, "m_Mesh\\s*:\\s*\\{\\s*fileID\\s*:\\s*(-?\\d+)");
        if (localMatch.Success && long.TryParse(localMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId) && fileId > 0)
        {
            return $"file:{fileId.ToString(CultureInfo.InvariantCulture)}";
        }

        return null;
    }

    private static IReadOnlyList<string> ParseYamlMaterialPointers(string content)
    {
        var sectionMatch = Regex.Match(content, "m_Materials\\s*:\\s*(?<body>(?:\\r?\\n\\s*-\\s*\\{[^}]*\\})+)");
        if (!sectionMatch.Success)
        {
            return [];
        }

        var materialPointers = new List<string>();
        var entries = Regex.Matches(sectionMatch.Groups["body"].Value, "\\{[^}]*\\}");
        foreach (Match entry in entries)
        {
            var guidMatch = Regex.Match(entry.Value, "guid\\s*:\\s*([0-9a-fA-F]{32})");
            if (guidMatch.Success)
            {
                materialPointers.Add($"guid:{guidMatch.Groups[1].Value}");
                continue;
            }

            var fileIdMatch = Regex.Match(entry.Value, "fileID\\s*:\\s*(-?\\d+)");
            if (fileIdMatch.Success)
            {
                materialPointers.Add(fileIdMatch.Groups[1].Value);
            }
        }

        return materialPointers;
    }

    private static IReadOnlyList<long> ParseYamlPointerListFileIds(string content, string fieldName)
    {
        var sectionMatch = Regex.Match(content, $"{Regex.Escape(fieldName)}\\s*:\\s*(?<body>(?:\\r?\\n\\s*-\\s*\\{{[^}}]*\\}})+)");
        if (!sectionMatch.Success)
        {
            return [];
        }

        var values = new List<long>();
        var pointerMatches = Regex.Matches(sectionMatch.Groups["body"].Value, "fileID\\s*:\\s*(-?\\d+)");
        foreach (Match pointerMatch in pointerMatches)
        {
            if (long.TryParse(pointerMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId))
            {
                values.Add(fileId);
            }
        }

        return values;
    }

    private static string? TryReadYamlPropertyValue(string content, string fieldName)
    {
        var match = Regex.Match(content, $"^\\s*{Regex.Escape(fieldName)}\\s*:\\s*(.+)$", RegexOptions.Multiline);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim();
    }

    private static IReadOnlyList<float>? ParseYamlVector3(string content, string fieldName)
    {
        var match = Regex.Match(content, $"{Regex.Escape(fieldName)}\\s*:\\s*\\{{\\s*x\\s*:\\s*([^,]+),\\s*y\\s*:\\s*([^,]+),\\s*z\\s*:\\s*([^}}]+)\\}}");
        if (!match.Success)
        {
            return null;
        }

        if (!float.TryParse(match.Groups[1].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(match.Groups[2].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(match.Groups[3].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return null;
        }

        return [x, y, z];
    }

    private static IReadOnlyList<float>? ParseYamlVector4(string content, string fieldName)
    {
        var match = Regex.Match(content, $"{Regex.Escape(fieldName)}\\s*:\\s*\\{{\\s*x\\s*:\\s*([^,]+),\\s*y\\s*:\\s*([^,]+),\\s*z\\s*:\\s*([^,]+),\\s*w\\s*:\\s*([^}}]+)\\}}");
        if (!match.Success)
        {
            return null;
        }

        if (!float.TryParse(match.Groups[1].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(match.Groups[2].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(match.Groups[3].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
            || !float.TryParse(match.Groups[4].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
        {
            return null;
        }

        return [x, y, z, w];
    }

    private static string BuildYamlObjectIdForFile(string assetId, long fileId)
        => $"{assetId}:obj:{fileId.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildYamlObjectIdForSuffix(string assetId, string suffix)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(suffix)));
        return $"{assetId}:obj:yaml:{hash[..12].ToLowerInvariant()}";
    }

    private static bool TryBuildRenderMeshFromYaml(
        byte[] assetBytes,
        string assetId,
        string containerId,
        string? packageGuid,
        string fallbackName,
        List<string> warnings,
        out UnityRenderMesh yamlMesh,
        out UnityObjectRef? yamlObjectRef)
    {
        yamlMesh = null!;
        yamlObjectRef = null;

        string text;
        try
        {
            text = Encoding.UTF8.GetString(assetBytes);
        }
        catch (Exception ex)
        {
            warnings.Add($"YAML decode failed for '{fallbackName}': {ex.Message}");
            return false;
        }

        if (!text.StartsWith("%YAML", StringComparison.Ordinal)
            || !text.Contains("\nMesh:", StringComparison.Ordinal))
        {
            return false;
        }

        var lines = text.Split('\n');
        var meshName = TryReadYamlScalar(lines, "m_Name") ?? fallbackName;
        var vertexCount = TryReadYamlInt(lines, "m_VertexCount");
        var indexFormat = TryReadYamlInt(lines, "m_IndexFormat");
        var indexHex = ReadYamlHexBlock(lines, "m_IndexBuffer");
        var vertexHex = ReadYamlHexBlock(lines, "_typelessdata");
        var dataSize = TryReadYamlInt(lines, "m_DataSize");
        var subMeshes = ReadYamlSubMeshes(lines);
        var channels = ReadYamlChannels(lines);
        var meshFileId = TryReadYamlMeshFileId(text);

        if (vertexCount is null || string.IsNullOrWhiteSpace(vertexHex))
        {
            warnings.Add($"YAML mesh data missing for '{fallbackName}'.");
            return false;
        }

        var vertexBytes = ParseHexBytes(vertexHex);
        if (dataSize is > 0 && vertexBytes.Length < dataSize)
        {
            warnings.Add($"YAML vertex data truncated for '{fallbackName}'.");
        }

        var positions = ReadYamlPositions(vertexBytes, vertexCount.Value, dataSize, channels);
        var colors = ReadYamlColors(vertexBytes, vertexCount.Value, dataSize, channels);
        var indexValues = ReadYamlIndexValues(indexHex, indexFormat);
        var objectId = meshFileId is > 0
            ? BuildYamlObjectIdForFile(assetId, meshFileId.Value)
            : BuildYamlObjectId(assetId, meshName);

        yamlMesh = new UnityRenderMesh(
            objectId,
            assetId,
            meshName,
            vertexCount,
            subMeshes.Count > 0 ? subMeshes.Count : null,
            null,
            channels.Count > 0 ? channels.Count : null,
            dataSize,
            indexValues?.Count,
            indexFormat,
            Convert.ToBase64String(vertexBytes),
            indexValues,
            positions,
            null,
            null,
            colors,
            null,
            null,
            subMeshes);

        yamlObjectRef = new UnityObjectRef(
            objectId,
            assetId,
            containerId,
            packageGuid,
            null,
            meshFileId?.ToString(CultureInfo.InvariantCulture),
            43,
            meshName,
            []);

        return true;
    }

    private static string BuildYamlObjectId(string assetId, string meshName)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(meshName)));
        return $"{assetId}:obj:yaml:{hash[..12].ToLowerInvariant()}";
    }

    private static long? TryReadYamlMeshFileId(string text)
    {
        var match = Regex.Match(text, "---\\s*!u!43\\s*&(-?\\d+)");
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId)
            ? fileId
            : null;
    }

    private static string? TryReadYamlScalar(string[] lines, string key)
    {
        var prefix = $"{key}:";
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed[prefix.Length..].Trim();
        }

        return null;
    }

    private static int? TryReadYamlInt(string[] lines, string key)
    {
        var value = TryReadYamlScalar(lines, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadYamlHexBlock(string[] lines, string key)
    {
        var prefix = $"{key}:";
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var sb = new StringBuilder();
            var initial = trimmed[prefix.Length..].Trim();
            AppendHex(sb, initial);

            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].Trim();
                if (next.Length == 0)
                {
                    continue;
                }

                if (next.Contains(':', StringComparison.Ordinal) && !IsHexOnly(next))
                {
                    break;
                }

                if (!IsHexOnly(next))
                {
                    break;
                }

                AppendHex(sb, next);
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        return null;
    }

    private static void AppendHex(StringBuilder builder, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsHexChar(ch))
            {
                builder.Append(ch);
            }
        }
    }

    private static bool IsHexOnly(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (!IsHexChar(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHexChar(char ch)
        => ch is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';

    private static byte[] ParseHexBytes(string hex)
    {
        var clean = new string(hex.Where(IsHexChar).ToArray());
        if (clean.Length % 2 != 0)
        {
            clean = clean[..^1];
        }

        var bytes = new byte[clean.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(clean.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }

    private static List<UnityRenderSubMesh> ReadYamlSubMeshes(string[] lines)
    {
        var subMeshes = new List<UnityRenderSubMesh>();
        var inSection = false;
        var current = new Dictionary<string, int>();
        var index = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!inSection)
            {
                if (trimmed == "m_SubMeshes:")
                {
                    inSection = true;
                }

                continue;
            }

            if (trimmed.StartsWith("m_", StringComparison.Ordinal) && !trimmed.StartsWith("m_SubMeshes", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current.Count > 0)
                {
                    subMeshes.Add(BuildYamlSubMesh(index++, current));
                    current = new Dictionary<string, int>();
                }

                continue;
            }

            var parts = trimmed.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            if (int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                current[parts[0].Trim()] = value;
            }
        }

        if (current.Count > 0)
        {
            subMeshes.Add(BuildYamlSubMesh(index, current));
        }

        return subMeshes;
    }

    private static UnityRenderSubMesh BuildYamlSubMesh(int index, Dictionary<string, int> values)
        => new(
            index,
            values.TryGetValue("firstByte", out var firstByte) ? firstByte : null,
            values.TryGetValue("indexCount", out var indexCount) ? indexCount : null,
            values.TryGetValue("topology", out var topology) ? topology : null,
            values.TryGetValue("baseVertex", out var baseVertex) ? baseVertex : null,
            values.TryGetValue("firstVertex", out var firstVertex) ? firstVertex : null,
            values.TryGetValue("vertexCount", out var vertexCount) ? vertexCount : null);

    private static List<YamlChannel> ReadYamlChannels(string[] lines)
    {
        var channels = new List<YamlChannel>();
        var inSection = false;
        YamlChannel? current = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!inSection)
            {
                if (trimmed == "m_Channels:")
                {
                    inSection = true;
                }

                continue;
            }

            if (trimmed.StartsWith("m_DataSize", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("- stream", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    channels.Add(current);
                }

                current = new YamlChannel(ReadYamlIntValue(trimmed), 0, 0, 0);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (trimmed.StartsWith("offset:", StringComparison.Ordinal))
            {
                current = current with { Offset = ReadYamlIntValue(trimmed) };
                continue;
            }

            if (trimmed.StartsWith("format:", StringComparison.Ordinal))
            {
                current = current with { Format = ReadYamlIntValue(trimmed) };
                continue;
            }

            if (trimmed.StartsWith("dimension:", StringComparison.Ordinal))
            {
                current = current with { Dimension = ReadYamlIntValue(trimmed) };
                continue;
            }
        }

        if (current is not null)
        {
            channels.Add(current);
        }

        return channels;
    }

    private static int ReadYamlIntValue(string line)
    {
        var parts = line.Split(':', 2);
        return parts.Length == 2 && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static List<float>? ReadYamlPositions(byte[] vertexBytes, int vertexCount, int? dataSize, List<YamlChannel> channels)
    {
        if (vertexCount <= 0 || vertexBytes.Length == 0)
        {
            return null;
        }

        var positionChannel = channels.FirstOrDefault(channel => channel.Dimension == 3 && channel.Offset == 0 && channel.Format == 0);
        if (positionChannel is null)
        {
            return null;
        }

        var stride = ResolveYamlStride(channels, positionChannel.Stream);
        if (dataSize is > 0)
        {
            var derivedStride = dataSize.Value / vertexCount;
            if (derivedStride >= 12)
            {
                stride = derivedStride;
            }
        }
        if (stride <= 0)
        {
            return null;
        }

        var positions = new List<float>(vertexCount * 3);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var baseOffset = (vertexIndex * stride) + positionChannel.Offset;
            if (baseOffset + 12 > vertexBytes.Length)
            {
                break;
            }

            positions.Add(ReadSingleLittleEndian(vertexBytes, baseOffset));
            positions.Add(ReadSingleLittleEndian(vertexBytes, baseOffset + 4));
            positions.Add(ReadSingleLittleEndian(vertexBytes, baseOffset + 8));
        }

        return positions.Count > 0 ? positions : null;
    }

    private static List<float>? ReadYamlColors(byte[] vertexBytes, int vertexCount, int? dataSize, List<YamlChannel> channels)
    {
        if (vertexCount <= 0 || vertexBytes.Length == 0)
        {
            return null;
        }

        var colorChannel = channels.FirstOrDefault(channel => channel.Dimension == 4);
        if (colorChannel is null)
        {
            return null;
        }

        var stride = ResolveYamlStride(channels, colorChannel.Stream);
        if (dataSize is > 0)
        {
            var derivedStride = dataSize.Value / vertexCount;
            if (derivedStride >= 12)
            {
                stride = derivedStride;
            }
        }
        if (stride <= 0)
        {
            return null;
        }

        var colors = new List<float>(vertexCount * 4);
        var formatSize = ResolveYamlChannelFormatSize(colorChannel.Format);
        var componentSize = formatSize;
        
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var baseOffset = (vertexIndex * stride) + colorChannel.Offset;
            if (baseOffset + (componentSize * 4) > vertexBytes.Length)
            {
                break;
            }

            for (var component = 0; component < 4; component++)
            {
                var componentOffset = baseOffset + (component * componentSize);
                var value = colorChannel.Format switch
                {
                    0 => ReadSingleLittleEndian(vertexBytes, componentOffset),
                    1 => (byte)vertexBytes[componentOffset] / 255f,
                    2 => (sbyte)vertexBytes[componentOffset] / 127f,
                    _ => 1f
                };
                colors.Add(value);
            }
        }

        return colors.Count > 0 ? colors : null;
    }

    private static int ResolveYamlStride(List<YamlChannel> channels, int stream)
    {
        var stride = 0;
        foreach (var channel in channels.Where(channel => channel.Stream == stream))
        {
            var elementSize = ResolveYamlChannelFormatSize(channel.Format) * channel.Dimension;
            stride = Math.Max(stride, channel.Offset + elementSize);
        }

        return stride;
    }

    private static int ResolveYamlChannelFormatSize(int format)
        => format switch
        {
            0 => 4,
            1 => 2,
            2 => 1,
            3 => 1,
            4 => 1,
            5 => 1,
            6 => 1,
            7 => 1,
            _ => 4
        };

    private static float ReadSingleLittleEndian(byte[] data, int offset)
    {
        if (offset + 4 > data.Length)
        {
            return 0f;
        }

        var value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        return BitConverter.Int32BitsToSingle(value);
    }

    private static List<int>? ReadYamlIndexValues(string? indexHex, int? indexFormat)
    {
        if (string.IsNullOrWhiteSpace(indexHex))
        {
            return null;
        }

        var bytes = ParseHexBytes(indexHex);
        if (bytes.Length == 0)
        {
            return null;
        }

        var indices = new List<int>();
        if (indexFormat == 1)
        {
            for (var i = 0; i + 3 < bytes.Length; i += 4)
            {
                indices.Add(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i, 4)));
            }
        }
        else
        {
            for (var i = 0; i + 1 < bytes.Length; i += 2)
            {
                indices.Add(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i, 2)));
            }
        }

        return indices.Count > 0 ? indices : null;
    }

    private sealed record YamlChannel(int Stream, int Offset, int Format, int Dimension);
    private sealed record YamlObjectSection(int ClassId, long FileId, string Content);
    private sealed record YamlTransformInfo(
        long TransformFileId,
        long ParentTransformFileId,
        IReadOnlyList<long> ChildTransformFileIds,
        IReadOnlyList<float>? LocalPosition,
        IReadOnlyList<float>? LocalRotation,
        IReadOnlyList<float>? LocalScale);
    private sealed record YamlRendererInfo(long FileId, int ClassId, string Kind, IReadOnlyList<string> MaterialPointers);

    private static (
        IReadOnlyList<UnityObjectRef> ObjectRefs,
        IReadOnlyList<UnityRenderObject> RenderObjects,
        IReadOnlyList<UnityRenderMesh> RenderMeshes,
        IReadOnlyList<UnityRenderMaterial> RenderMaterials,
        IReadOnlyList<UnityRenderTexture> RenderTextures) BuildObjectRefsFromBundleBytes(
        byte[] bundleBytes,
        string fallbackName,
        string assetId,
        string containerId,
        List<string> warnings)
    {
        var objectRefs = new List<UnityObjectRef>();
        var renderObjects = new List<UnityRenderObject>();
        var renderMeshes = new List<UnityRenderMesh>();
        var renderMaterials = new List<UnityRenderMaterial>();
        var renderTextures = new List<UnityRenderTexture>();

        if (bundleBytes.Length == 0)
        {
            warnings.Add($"Bundle object-level read skipped for '{fallbackName}': bundle payload is empty.");
            return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
        }

        try
        {
            // In-memory parsing: bundleBytes -> MemoryStream -> FileReader.
            // FileReader detects format from magic bytes (UnityFS, UnityWeb, UnityRaw, UnityArchive, or serialized asset).
            // No temporary files written to disk.
            using var bundleStream = new MemoryStream(bundleBytes, writable: false);
            using var bundleReader = new FileReader(fallbackName, bundleStream);
            var assetsManager = new AssetsManager();

            if (bundleReader.FileType == FileType.AssetsFile)
            {
                var serializedFile = new SerializedFile(bundleReader, assetsManager);
                AppendSerializedFileObjects(serializedFile, assetId, containerId, null, objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
                return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
            }

            if (bundleReader.FileType != FileType.BundleFile)
            {
                warnings.Add($"Bundle object-level read skipped for '{fallbackName}': unsupported bundle payload type {bundleReader.FileType}.");
                return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
            }

            // Payload is a Unity asset bundle archive. Iterate internal file list and deserialize each.
            // Each internal archived file is checked for AssetsFile format; other types are skipped.
            var importOptions = new ImportOptions();
            var bundleFile = new BundleFile(bundleReader, importOptions.BundleOptions);
            foreach (var file in bundleFile.fileList)
            {
                if (file.stream is null)
                {
                    continue;
                }

                file.stream.Position = 0;
                var parentDirectory = Path.GetDirectoryName(bundleReader.FullPath) ?? string.Empty;
                var virtualPath = Path.Combine(parentDirectory, file.fileName);
                var subReader = new FileReader(virtualPath, file.stream);
                if (subReader.FileType != FileType.AssetsFile)
                {
                    continue;
                }

                var serializedFile = new SerializedFile(subReader, assetsManager);
                AppendSerializedFileObjects(serializedFile, assetId, containerId, null, objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
            }

            return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
        }
        catch (Exception ex)
        {
            warnings.Add($"Bundle object-level read failed for '{fallbackName}': {ex.Message}");
            return (objectRefs, renderObjects, renderMeshes, renderMaterials, renderTextures);
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
        var subMeshes = ExtractSubMeshes(data);

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
                decodedChannels.Tangents,
                decodedChannels.Colors,
            decodedChannels.Uv0,
            decodedChannels.Uv1,
            subMeshes);
    }

    private static IReadOnlyList<UnityRenderSubMesh> ExtractSubMeshes(OrderedDictionary meshData)
    {
        if (!TryGetDictionaryValue(meshData, "m_SubMeshes", out var subMeshesValue)
            || subMeshesValue is not Array subMeshesArray)
        {
            return [];
        }

        var subMeshes = new List<UnityRenderSubMesh>(subMeshesArray.Length);
        for (var subMeshIndex = 0; subMeshIndex < subMeshesArray.Length; subMeshIndex++)
        {
            if (subMeshesArray.GetValue(subMeshIndex) is not OrderedDictionary subMeshDictionary)
            {
                continue;
            }

            subMeshes.Add(new UnityRenderSubMesh(
                subMeshIndex,
                ReadIntValue(subMeshDictionary, "firstByte"),
                ReadIntValue(subMeshDictionary, "indexCount"),
                ReadIntValue(subMeshDictionary, "topology"),
                ReadIntValue(subMeshDictionary, "baseVertex"),
                ReadIntValue(subMeshDictionary, "firstVertex"),
                ReadIntValue(subMeshDictionary, "vertexCount")));
        }

        return subMeshes;
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
        var width = ReadIntValue(data, "m_Width") ?? 0;
        var height = ReadIntValue(data, "m_Height") ?? 0;
        var textureFormat = ReadIntValue(data, "m_TextureFormat") ?? 0;

        // Try to extract and convert texture image bytes to PNG
        var imageDataBase64 = TryExtractTextureImageBytes(
            serializedFile,
            data,
            width,
            height,
            textureFormat,
            streamInfo);

        return new UnityRenderTexture(
            objectId,
            assetId,
            BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
            width,
            height,
            textureFormat,
            ReadArrayCount(data, "m_ImageData"),
            streamInfo.Path,
            streamInfo.Offset,
            streamInfo.Size,
            imageDataBase64);
    }

    private static string? TryExtractTextureImageBytes(
        SerializedFile serializedFile,
        OrderedDictionary data,
        int width,
        int height,
        int textureFormat,
        (string? Path, long? Offset, int? Size) streamInfo)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        try
        {
            byte[]? textureBytes = null;

            // Try to get image data from stream or embedded data
            if (!string.IsNullOrWhiteSpace(streamInfo.Path) && streamInfo.Offset.HasValue && streamInfo.Size.HasValue)
            {
                // External stream file (e.g., .resS)
                textureBytes = TryReadExternalStreamFile(serializedFile, streamInfo.Path, streamInfo.Offset.Value, streamInfo.Size.Value);
            }
            else if (TryGetDictionaryValue(data, "m_ImageData", out var imageDataValue) && imageDataValue is byte[] embeddedBytes)
            {
                // Embedded image data directly in the serialized file
                textureBytes = embeddedBytes;
            }

            if (textureBytes?.Length == 0)
            {
                return null;
            }

            if (textureBytes == null)
            {
                return null;
            }

            // Convert texture bytes to PNG base64
            if (TextureUtilities.TryConvertTextureToPngBase64(textureBytes, textureFormat, width, height, out var pngBase64))
            {
                return pngBase64;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryReadExternalStreamFile(SerializedFile serializedFile, string streamPath, long offset, int size)
    {
        try
        {
            // Try to find and read the external resource file
            var resourceFileName = Path.GetFileName(streamPath);
            var assetsFileDirectory = Path.GetDirectoryName(serializedFile.fullName);
            var resourceFilePath = Path.Combine(assetsFileDirectory, resourceFileName);

            if (!File.Exists(resourceFilePath))
            {
                var findFiles = Directory.GetFiles(assetsFileDirectory, resourceFileName, SearchOption.AllDirectories);
                if (findFiles.Length == 0)
                {
                    return null;
                }
                resourceFilePath = findFiles[0];
            }

            using var fileStream = File.OpenRead(resourceFilePath);
            using var reader = new BinaryReader(fileStream);
            reader.BaseStream.Position = offset;
            return reader.ReadBytes(size);
        }
        catch
        {
            return null;
        }
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

    private static (IReadOnlyList<float>? Positions, IReadOnlyList<float>? Normals, IReadOnlyList<float>? Tangents, IReadOnlyList<float>? Colors, IReadOnlyList<float>? Uv0, IReadOnlyList<float>? Uv1) DecodeVertexChannels(
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
            return (null, null, null, null, null, null);
        }

        var channelInfos = ParseVertexChannels(channelsArray);
        if (channelInfos.Count == 0)
        {
            return (null, null, null, null, null, null);
        }

        var streamLayouts = BuildStreamLayouts(channelInfos);
        if (streamLayouts.Count == 0)
        {
            return (null, null, null, null, null, null);
        }

        var streamBaseOffsets = BuildStreamBaseOffsets(streamLayouts, vertexCount.Value, vertexDataBytes.Length);

        var positions = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 0, expectedDimension: 3);
        var normals = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 1, expectedDimension: 3);
        var tangents = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 2, expectedDimension: 4);
        var colors = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 3, expectedDimension: 4);
        var uv0 = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 4, expectedDimension: 2);
        var uv1 = DecodeFloatChannel(vertexDataBytes, vertexCount.Value, channelInfos, streamLayouts, streamBaseOffsets, channelIndex: 5, expectedDimension: 2);

        return (
            positions.Count > 0 ? positions : null,
            normals.Count > 0 ? normals : null,
            tangents.Count > 0 ? tangents : null,
            colors.Count > 0 ? colors : null,
                uv0.Count > 0 ? uv0 : null,
                uv1.Count > 0 ? uv1 : null);
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
                        [],
                    null,
                    null,
                    null);
            }

            if (objectReader.type == ClassIDType.MeshRenderer)
            {
                var meshRenderer = new MeshRenderer(objectReader);
                var materialObjectIds = meshRenderer.m_Materials
                    .Select(material => ResolvePointerObjectId(assetId, material.m_FileID, material.m_PathID))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Cast<string>()
                    .ToArray();

                return new UnityRenderObject(
                    objectId,
                    assetId,
                    objectInfo.classID,
                    "meshrenderer",
                    BuildObjectDisplayName(objectInfo.classID, objectInfo.m_PathID),
                    ResolvePointerObjectId(assetId, meshRenderer.m_GameObject.m_FileID, meshRenderer.m_GameObject.m_PathID),
                    [],
                    null,
                    materialObjectIds,
                    BuildMaterialAssignments(materialObjectIds),
                    null,
                    null,
                    null);
            }

            if (objectReader.type == ClassIDType.SkinnedMeshRenderer)
            {
                var skinnedMeshRenderer = new SkinnedMeshRenderer(objectReader);
                var materialObjectIds = skinnedMeshRenderer.m_Materials
                    .Select(material => ResolvePointerObjectId(assetId, material.m_FileID, material.m_PathID))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Cast<string>()
                    .ToArray();

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
                    materialObjectIds,
                    BuildMaterialAssignments(materialObjectIds),
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

    private static IReadOnlyList<UnityRenderMaterialAssignment> BuildMaterialAssignments(IReadOnlyList<string> materialObjectIds)
    {
        if (materialObjectIds.Count == 0)
        {
            return [];
        }

        var assignments = new List<UnityRenderMaterialAssignment>(materialObjectIds.Count);
        for (var index = 0; index < materialObjectIds.Count; index++)
        {
            assignments.Add(new UnityRenderMaterialAssignment(index, materialObjectIds[index]));
        }

        return assignments;
    }

    private static IReadOnlyList<UnityRenderPrimitive> BuildRenderPrimitives(
        IReadOnlyList<UnityRenderObject> renderObjects,
        IReadOnlyList<UnityRenderMesh> renderMeshes)
    {
        if (renderMeshes.Count == 0)
        {
            return [];
        }

        var meshByObjectId = renderMeshes
            .GroupBy(mesh => mesh.ObjectId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var meshByGameObjectId = renderObjects
            .Where(item => item.Kind == "meshfilter"
                && !string.IsNullOrWhiteSpace(item.ParentObjectId)
                && !string.IsNullOrWhiteSpace(item.MeshObjectId))
            .GroupBy(item => item.ParentObjectId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().MeshObjectId!, StringComparer.Ordinal);

        var primitives = new List<UnityRenderPrimitive>();
        var referencedMeshIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var renderObject in renderObjects)
        {
            if (renderObject.Kind is not ("meshrenderer" or "skinnedmeshrenderer"))
            {
                continue;
            }

            var meshObjectId = ResolveRenderObjectMeshObjectId(renderObject, meshByGameObjectId);
            if (string.IsNullOrWhiteSpace(meshObjectId)
                || !meshByObjectId.TryGetValue(meshObjectId, out var mesh))
            {
                continue;
            }

            referencedMeshIds.Add(mesh.ObjectId);

            var assignments = renderObject.MaterialAssignments.Count > 0
                ? renderObject.MaterialAssignments
                : BuildMaterialAssignments(renderObject.MaterialObjectIds);

            if (mesh.SubMeshes.Count > 0)
            {
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var materialObjectId = assignments
                        .FirstOrDefault(assignment => assignment.SubMeshIndex == subMesh.SubMeshIndex)
                        ?.MaterialObjectId;
                    var subMeshGeometry = BuildPrimitiveGeometry(mesh, subMesh);

                    primitives.Add(new UnityRenderPrimitive(
                        BuildRenderPrimitiveId(renderObject.ObjectId, mesh.ObjectId, subMesh.SubMeshIndex, materialObjectId),
                        renderObject.AssetId,
                        renderObject.ObjectId,
                        renderObject.ParentObjectId,
                        mesh.ObjectId,
                        subMesh.SubMeshIndex,
                        materialObjectId,
                        subMeshGeometry.IndexValues,
                        subMeshGeometry.Positions,
                        subMeshGeometry.Normals,
                        subMeshGeometry.Tangents,
                        subMeshGeometry.Colors,
                        subMeshGeometry.Uv0,
                        subMeshGeometry.Uv1,
                        subMesh.FirstByte,
                        subMesh.IndexCount,
                        subMesh.Topology,
                        subMesh.BaseVertex,
                        subMesh.FirstVertex,
                        subMesh.VertexCount));
                }

                continue;
            }

            if (assignments.Count > 0)
            {
                foreach (var assignment in assignments)
                {
                    var assignmentGeometry = BuildPrimitiveGeometry(mesh, null);
                    primitives.Add(new UnityRenderPrimitive(
                        BuildRenderPrimitiveId(renderObject.ObjectId, mesh.ObjectId, assignment.SubMeshIndex, assignment.MaterialObjectId),
                        renderObject.AssetId,
                        renderObject.ObjectId,
                        renderObject.ParentObjectId,
                        mesh.ObjectId,
                        assignment.SubMeshIndex,
                        assignment.MaterialObjectId,
                        assignmentGeometry.IndexValues,
                        assignmentGeometry.Positions,
                        assignmentGeometry.Normals,
                        assignmentGeometry.Tangents,
                        assignmentGeometry.Colors,
                        assignmentGeometry.Uv0,
                        assignmentGeometry.Uv1,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));
                }

                continue;
            }

            var fallbackGeometry = BuildPrimitiveGeometry(mesh, null);
            primitives.Add(new UnityRenderPrimitive(
                BuildRenderPrimitiveId(renderObject.ObjectId, mesh.ObjectId, 0, null),
                renderObject.AssetId,
                renderObject.ObjectId,
                renderObject.ParentObjectId,
                mesh.ObjectId,
                0,
                null,
                fallbackGeometry.IndexValues,
                fallbackGeometry.Positions,
                fallbackGeometry.Normals,
                fallbackGeometry.Tangents,
                fallbackGeometry.Colors,
                fallbackGeometry.Uv0,
                fallbackGeometry.Uv1,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        foreach (var mesh in renderMeshes)
        {
            if (referencedMeshIds.Contains(mesh.ObjectId))
            {
                continue;
            }

            if (mesh.SubMeshes.Count > 0)
            {
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var subMeshGeometry = BuildPrimitiveGeometry(mesh, subMesh);
                    primitives.Add(new UnityRenderPrimitive(
                        BuildRenderPrimitiveId(mesh.ObjectId, mesh.ObjectId, subMesh.SubMeshIndex, null),
                        mesh.AssetId,
                        mesh.ObjectId,
                        null,
                        mesh.ObjectId,
                        subMesh.SubMeshIndex,
                        null,
                        subMeshGeometry.IndexValues,
                        subMeshGeometry.Positions,
                        subMeshGeometry.Normals,
                        subMeshGeometry.Tangents,
                        subMeshGeometry.Colors,
                        subMeshGeometry.Uv0,
                        subMeshGeometry.Uv1,
                        subMesh.FirstByte,
                        subMesh.IndexCount,
                        subMesh.Topology,
                        subMesh.BaseVertex,
                        subMesh.FirstVertex,
                        subMesh.VertexCount));
                }

                continue;
            }

            var fallbackGeometry = BuildPrimitiveGeometry(mesh, null);
            primitives.Add(new UnityRenderPrimitive(
                BuildRenderPrimitiveId(mesh.ObjectId, mesh.ObjectId, 0, null),
                mesh.AssetId,
                mesh.ObjectId,
                null,
                mesh.ObjectId,
                0,
                null,
                fallbackGeometry.IndexValues,
                fallbackGeometry.Positions,
                fallbackGeometry.Normals,
                fallbackGeometry.Tangents,
                fallbackGeometry.Colors,
                fallbackGeometry.Uv0,
                fallbackGeometry.Uv1,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        return primitives;
    }

    private static string? ResolveRenderObjectMeshObjectId(
        UnityRenderObject renderObject,
        IReadOnlyDictionary<string, string> meshByGameObjectId)
    {
        if (!string.IsNullOrWhiteSpace(renderObject.MeshObjectId))
        {
            return renderObject.MeshObjectId;
        }

        if (!string.IsNullOrWhiteSpace(renderObject.ParentObjectId)
            && meshByGameObjectId.TryGetValue(renderObject.ParentObjectId, out var meshObjectId))
        {
            return meshObjectId;
        }

        return null;
    }

    private static string BuildRenderPrimitiveId(
        string renderObjectId,
        string meshObjectId,
        int subMeshIndex,
        string? materialObjectId)
    {
        var raw = $"{renderObjectId}|{meshObjectId}|{subMeshIndex.ToString(CultureInfo.InvariantCulture)}|{materialObjectId ?? "none"}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return $"primitive:{hash[..16].ToLowerInvariant()}";
    }

    private static PrimitiveGeometry BuildPrimitiveGeometry(UnityRenderMesh mesh, UnityRenderSubMesh? subMesh)
    {
        var sourceIndexValues = BuildPrimitiveIndexValues(mesh, subMesh);
        if (sourceIndexValues is not { Count: > 0 })
        {
            return new PrimitiveGeometry(null, null, null, null, null, null, null);
        }

        var availableVertexCount = ResolveAvailableVertexCount(mesh);
        if (availableVertexCount <= 0)
        {
            return new PrimitiveGeometry(sourceIndexValues, null, null, null, null, null, null);
        }

        var indexMap = new Dictionary<int, int>();
        var localIndices = new List<int>(sourceIndexValues.Count);
        List<float>? localPositions = mesh.Positions is { Count: > 0 } ? [] : null;
        List<float>? localNormals = mesh.Normals is { Count: > 0 } ? [] : null;
        List<float>? localTangents = mesh.Tangents is { Count: > 0 } ? [] : null;
        List<float>? localColors = mesh.Colors is { Count: > 0 } ? [] : null;
        List<float>? localUv0 = mesh.Uv0 is { Count: > 0 } ? [] : null;
        List<float>? localUv1 = mesh.Uv1 is { Count: > 0 } ? [] : null;

        foreach (var sourceIndex in sourceIndexValues)
        {
            if (sourceIndex < 0 || sourceIndex >= availableVertexCount)
            {
                return new PrimitiveGeometry(sourceIndexValues, null, null, null, null, null, null);
            }

            if (!indexMap.TryGetValue(sourceIndex, out var localIndex))
            {
                localIndex = indexMap.Count;
                indexMap[sourceIndex] = localIndex;

                AppendVertexComponents(localPositions, mesh.Positions, sourceIndex, componentsPerVertex: 3);
                AppendVertexComponents(localNormals, mesh.Normals, sourceIndex, componentsPerVertex: 3);
                AppendVertexComponents(localTangents, mesh.Tangents, sourceIndex, componentsPerVertex: 4);
                AppendVertexComponents(localColors, mesh.Colors, sourceIndex, componentsPerVertex: 4);
                AppendVertexComponents(localUv0, mesh.Uv0, sourceIndex, componentsPerVertex: 2);
                AppendVertexComponents(localUv1, mesh.Uv1, sourceIndex, componentsPerVertex: 2);
            }

            localIndices.Add(localIndex);
        }

        return new PrimitiveGeometry(
            localIndices,
            localPositions,
            localNormals,
            localTangents,
            localColors,
            localUv0,
            localUv1);
    }

    private static IReadOnlyList<int>? BuildPrimitiveIndexValues(UnityRenderMesh mesh, UnityRenderSubMesh? subMesh)
    {
        if (mesh.IndexValues is not { Count: > 0 })
        {
            return null;
        }

        if (subMesh is null)
        {
            return mesh.IndexValues;
        }

        var bytesPerIndex = mesh.IndexFormat == 1 ? 4 : 2;
        var startIndex = subMesh.FirstByte is > 0
            ? subMesh.FirstByte.Value / bytesPerIndex
            : 0;

        if (startIndex < 0 || startIndex >= mesh.IndexValues.Count)
        {
            return null;
        }

        var requestedCount = subMesh.IndexCount ?? (mesh.IndexValues.Count - startIndex);
        if (requestedCount <= 0)
        {
            return null;
        }

        var availableCount = mesh.IndexValues.Count - startIndex;
        var clampedCount = Math.Min(requestedCount, availableCount);
        if (clampedCount <= 0)
        {
            return null;
        }

        var baseVertex = subMesh.BaseVertex ?? 0;
        var sliced = new List<int>(clampedCount);
        for (var index = 0; index < clampedCount; index++)
        {
            sliced.Add(mesh.IndexValues[startIndex + index] + baseVertex);
        }

        return sliced;
    }

    private static int ResolveAvailableVertexCount(UnityRenderMesh mesh)
    {
        var counts = new List<int>(6);
        if (mesh.Positions is { Count: > 0 })
        {
            counts.Add(mesh.Positions.Count / 3);
        }

        if (mesh.Normals is { Count: > 0 })
        {
            counts.Add(mesh.Normals.Count / 3);
        }

        if (mesh.Tangents is { Count: > 0 })
        {
            counts.Add(mesh.Tangents.Count / 4);
        }

        if (mesh.Colors is { Count: > 0 })
        {
            counts.Add(mesh.Colors.Count / 4);
        }

        if (mesh.Uv0 is { Count: > 0 })
        {
            counts.Add(mesh.Uv0.Count / 2);
        }

        if (mesh.Uv1 is { Count: > 0 })
        {
            counts.Add(mesh.Uv1.Count / 2);
        }

        if (counts.Count == 0)
        {
            return 0;
        }

        return counts.Min();
    }

    private static void AppendVertexComponents(
        List<float>? destination,
        IReadOnlyList<float>? source,
        int vertexIndex,
        int componentsPerVertex)
    {
        if (destination is null || source is null)
        {
            return;
        }

        var offset = vertexIndex * componentsPerVertex;
        if (offset < 0 || offset + componentsPerVertex > source.Count)
        {
            return;
        }

        for (var component = 0; component < componentsPerVertex; component++)
        {
            destination.Add(source[offset + component]);
        }
    }

    private sealed record PrimitiveGeometry(
        IReadOnlyList<int>? IndexValues,
        IReadOnlyList<float>? Positions,
        IReadOnlyList<float>? Normals,
        IReadOnlyList<float>? Tangents,
        IReadOnlyList<float>? Colors,
        IReadOnlyList<float>? Uv0,
        IReadOnlyList<float>? Uv1);

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
