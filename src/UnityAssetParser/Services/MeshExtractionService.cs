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
            catch (Exception ex)
            {
                // Log and skip this mesh, continue with others
                Console.WriteLine($"Warning: Failed to extract mesh PathId={meshObj.PathId}: {ex.Message}");
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

        // Parse Mesh (simplified - in a real implementation, we'd need a full Mesh parser)
        // For now, we'll create a minimal Mesh object and use MeshHelper
        var mesh = ParseMeshObject(objectData.Span, resSData);
        
        if (mesh == null)
        {
            return null;
        }

        // Use MeshHelper to extract geometry
        // TODO: Get actual Unity version from SerializedFile header
        var version = (2020, 3, 0, 0); // Default version for now
        var helper = new MeshHelper(mesh, version, isLittleEndian: true);
        
        try
        {
            helper.Process();
        }
        catch (NotImplementedException ex) when (ex.Message.Contains(".resS"))
        {
            // External streaming data not yet supported
            Console.WriteLine($"Warning: Mesh uses external .resS streaming data (not yet supported)");
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
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to extract submesh groups: {ex.Message}");
                // Continue without groups
            }
        }

        return dto;
    }

    /// <summary>
    /// Parses a Mesh object from SerializedFile object data.
    /// This is a simplified parser that extracts the minimum fields needed for MeshHelper.
    /// </summary>
    private Mesh? ParseMeshObject(ReadOnlySpan<byte> objectData, ReadOnlyMemory<byte>? resSData)
    {
        // TODO: Implement full Mesh parser
        // For now, return null to indicate we can't parse yet
        // This will be implemented in a follow-up commit
        
        // A full implementation would:
        // 1. Use EndianBinaryReader to read Mesh fields in order
        // 2. Handle 4-byte alignment after byte arrays and bool triplets
        // 3. Parse VertexData structure with channels/streams
        // 4. Parse IndexBuffer
        // 5. Parse SubMeshes
        // 6. Handle StreamingInfo for external .resS
        // 7. Handle CompressedMesh
        
        return null;
    }
}
