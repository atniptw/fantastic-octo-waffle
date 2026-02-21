using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UnityAssetParser;

public sealed class UnityPackageParser
{
    public BaseAssetsContext Parse(byte[] packageBytes)
    {
        if (packageBytes is null)
        {
            throw new ArgumentNullException(nameof(packageBytes));
        }

        var context = new BaseAssetsContext();
        var tarBytes = TryDecompressGzip(packageBytes, context);
        var container = new ParsedContainer("unitypackage", ContainerKind.UnityPackageTar, tarBytes.Length);
        var pathNamesByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in TarReader.EnumerateEntries(tarBytes))
        {
            container.Entries.Add(new ContainerEntry(entry.Name, entry.Offset, entry.Size, 0));

            if (entry.Name.EndsWith("/pathname", StringComparison.OrdinalIgnoreCase))
            {
                var pathBytes = new byte[entry.Size];
                Buffer.BlockCopy(tarBytes, entry.Offset, pathBytes, 0, entry.Size);
                var pathName = Encoding.UTF8.GetString(pathBytes).Trim('\0', '\r', '\n', ' ');
                if (!string.IsNullOrWhiteSpace(pathName))
                {
                    var guid = GetGuidFromEntryPath(entry.Name);
                    if (guid is not null)
                    {
                        pathNamesByGuid[guid] = pathName;
                    }
                }
            }

            if (entry.Name.EndsWith("/asset", StringComparison.OrdinalIgnoreCase))
            {
                var assetBytes = new byte[entry.Size];
                Buffer.BlockCopy(tarBytes, entry.Offset, assetBytes, 0, entry.Size);
                if (UnityYamlParser.LooksLikeYaml(assetBytes))
                {
                    UnityYamlParser.Parse(assetBytes, entry.Name, context);
                }
                else
                {
                    SkeletonParser.Parse(assetBytes, entry.Name, context);
                }
            }
        }
        context.Containers.Add(container);

        BuildInventory(context, pathNamesByGuid);

        return context;
    }

    private static byte[] TryDecompressGzip(byte[] input, BaseAssetsContext context)
    {
        if (input.Length < 2 || input[0] != 0x1F || input[1] != 0x8B)
        {
            return input;
        }

        try
        {
            using var source = new MemoryStream(input, writable: false);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            context.Warnings.Add("Failed to decompress unitypackage gzip payload; treating as raw tar.");
            return input;
        }
    }

    private static void BuildInventory(BaseAssetsContext context, IReadOnlyDictionary<string, string> pathNamesByGuid)
    {
        var inventory = context.Inventory;
        inventory.Entries.Clear();
        inventory.GameObjects.Clear();
        inventory.Transforms.Clear();
        inventory.Meshes.Clear();
        inventory.Materials.Clear();
        inventory.Textures.Clear();

        foreach (var container in context.Containers)
        {
            foreach (var entry in container.Entries)
            {
                var guid = GetGuidFromEntryPath(entry.Path);
                var resolvedPath = guid is not null && pathNamesByGuid.TryGetValue(guid, out var pathName)
                    ? pathName
                    : null;
                inventory.Entries.Add(new PackageEntryInfo(
                    container.SourceName,
                    container.Kind,
                    entry.Path,
                    entry.Size,
                    entry.Flags,
                    guid,
                    resolvedPath));
            }
        }

        foreach (var gameObject in context.SemanticGameObjects)
        {
            inventory.GameObjects.Add(new PackageGameObjectInfo(
                gameObject.PathId,
                gameObject.Name,
                gameObject.IsActive,
                gameObject.Layer));
        }

        foreach (var transform in context.SemanticTransforms)
        {
            inventory.Transforms.Add(new PackageTransformInfo(
                transform.PathId,
                transform.GameObjectPathId,
                transform.ParentPathId,
                transform.ChildrenPathIds.Count));
        }

        foreach (var mesh in context.SemanticMeshes)
        {
            inventory.Meshes.Add(new PackageMeshInfo(
                mesh.PathId,
                mesh.Name,
                mesh.VertexCount,
                mesh.SubMeshCount,
                mesh.ChannelFlags));
        }

        foreach (var material in context.SemanticMaterials)
        {
            inventory.Materials.Add(new PackageMaterialInfo(
                material.PathId,
                material.Name,
                material.ShaderPathId));
        }

        foreach (var texture in context.SemanticTextures)
        {
            inventory.Textures.Add(new PackageTextureInfo(
                texture.PathId,
                texture.Name,
                texture.Width,
                texture.Height,
                texture.Format,
                texture.MipCount));
        }
    }

    private static string? GetGuidFromEntryPath(string entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        var slashIndex = entryPath.IndexOf('/');
        if (slashIndex <= 0)
        {
            return null;
        }

        return entryPath.Substring(0, slashIndex);
    }
}
