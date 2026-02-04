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
        Console.WriteLine($"DEBUG: Bundle has {bundle.Nodes.Count} nodes");
        for (int i = 0; i < bundle.Nodes.Count; i++)
        {
            var n = bundle.Nodes[i];
            Console.WriteLine($"DEBUG: Node[{i}] Path='{n.Path}', Size={n.Size}, Flags={n.Flags}");
        }

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
        // Debug: summarize object ClassIds present
        var classIdCounts = serializedFile.Objects
            .GroupBy(o => o.ClassId)
            .ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine($"DEBUG: SerializedFile contains {serializedFile.Objects.Count} objects");
        foreach (var kv in classIdCounts.OrderBy(kv => kv.Key))
        {
            Console.WriteLine($"DEBUG: ClassID {kv.Key} count={kv.Value}");
        }
        
        // Debug: List all objects to find the Mesh
        Console.WriteLine($"DEBUG: Object table:");
        for (int i = 0; i < Math.Min(10, serializedFile.Objects.Count); i++)
        {
            var obj = serializedFile.Objects[i];
            Console.WriteLine($"DEBUG:   [{i}] PathId={obj.PathId}, ClassId={obj.ClassId}, ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}");
        }
        var lastObjIndex = serializedFile.Objects.Count - 1;
        if (lastObjIndex >= 10)
        {
            var obj = serializedFile.Objects[lastObjIndex];
            Console.WriteLine($"DEBUG:   ... [{lastObjIndex}] PathId={obj.PathId}, ClassId={obj.ClassId}, ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}");
        }

        var meshObjects = serializedFile.Objects
            .Where(obj => obj.ClassId == RenderableDetector.RenderableClassIds.Mesh)
            .ToList();

        if (meshObjects.Count == 0)
        {
            return results; // No meshes found
        }

        Console.WriteLine($"DEBUG: Found {meshObjects.Count} Mesh objects (ClassID 43)");
        foreach (var meshObj in meshObjects)
        {
            Console.WriteLine($"DEBUG: Mesh PathId={meshObj.PathId}, ByteStart={meshObj.ByteStart}, ByteSize={meshObj.ByteSize}, TypeId={meshObj.TypeId}");
        }

        // Step 5: Extract geometry from each mesh
        foreach (var meshObj in meshObjects)
        {
            try
            {
                var meshDto = ExtractMeshGeometry(serializedFile, meshObj, bundle, resSData);
                if (meshDto != null)
                {
                    results.Add(meshDto);
                }
            }
            catch (Exception ex)
            {
                // Log and skip meshes that fail to extract - continue with others
                // TODO: Replace with proper logging when ILogger is available
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: Failed to extract mesh PathId={meshObj.PathId}, ClassId={meshObj.ClassId}: {ex.Message}");
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
        BundleFile bundle,
        ReadOnlyMemory<byte>? resSData)
    {
        // Read object data
        var objectData = serializedFile.ReadObjectData(meshObj);

        // Get Unity version from SerializedFile header
        var version = GetUnityVersion(serializedFile.Header);
        bool isBigEndian = serializedFile.Header.Endianness == 1;

        // Get TypeTree nodes for this object's type
        var meshType = serializedFile.TypeTree.GetType(meshObj.TypeId);
        var typeTreeNodes = meshType?.Nodes;

        // Parse Mesh object using TypeTree (dynamic field order per Unity version)
        // TypeTree encodes the actual field sequence for this version, so we use it rather than hardcoding
        Console.WriteLine($"DEBUG: ExtractMeshGeometry - objectData.Length={objectData.Length}, meshObj.ByteSize={meshObj.ByteSize}");
        
        // CRITICAL: Ensure objectData matches ByteSize from ObjectInfo
        // Unity object data should be exactly ByteSize bytes as specified in the object table
        if (objectData.Length != meshObj.ByteSize)
        {
            Console.WriteLine($"WARNING: objectData.Length ({objectData.Length}) != meshObj.ByteSize ({meshObj.ByteSize}) - truncating to ByteSize");
            objectData = objectData.Slice(0, (int)meshObj.ByteSize);
        }
        
        Mesh? mesh;
        try
        {
            // TypeTree-driven parsing is the only supported approach.
            // TypeTree provides version-specific field ordering.
            if (typeTreeNodes == null || typeTreeNodes.Count == 0)
            {
                Console.WriteLine($"DEBUG: No TypeTree available for Mesh object, skipping");
                return null;
            }

            Console.WriteLine($"DEBUG: Using TypeTree parsing ({typeTreeNodes.Count} nodes)");
            mesh = MeshParser.ParseWithTypeTree(objectData.Span, typeTreeNodes, version, isBigEndian, resSData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: MeshParser failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        if (mesh == null)
        {
            // Mesh parsing not yet implemented or failed
            Console.WriteLine($"DEBUG: MeshParser returned null for mesh PathId={meshObj.PathId}");
            return null;
        }

        Console.WriteLine($"DEBUG: MeshParser returned mesh: Name='{mesh.Name}', SubMeshes={mesh.SubMeshes?.Length ?? 0}");

        // Use MeshHelper to extract geometry
        // Pass bundle nodes and data region for StreamingInfo resolution
        var helper = new MeshHelper(
            mesh,
            version,
            isLittleEndian: !isBigEndian,
            nodes: bundle.Nodes,
            dataRegion: bundle.DataRegion);

        try
        {
            helper.Process();
        }
        catch (NotImplementedException ex)
        {
            // External streaming data or other features not yet supported - return null
            // This is expected during the scaffold phase while MeshParser is being implemented
            Console.WriteLine($"DEBUG: MeshHelper.Process() threw NotImplementedException: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Any other exception from MeshHelper
            Console.WriteLine($"DEBUG: MeshHelper.Process() threw {ex.GetType().Name}: {ex.Message}");
            // For now, if we can't process the mesh (likely due to external data),
            // still return basic metadata so we can at least see the mesh exists
            if (ex is InvalidOperationException && ex.Message.Contains("VertexData"))
            {
                Console.WriteLine($"DEBUG: Mesh '{mesh.Name}' has no inline VertexData (likely in external .resS file)");
                // Return a basic DTO with mesh metadata only
                return new MeshGeometryDto
                {
                    Name = mesh.Name,
                    Positions = Array.Empty<float>(),
                    Normals = null,
                    UVs = null,
                    VertexCount = 0,
                    Use16BitIndices = false,
                    Indices = Array.Empty<uint>(),
                    Groups = new List<MeshGeometryDto.SubMeshGroup>()
                };
            }
            // Rethrow other exceptions for debugging
            throw;
        }

        // Convert to DTO
        var dto = new MeshGeometryDto
        {
            Name = mesh.Name,
            Positions = helper.Positions ?? Array.Empty<float>(),
            Normals = helper.Normals,
            UVs = helper.UVs,
            VertexCount = helper.VertexCount,
            Use16BitIndices = helper.Use16BitIndices
        };

        // Extract submesh groups and convert triangles to flat index array
        // Note: GetTriangles() may convert triangle strips/quads to standard triangles,
        // so we use its output for both Groups and Indices to ensure consistency
        if (mesh.SubMeshes != null && mesh.SubMeshes.Length > 0)
        {
            try
            {
                var triangles = helper.GetTriangles();
                var indicesList = new List<uint>();
                int indexOffset = 0;

                for (int i = 0; i < triangles.Count; i++)
                {
                    var submeshTriangles = triangles[i];
                    var indexCount = submeshTriangles.Count * 3;

                    // Add group metadata
                    dto.Groups.Add(new MeshGeometryDto.SubMeshGroup
                    {
                        Start = indexOffset,
                        Count = indexCount,
                        MaterialIndex = i
                    });

                    // Flatten triangles to index array
                    foreach (var tri in submeshTriangles)
                    {
                        indicesList.Add(tri.Item1);
                        indicesList.Add(tri.Item2);
                        indicesList.Add(tri.Item3);
                    }

                    indexOffset += indexCount;
                }

                dto.Indices = indicesList.ToArray();
            }
            catch (Exception ex)
            {
                // Log and fall back to raw IndexBuffer if submesh extraction fails
                // TODO: Replace with proper logging when ILogger is available
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: Failed to extract submesh groups for mesh '{mesh.Name}': {ex.Message}");
                dto.Indices = helper.Indices ?? Array.Empty<uint>();
            }
        }
        else
        {
            // No submeshes - use raw indices from helper
            dto.Indices = helper.Indices ?? Array.Empty<uint>();
        }

        // Calculate triangle count
        dto.TriangleCount = dto.Indices.Length / 3;

        return dto;
    }

    /// <summary>
    /// Extracts Unity version from SerializedFile header.
    /// Returns a version tuple (major, minor, patch, type) for use with MeshHelper.
    /// </summary>
    /// <remarks>
    /// WARNING: The fallback version estimation is approximate and may produce incorrect
    /// results for version-specific parsing logic. When UnityVersionString is unavailable,
    /// the SerializedFile format version is used to estimate the Unity engine version, but
    /// this mapping is not precise and can lead to parsing errors for assets created with
    /// Unity versions that don't align with typical format version progressions.
    /// 
    /// Consumers should be aware that mesh parsing accuracy depends on correct version
    /// detection, and bundles without embedded version strings may fail to parse correctly.
    /// </remarks>
    private static (int, int, int, int) GetUnityVersion(SerializedFileHeader header)
    {
        // Try to parse Unity version string if available
        if (!string.IsNullOrEmpty(header.UnityVersionString))
        {
            var parts = header.UnityVersionString.Split('.');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out int major) &&
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
