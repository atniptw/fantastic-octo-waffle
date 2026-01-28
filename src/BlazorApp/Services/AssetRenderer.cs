using System.IO.Compression;
using BlazorApp.Models;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;
using UnityAssetParser.Classes;

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
            // Match by filename (handles files in subdirectories)
            var entry = archive.Entries.FirstOrDefault(e => 
                e.Name.Equals(file.FileName, StringComparison.OrdinalIgnoreCase) ||
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

        // Use first mesh object
        var meshObj = meshObjects[0];
        var meshData = serializedFile.ReadObjectData(meshObj);
        var version = ExtractVersionTuple((int)serializedFile.Header.Version);
        var isBigEndian = serializedFile.Header.Endianness != 0;
        var mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);

        if (mesh == null)
        {
            throw new InvalidDataException("Failed to parse Mesh object.");
        }

        // Convert to Three.js format
        return ConvertMeshToThreeJS(mesh);
    }

    private ThreeJsGeometry ParseSerializedFile(byte[] serializedFileBytes)
    {
        var serializedFile = UnityAssetParser.SerializedFile.SerializedFile.Parse(serializedFileBytes);

        var meshObjects = serializedFile.GetObjectsByClassId(43).ToList();

        if (meshObjects.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in SerializedFile.");
        }

        // Use first mesh object
        var meshObj = meshObjects[0];
        var meshData = serializedFile.ReadObjectData(meshObj);
        var version = ExtractVersionTuple((int)serializedFile.Header.Version);
        var isBigEndian = serializedFile.Header.Endianness != 0;
        var mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);

        if (mesh == null)
        {
            throw new InvalidDataException("Failed to parse Mesh object.");
        }

        // Convert to Three.js format
        return ConvertMeshToThreeJS(mesh);
    }

    /// <summary>
    /// Converts SerializedFile version number to version tuple (major, minor, patch, build).
    /// For version 22 (modern Unity), returns (2022, 0, 0, 0) as a reasonable default.
    /// </summary>
    private static (int, int, int, int) ExtractVersionTuple(int version)
    {
        return version switch
        {
            22 => (2022, 0, 0, 0),  // Modern Unity 2022+
            21 => (2021, 0, 0, 0),  // Unity 2021
            20 => (2020, 0, 0, 0),  // Unity 2020
            19 => (2019, 0, 0, 0),  // Unity 2019
            _ => (version, 0, 0, 0) // Fallback: use version as major
        };
    }

    /// <summary>
    /// Converts a parsed Mesh object to Three.js-compatible geometry format.
    /// </summary>
    private ThreeJsGeometry ConvertMeshToThreeJS(Mesh mesh)
    {
        // Extract indices from IndexBuffer
        uint[] indices = ExtractIndices(mesh);

        // For now, we have indices but vertex positions require loading external .resS resource
        // Create minimal geometry with what we have
        var positions = new float[mesh.VertexData?.VertexCount * 3 ?? 0];
        
        // Create placeholder positions (will be replaced when we load .resS files)
        // For now, create a simple bounding box visualization
        if (positions.Length == 0)
        {
            positions = new float[]
            {
                -0.5f, -0.5f, -0.5f,
                0.5f, -0.5f, -0.5f,
                0.5f, 0.5f, -0.5f,
                -0.5f, 0.5f, -0.5f,
            };
            indices = new uint[] { 0, 1, 1, 2, 2, 3, 3, 0 };
        }

        // Extract submeshes as groups
        var groups = new List<SubMeshGroup>();
        if (mesh.SubMeshes != null)
        {
            int groupIndex = 0;
            foreach (var submesh in mesh.SubMeshes)
            {
                groups.Add(new SubMeshGroup
                {
                    Start = (int)(submesh.FirstByte / 4), // Convert bytes to uint indices
                    Count = (int)submesh.IndexCount,
                    MaterialIndex = groupIndex++
                });
            }
        }

        return new ThreeJsGeometry
        {
            Positions = positions,
            Indices = indices,
            VertexCount = (int)(mesh.VertexData?.VertexCount ?? 4),
            TriangleCount = indices.Length / 3,
            Groups = groups.Count > 0 ? groups : null
        };
    }

    /// <summary>
    /// Extracts indices from the IndexBuffer byte array.
    /// </summary>
    private uint[] ExtractIndices(Mesh mesh)
    {
        if (mesh.IndexBuffer == null || mesh.IndexBuffer.Length == 0)
        {
            return Array.Empty<uint>();
        }

        // Determine if using 16-bit or 32-bit indices
        bool use32Bit = mesh.IndexFormat == 1 || (mesh.IndexFormat == null && mesh.Use16BitIndices == false);

        if (use32Bit)
        {
            var indices = new uint[mesh.IndexBuffer.Length / 4];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = BitConverter.ToUInt32(mesh.IndexBuffer, i * 4);
            }
            return indices;
        }
        else
        {
            var indices = new uint[mesh.IndexBuffer.Length / 2];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = BitConverter.ToUInt16(mesh.IndexBuffer, i * 2);
            }
            return indices;
        }
    }

}
