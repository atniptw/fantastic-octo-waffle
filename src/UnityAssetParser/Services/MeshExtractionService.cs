using UnityAssetParser.Bundle;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Services;

/// <summary>
/// Service for extracting renderable mesh geometry from Unity bundles.
/// Wires BundleFile → SerializedFile → Mesh → MeshHelper to produce DTOs.
/// </summary>
public sealed class MeshExtractionService
{
    /// <summary>
    /// Extracts all mesh geometries from a Unity bundle.
    /// </summary>
    /// <param name="bundleData">Raw bundle bytes (.hhh file)</param>
    /// <returns>List of extracted mesh geometries ready for rendering</returns>
    /// <exception cref="ArgumentNullException">If bundleData is null</exception>
    /// <exception cref="InvalidOperationException">If bundle structure is invalid</exception>
    public List<MeshGeometryDto> ExtractMeshes(byte[] bundleData)
    {
        ArgumentNullException.ThrowIfNull(bundleData);

        var results = new List<MeshGeometryDto>();

        // Step 1: Parse the bundle
        BundleFile bundle;
        using (var bundleStream = new MemoryStream(bundleData, false))
        {
            bundle = BundleFile.Parse(bundleStream);
        }

        // Step 2: Find and parse SerializedFile (Node 0)
        if (bundle.Nodes.Count == 0)
        {
            throw new InvalidOperationException("Bundle has no nodes");
        }

        var node0 = bundle.Nodes[0];
        var serializedFileData = bundle.ExtractNode(node0);

        SerializedFile.SerializedFile serializedFile;
        try
        {
            serializedFile = SerializedFile.SerializedFile.Parse(serializedFileData.Span);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse SerializedFile from Node 0: {ex.Message}", ex);
        }

        // Step 3: Find .resS resource node (Node 1) if present
        ReadOnlyMemory<byte>? resSData = null;
        if (bundle.Nodes.Count > 1)
        {
            var node1 = bundle.Nodes[1];
            // Check if this is a .resS resource file
            if (node1.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                node1.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
            {
                resSData = bundle.ExtractNode(node1);
            }
        }

        // Step 4: Find all Mesh objects (ClassID 43)
        var meshObjects = serializedFile.Objects
            .Where(obj => obj.ClassId == RenderableDetector.RenderableClassIds.Mesh)
            .ToList();

        if (meshObjects.Count == 0)
        {
            return results; // No meshes found
        }

        // Step 5: Extract geometry from each mesh
        foreach (var meshObj in meshObjects)
        {
            try
            {
                var meshDto = ExtractMeshGeometry(serializedFile, meshObj, resSData);
                if (meshDto != null)
                {
                    results.Add(meshDto);
                }
            }
            catch (Exception)
            {
                // Skip meshes that fail to extract - continue with others
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts geometry from a single Mesh object.
    /// </summary>
    private MeshGeometryDto? ExtractMeshGeometry(
        SerializedFile.SerializedFile serializedFile,
        ObjectInfo meshObj,
        ReadOnlyMemory<byte>? resSData)
    {
        // Read object data
        var objectData = serializedFile.ReadObjectData(meshObj);

        // Get Unity version from SerializedFile header
        var version = GetUnityVersion(serializedFile.Header);
        bool isBigEndian = serializedFile.Header.Endianness == 1;

        // Parse Mesh object
        var mesh = MeshParser.Parse(objectData.Span, version, isBigEndian, resSData);
        
        if (mesh == null)
        {
            // Mesh parsing not yet implemented or failed
            return null;
        }

        // Use MeshHelper to extract geometry
        var helper = new MeshHelper(mesh, version, isLittleEndian: !isBigEndian);
        
        try
        {
            helper.Process();
        }
        catch (NotImplementedException ex) when (ex.Message.Contains(".resS"))
        {
            // External streaming data not yet supported - return null
            return null;
        }

        // Convert to DTO
        var dto = new MeshGeometryDto
        {
            Name = mesh.Name,
            Positions = helper.Positions ?? Array.Empty<float>(),
            Indices = helper.Indices ?? Array.Empty<uint>(),
            Normals = helper.Normals,
            UVs = helper.UVs,
            VertexCount = helper.VertexCount,
            Use16BitIndices = helper.Use16BitIndices
        };

        // Calculate triangle count
        dto.TriangleCount = dto.Indices.Length / 3;

        // Extract submesh groups
        if (mesh.SubMeshes != null && mesh.SubMeshes.Length > 0)
        {
            try
            {
                var triangles = helper.GetTriangles();
                int indexOffset = 0;
                
                for (int i = 0; i < triangles.Count; i++)
                {
                    var submeshTriangles = triangles[i];
                    var indexCount = submeshTriangles.Count * 3;
                    
                    dto.Groups.Add(new MeshGeometryDto.SubMeshGroup
                    {
                        Start = indexOffset,
                        Count = indexCount,
                        MaterialIndex = i
                    });
                    
                    indexOffset += indexCount;
                }
            }
            catch (Exception)
            {
                // Skip submesh group extraction on error - continue without groups
            }
        }

        return dto;
    }

    /// <summary>
    /// Extracts Unity version from SerializedFile header.
    /// Returns a version tuple (major, minor, patch, type) for use with MeshHelper.
    /// </summary>
    private static (int, int, int, int) GetUnityVersion(SerializedFileHeader header)
    {
        // Try to parse Unity version string if available
        if (!string.IsNullOrEmpty(header.UnityVersionString))
        {
            var parts = header.UnityVersionString.Split('.');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out int major) && 
                    int.TryParse(parts[1], out int minor))
                {
                    int patch = 0;
                    if (parts.Length >= 3)
                    {
                        // Extract patch number (may have alpha/beta suffix)
                        var patchStr = new string(parts[2].TakeWhile(char.IsDigit).ToArray());
                        int.TryParse(patchStr, out patch);
                    }
                    
                    return (major, minor, patch, 0);
                }
            }
        }

        // Fallback: estimate version from SerializedFile format version
        // This is a rough approximation based on common version mappings
        return header.Version switch
        {
            >= 22 => (2022, 1, 0, 0),  // Unity 2022+
            >= 20 => (2021, 1, 0, 0),  // Unity 2021
            >= 19 => (2020, 1, 0, 0),  // Unity 2020
            >= 17 => (2019, 1, 0, 0),  // Unity 2019
            >= 14 => (2018, 1, 0, 0),  // Unity 2018
            _ => (2017, 1, 0, 0)       // Unity 2017 or earlier
        };
    }
}
