using System.IO.Compression;
using BlazorApp.Models;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;

namespace BlazorApp.Services;

/// <summary>
/// Implementation of asset renderer that parses Unity bundles and extracts mesh geometry.
/// </summary>
public class AssetRenderer : IAssetRenderer
{
    /// <inheritdoc/>
    public async Task<ThreeJsGeometry> RenderAsync(FileIndexItem file, byte[] zipBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(zipBytes);
        ct.ThrowIfCancellationRequested();

        await Task.CompletedTask;

        if (!file.Renderable)
        {
            throw new InvalidOperationException($"File '{file.FileName}' is not marked as renderable.");
        }

        // Extract file from ZIP
        byte[] fileBytes;
        using (var ms = new MemoryStream(zipBytes))
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            var entry = archive.Entries.FirstOrDefault(e => 
                e.FullName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
            
            if (entry == null)
            {
                throw new InvalidDataException($"File '{file.FileName}' not found in ZIP archive.");
            }

            using var entryStream = entry.Open();
            fileBytes = new byte[entry.Length];
            int totalRead = 0;
            int bytesRead;
            while (totalRead < fileBytes.Length && 
                   (bytesRead = await entryStream.ReadAsync(fileBytes.AsMemory(totalRead), ct)) > 0)
            {
                totalRead += bytesRead;
            }
        }

        // Parse based on file type
        if (file.Type == FileType.UnityFS)
        {
            return ParseUnityFSBundle(fileBytes);
        }
        else if (file.Type == FileType.SerializedFile)
        {
            return ParseSerializedFile(fileBytes);
        }
        else
        {
            throw new InvalidDataException($"Unsupported file type: {file.Type}");
        }
    }

    private ThreeJsGeometry ParseUnityFSBundle(byte[] bundleBytes)
    {
        using var ms = new MemoryStream(bundleBytes);
        var parseResult = BundleFile.TryParse(ms);

        if (!parseResult.Success || parseResult.Bundle == null)
        {
            throw new InvalidDataException(
                $"Failed to parse UnityFS bundle: {string.Join(", ", parseResult.Errors)}");
        }

        var bundle = parseResult.Bundle;

        // Extract Node 0 (SerializedFile containing mesh objects)
        if (bundle.Nodes.Count == 0)
        {
            throw new InvalidDataException("UnityFS bundle contains no nodes.");
        }

        var node0 = bundle.Nodes[0];
        var node0Data = bundle.ExtractNode(node0);

        // Parse SerializedFile from Node 0
        var serializedFile = UnityAssetParser.SerializedFile.SerializedFile.Parse(node0Data.Span);

        // Find Mesh objects (ClassID 43)
        var meshObjects = serializedFile.GetObjectsByClassId(43).ToList();

        if (meshObjects.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in bundle.");
        }

        // For now, use first mesh object
        // TODO: Full mesh parsing with MeshHelper (issue #190)
        // For MVP, return a simple placeholder geometry
        return CreatePlaceholderGeometry();
    }

    private ThreeJsGeometry ParseSerializedFile(byte[] serializedFileBytes)
    {
        var serializedFile = UnityAssetParser.SerializedFile.SerializedFile.Parse(serializedFileBytes);

        var meshObjects = serializedFile.GetObjectsByClassId(43).ToList();

        if (meshObjects.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in SerializedFile.");
        }

        // For MVP, return a simple placeholder geometry
        return CreatePlaceholderGeometry();
    }

    private ThreeJsGeometry CreatePlaceholderGeometry()
    {
        // Create a simple triangle as placeholder
        // TODO: Replace with real mesh parsing (issue #190)
        var positions = new float[]
        {
            0.0f, 1.0f, 0.0f,   // top
            -1.0f, -1.0f, 0.0f, // bottom-left
            1.0f, -1.0f, 0.0f   // bottom-right
        };

        var indices = new uint[] { 0, 1, 2 };

        return new ThreeJsGeometry
        {
            Positions = positions,
            Indices = indices,
            VertexCount = 3,
            TriangleCount = 1
        };
    }
}
