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
        var version = ParseUnityVersion(serializedFile.Header.UnityVersionString);
        var isBigEndian = serializedFile.Header.Endianness != 0;
        var mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);

        if (mesh == null)
        {
            throw new InvalidDataException("Failed to parse Mesh object.");
        }

        // Try to load external resource data (Node 1 usually contains .resS data)
        byte[]? externalResourceData = null;
        if (bundle.Nodes.Count > 1)
        {
            try
            {
                var node1 = bundle.Nodes[1];
                var node1Data = bundle.ExtractNode(node1);
                externalResourceData = node1Data.ToArray();
            }
            catch
            {
                // If resource loading fails, continue without external data
                // (vertices will still show as placeholder)
            }
        }

        // Convert to Three.js format with actual vertex data
        return ConvertMeshToThreeJS(mesh, externalResourceData);
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
        var version = ParseUnityVersion(serializedFile.Header.UnityVersionString);
        var isBigEndian = serializedFile.Header.Endianness != 0;
        var mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);

        if (mesh == null)
        {
            throw new InvalidDataException("Failed to parse Mesh object.");
        }

        // For SerializedFile, we don't have external resources, so pass null
        // Vertex data should be inline if available
        return ConvertMeshToThreeJS(mesh, null);
    }

    /// <summary>
    /// Converts SerializedFile version number to version tuple (major, minor, patch, build).
    /// For version 22 (modern Unity), returns (2022, 0, 0, 0) as a reasonable default.
    /// </summary>
    /// <summary>
    /// Parses Unity version string (e.g., "2020.3.3f1c1") into tuple (major, minor, patch, type).
    /// </summary>
    private static (int, int, int, int) ParseUnityVersion(string versionString)
    {
        if (string.IsNullOrEmpty(versionString))
        {
            return (2020, 0, 0, 0); // Default fallback
        }

        // Parse "2020.3.3f1c1" -> (2020, 3, 3, 0)
        var parts = versionString.Split('.');
        if (parts.Length < 3)
        {
            return (2020, 0, 0, 0);
        }

        int.TryParse(parts[0], out int major);
        int.TryParse(parts[1], out int minor);
        
        // Parse patch (may have letter suffix like "3f1c1")
        var patchStr = parts[2];
        int patchEndIdx = 0;
        while (patchEndIdx < patchStr.Length && char.IsDigit(patchStr[patchEndIdx]))
        {
            patchEndIdx++;
        }
        int.TryParse(patchStr.Substring(0, Math.Max(1, patchEndIdx)), out int patch);

        return (major, minor, patch, 0);
    }

    /// <summary>
    /// Converts a parsed Mesh object to Three.js-compatible geometry format.
    /// Extracts actual vertex positions and attributes from the mesh data.
    /// </summary>
    private ThreeJsGeometry ConvertMeshToThreeJS(Mesh mesh, byte[]? externalResourceData = null)
    {
        // Extract indices from IndexBuffer
        uint[] indices = ExtractIndices(mesh);
        Console.WriteLine($"DEBUG: ConvertMeshToThreeJS - indices.Length={indices.Length}");

        // Extract vertex positions using VertexDataExtractor
        float[] positions = VertexDataExtractor.ExtractVertexPositions(mesh, externalResourceData);
        Console.WriteLine($"DEBUG: ConvertMeshToThreeJS - positions.Length={positions.Length}");

        // If no positions found, create placeholder
        if (positions.Length == 0)
        {
            positions = CreatePlaceholderPositions(mesh.VertexData?.VertexCount ?? 4);
        }

        // Extract normals and UVs
        float[]? normals = null;
        float[]? uvs = null;

        if (mesh.CompressedMesh != null)
        {
            var extractedNormals = VertexDataExtractor.ExtractVertexNormals(mesh, externalResourceData);
            if (extractedNormals.Length > 0)
            {
                normals = extractedNormals;
            }

            var extractedUVs = VertexDataExtractor.ExtractVertexUVs(mesh, externalResourceData);
            if (extractedUVs.Length > 0)
            {
                uvs = extractedUVs;
            }
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

        var result = new ThreeJsGeometry
        {
            Positions = positions,
            Indices = indices,
            Normals = normals,
            Uvs = uvs,
            VertexCount = (int)(mesh.VertexData?.VertexCount ?? 4),
            TriangleCount = indices.Length / 3,
            Groups = groups.Count > 0 ? groups : null
        };

        Console.WriteLine($"DEBUG: ThreeJsGeometry created - VertexCount={result.VertexCount}, TriangleCount={result.TriangleCount}, GroupsCount={groups.Count}");
        if (groups.Count > 0)
        {
            Console.WriteLine($"DEBUG: First group - Start={groups[0].Start}, Count={groups[0].Count}");
        }

        return result;
    }

    /// <summary>
    /// Creates placeholder vertex positions (simple cube) when real data unavailable.
    /// </summary>
    private static float[] CreatePlaceholderPositions(uint vertexCount)
    {
        // Return a simple cube that fits in the expected vertex count
        return new float[]
        {
            // Front face
            -0.5f, -0.5f,  0.5f,  // 0
             0.5f, -0.5f,  0.5f,  // 1
             0.5f,  0.5f,  0.5f,  // 2
            -0.5f,  0.5f,  0.5f,  // 3
            
            // Back face
            -0.5f, -0.5f, -0.5f,  // 4
            -0.5f,  0.5f, -0.5f,  // 5
             0.5f,  0.5f, -0.5f,  // 6
             0.5f, -0.5f, -0.5f,  // 7
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
