using System.IO.Compression;
using BlazorApp.Models;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;
using UnityAssetParser.Export;

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

        return await RenderFromBundleAsync(file, fileBytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ThreeJsGeometry> RenderFromBundleAsync(FileIndexItem file, byte[] bundleBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bundleBytes);
        ct.ThrowIfCancellationRequested();

        await Task.CompletedTask;

        if (!file.Renderable)
        {
            throw new InvalidOperationException($"File '{file.FileName}' is not marked as renderable.");
        }

        // Parse based on file type
        if (file.Type == FileType.UnityFS)
        {
            return ParseUnityFSBundle(bundleBytes);
        }
        else if (file.Type == FileType.SerializedFile)
        {
            return ParseSerializedFile(bundleBytes);
        }
        else
        {
            throw new InvalidDataException($"Unsupported file type: {file.Type}");
        }
    }

    /// <summary>
    /// Parses a UnityFS bundle and extracts mesh geometry.
    /// </summary>
    private ThreeJsGeometry ParseUnityFSBundle(byte[] bundleBytes)
    {
        var service = new MeshExtractionService();
        var meshes = service.ExtractMeshes(bundleBytes);
        if (meshes.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in bundle.");
        }

        var selected = SelectBestMesh(meshes);
        return ConvertMeshDtoToThreeJs(selected);
    }

    private ThreeJsGeometry ParseSerializedFile(byte[] serializedFileBytes)
    {
        var meshes = ExtractMeshesFromSerializedFile(serializedFileBytes);
        if (meshes.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in SerializedFile.");
        }

        var selected = SelectBestMesh(meshes);
        return ConvertMeshDtoToThreeJs(selected);
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

    private static List<MeshGeometryDto> ExtractMeshesFromSerializedFile(byte[] serializedFileBytes)
    {
        var serializedFile = UnityAssetParser.SerializedFile.SerializedFile.Parse(serializedFileBytes);
        var meshObjects = serializedFile.GetObjectsByClassId(RenderableDetector.RenderableClassIds.Mesh).ToList();
        if (meshObjects.Count == 0)
        {
            return new List<MeshGeometryDto>();
        }

        var version = ParseUnityVersion(serializedFile.Header.UnityVersionString);
        var isBigEndian = serializedFile.Header.Endianness != 0;

        var results = new List<MeshGeometryDto>();
        foreach (var meshObj in meshObjects)
        {
            var meshData = serializedFile.ReadObjectData(meshObj);
            var meshType = serializedFile.TypeTree.GetType(meshObj.TypeId);
            var typeTreeNodes = meshType?.Nodes;
            Mesh? mesh;
            if (typeTreeNodes == null || typeTreeNodes.Count == 0)
            {
                mesh = MeshParser.Parse(meshData.Span, version, isBigEndian, null);
            }
            else
            {
                mesh = MeshParser.ParseWithTypeTree(meshData.Span, typeTreeNodes, version, isBigEndian, null);
            }
            if (mesh == null)
            {
                continue;
            }

            var helper = new MeshHelper(mesh, version, isLittleEndian: !isBigEndian);
            helper.Process();

            var dto = new MeshGeometryDto
            {
                Name = mesh.Name,
                Positions = helper.Positions ?? Array.Empty<float>(),
                Normals = helper.Normals,
                UVs = helper.UVs,
                VertexCount = helper.VertexCount,
                Use16BitIndices = helper.Use16BitIndices
            };

            if (mesh.SubMeshes != null && mesh.SubMeshes.Length > 0)
            {
                var triangles = helper.GetTriangles();
                var indicesList = new List<uint>();
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
            else
            {
                dto.Indices = helper.Indices ?? Array.Empty<uint>();
            }

            dto.TriangleCount = dto.Indices.Length / 3;
            results.Add(dto);
        }

        return results;
    }

    private static MeshGeometryDto SelectBestMesh(IReadOnlyList<MeshGeometryDto> meshes)
    {
        return meshes
            .OrderByDescending(m => m.Positions.Length)
            .ThenByDescending(m => m.Indices.Length)
            .First();
    }

    private static ThreeJsGeometry ConvertMeshDtoToThreeJs(MeshGeometryDto dto)
    {
        List<SubMeshGroup>? groups = null;
        if (dto.Groups.Count > 0)
        {
            groups = dto.Groups.Select(g => new SubMeshGroup
            {
                Start = g.Start,
                Count = g.Count,
                MaterialIndex = g.MaterialIndex
            }).ToList();
        }

        return new ThreeJsGeometry
        {
            Positions = dto.Positions,
            Indices = dto.Indices,
            Normals = dto.Normals,
            Uvs = dto.UVs,
            VertexCount = dto.VertexCount,
            TriangleCount = dto.TriangleCount,
            Groups = groups
        };
    }

    /// <inheritdoc/>
    public async Task<GlbExportResult> RenderAsGlbAsync(FileIndexItem file, byte[] zipBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(zipBytes);
        ct.ThrowIfCancellationRequested();

        if (!file.Renderable)
        {
            throw new InvalidOperationException($"File '{file.FileName}' is not marked as renderable.");
        }

        // Extract file from ZIP (same logic as RenderAsync)
        byte[] fileBytes;
        using (var ms = new MemoryStream(zipBytes))
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
        {
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

        // Parse based on file type and extract meshes
        List<UnityAssetParser.Classes.Mesh> meshes;

        if (file.Type == FileType.UnityFS)
        {
            meshes = ParseUnityFSBundleToMeshes(fileBytes);
        }
        else if (file.Type == FileType.SerializedFile)
        {
            meshes = ParseSerializedFileToMeshes(fileBytes);
        }
        else
        {
            throw new InvalidDataException($"Unsupported file type: {file.Type}");
        }

        // Export meshes to GLB using GltfExporter
        var exporter = new GltfExporter();
        var materials = ExtractMaterials(file, fileBytes);
        var glbData = exporter.MeshesToGlb(meshes, materials);
        var (vertexCount, triangleCount) = ComputeMeshStats(meshes);

        return new GlbExportResult
        {
            GlbBytes = glbData,
            VertexCount = vertexCount,
            TriangleCount = triangleCount,
            MeshCount = meshes.Count
        };
    }

    /// <inheritdoc/>
    public async Task<GlbExportResult> RenderAsGlbFromBundleAsync(FileIndexItem file, byte[] bundleBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bundleBytes);
        ct.ThrowIfCancellationRequested();

        await Task.CompletedTask;

        if (!file.Renderable)
        {
            throw new InvalidOperationException($"File '{file.FileName}' is not marked as renderable.");
        }

        List<UnityAssetParser.Classes.Mesh> meshes;
        if (file.Type == FileType.UnityFS)
        {
            meshes = ParseUnityFSBundleToMeshes(bundleBytes);
        }
        else if (file.Type == FileType.SerializedFile)
        {
            meshes = ParseSerializedFileToMeshes(bundleBytes);
        }
        else
        {
            throw new InvalidDataException($"Unsupported file type: {file.Type}");
        }

        var exporter = new GltfExporter();
        var materials = ExtractMaterials(file, bundleBytes);
        var glbData = exporter.MeshesToGlb(meshes, materials);
        var (vertexCount, triangleCount) = ComputeMeshStats(meshes);

        return new GlbExportResult
        {
            GlbBytes = glbData,
            VertexCount = vertexCount,
            TriangleCount = triangleCount,
            MeshCount = meshes.Count
        };
    }

    /// <summary>
    /// Parses a UnityFS bundle and extracts Mesh objects.
    /// </summary>
    private List<UnityAssetParser.Classes.Mesh> ParseUnityFSBundleToMeshes(byte[] bundleBytes)
    {
        using var ms = new MemoryStream(bundleBytes);
        var parseResult = BundleFile.TryParse(ms);

        if (!parseResult.Success || parseResult.Bundle == null)
        {
            throw new InvalidDataException(
                $"Failed to parse UnityFS bundle: {string.Join(", ", parseResult.Errors)}");
        }

        var bundle = parseResult.Bundle;

        if (!TryFindSerializedFileNode(bundle, out var serializedFile, out var serializedNode, out var serializedError))
        {
            throw new InvalidDataException($"Failed to locate SerializedFile in bundle: {serializedError}");
        }

        // Find all Mesh objects (ClassID 43)
        var meshObjects = serializedFile.GetObjectsByClassId(RenderableDetector.RenderableClassIds.Mesh).ToList();

        if (meshObjects.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in bundle.");
        }

        var version = ParseUnityVersion(serializedFile.Header.UnityVersionString);
        var isBigEndian = serializedFile.Header.Endianness != 0;

        // Parse all mesh objects
        var meshes = meshObjects.Select(meshObj =>
        {
            var meshData = serializedFile.ReadObjectData(meshObj);
            var meshType = serializedFile.TypeTree.GetType(meshObj.TypeId);
            var typeTreeNodes = meshType?.Nodes;
            Mesh? mesh;
            if (typeTreeNodes == null || typeTreeNodes.Count == 0)
            {
                mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);
            }
            else
            {
                mesh = MeshParser.ParseWithTypeTree(meshData.Span, typeTreeNodes, version, isBigEndian);
            }

            if (mesh != null)
            {
                // Extract vertex attributes using MeshHelper
                var meshHelper = new MeshHelper(mesh, version, !isBigEndian, bundle.Nodes, bundle.DataRegion);
                meshHelper.Process();

                if (mesh.CompressedMesh != null)
                {
                    var cm = mesh.CompressedMesh;
                    Console.WriteLine($"DEBUG: CompressedMesh weights={cm.Weights.NumItems} boneIndices={cm.BoneIndices.NumItems} bindPoses={cm.BindPoses?.NumItems ?? 0}");
                }

                Console.WriteLine($"DEBUG: Mesh '{mesh.Name}' vtx={meshHelper.VertexCount} pos={meshHelper.Positions?.Length ?? 0} norm={meshHelper.Normals?.Length ?? 0} uv={meshHelper.UVs?.Length ?? 0} color={meshHelper.Colors?.Length ?? 0}");

                // Populate mesh attributes from extracted data
                PopulateMeshAttributesFromHelper(mesh, meshHelper);

                // Build skin weights from blend channels if present
                if (mesh.Skin == null && mesh.VariableBoneCountWeights != null && mesh.VariableBoneCountWeights.Length > 0)
                {
                    var weights = SkinningHelper.BuildWeightsFromVariableBoneCount(
                        mesh.VariableBoneCountWeights,
                        meshHelper.VertexCount);
                    if (weights != null)
                    {
                        mesh.Skin = weights;
                        Console.WriteLine($"DEBUG: Built skin weights from VariableBoneCountWeights for mesh '{mesh.Name}'");
                    }
                }

                if (SkinningHelper.TryApplyBindPoseSkinning(mesh))
                {
                    Console.WriteLine($"DEBUG: Applied bind-pose skinning to mesh '{mesh.Name}'");
                }
            }

            return mesh;
        }).Where(mesh => mesh != null).Cast<UnityAssetParser.Classes.Mesh>().ToList();

        Console.WriteLine($"DEBUG: GLB export using SerializedFile node: Path='{serializedNode.Path}', Size={serializedNode.Size}");

        return meshes;
    }

    /// <summary>
    /// Populates mesh vertex attributes (positions, normals, UVs) from MeshHelper extracted data.
    /// </summary>
    private static void PopulateMeshAttributesFromHelper(UnityAssetParser.Classes.Mesh mesh, MeshHelper meshHelper)
    {
        // Convert positions
        if (meshHelper.Positions != null && meshHelper.Positions.Length > 0)
        {
            var positions = new Vector3f[meshHelper.Positions.Length / 3];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Vector3f
                {
                    X = meshHelper.Positions[i * 3],
                    Y = meshHelper.Positions[i * 3 + 1],
                    Z = meshHelper.Positions[i * 3 + 2]
                };
            }
            mesh.Vertices = positions;
        }

        // Convert normals
        if (meshHelper.Normals != null && meshHelper.Normals.Length > 0)
        {
            var normals = new Vector3f[meshHelper.Normals.Length / 3];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = new Vector3f
                {
                    X = meshHelper.Normals[i * 3],
                    Y = meshHelper.Normals[i * 3 + 1],
                    Z = meshHelper.Normals[i * 3 + 2]
                };
            }
            mesh.Normals = normals;
        }

        // Convert UVs
        if (meshHelper.UVs != null && meshHelper.UVs.Length > 0)
        {
            var uvs = new Vector2f[meshHelper.UVs.Length / 2];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2f
                {
                    U = meshHelper.UVs[i * 2],
                    V = meshHelper.UVs[i * 2 + 1]
                };
            }
            mesh.UV = uvs;
        }

        // Convert vertex colors
        if (meshHelper.Colors != null && meshHelper.Colors.Length > 0)
        {
            var colors = new ColorRGBA[meshHelper.Colors.Length / 4];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new ColorRGBA(
                    meshHelper.Colors[i * 4],
                    meshHelper.Colors[i * 4 + 1],
                    meshHelper.Colors[i * 4 + 2],
                    meshHelper.Colors[i * 4 + 3]);
            }
            mesh.Colors = colors;
        }

        // Convert tangents
        if (meshHelper.Tangents != null && meshHelper.Tangents.Length > 0)
        {
            var tangents = new Vector4f[meshHelper.Tangents.Length / 4];
            for (int i = 0; i < tangents.Length; i++)
            {
                tangents[i] = new Vector4f(
                    meshHelper.Tangents[i * 4],
                    meshHelper.Tangents[i * 4 + 1],
                    meshHelper.Tangents[i * 4 + 2],
                    meshHelper.Tangents[i * 4 + 3]);
            }
            mesh.Tangents = tangents;
        }

        // Populate index buffer if missing, using indices from MeshHelper.
        // This ensures GLTF export does not fall back to incorrect sequential indices.
        if ((mesh.IndexBuffer == null || mesh.IndexBuffer.Length == 0) &&
            meshHelper.Indices != null && meshHelper.Indices.Length > 0)
        {
            // Convert uint[] indices to byte[] format
            // Determine format: use 32-bit if any index exceeds UInt16.MaxValue
            bool use32Bit = meshHelper.Indices.Any(idx => idx > ushort.MaxValue);

            if (use32Bit)
            {
                mesh.IndexFormat = 1; // UInt32
                mesh.IndexBuffer = new byte[meshHelper.Indices.Length * 4];
                for (int i = 0; i < meshHelper.Indices.Length; i++)
                {
                    BitConverter.GetBytes(meshHelper.Indices[i]).CopyTo(mesh.IndexBuffer, i * 4);
                }
            }
            else
            {
                mesh.IndexFormat = 0; // UInt16
                mesh.IndexBuffer = new byte[meshHelper.Indices.Length * 2];
                for (int i = 0; i < meshHelper.Indices.Length; i++)
                {
                    BitConverter.GetBytes((ushort)meshHelper.Indices[i]).CopyTo(mesh.IndexBuffer, i * 2);
                }
            }
        }
    }

    /// <summary>
    /// Parses a SerializedFile and extracts Mesh objects.
    /// </summary>
    private List<UnityAssetParser.Classes.Mesh> ParseSerializedFileToMeshes(byte[] serializedFileBytes)
    {
        var serializedFile = UnityAssetParser.SerializedFile.SerializedFile.Parse(serializedFileBytes);
        var meshObjects = serializedFile.GetObjectsByClassId(RenderableDetector.RenderableClassIds.Mesh).ToList();

        if (meshObjects.Count == 0)
        {
            throw new InvalidDataException("No Mesh objects found in SerializedFile.");
        }

        var version = ParseUnityVersion(serializedFile.Header.UnityVersionString);
        var isBigEndian = serializedFile.Header.Endianness != 0;

        var meshes = meshObjects.Select(meshObj =>
        {
            var meshData = serializedFile.ReadObjectData(meshObj);
            var meshType = serializedFile.TypeTree.GetType(meshObj.TypeId);
            var typeTreeNodes = meshType?.Nodes;
            Mesh? mesh;
            if (typeTreeNodes == null || typeTreeNodes.Count == 0)
            {
                mesh = MeshParser.Parse(meshData.Span, version, isBigEndian);
            }
            else
            {
                mesh = MeshParser.ParseWithTypeTree(meshData.Span, typeTreeNodes, version, isBigEndian);
            }

            if (mesh != null)
            {
                // Extract vertex attributes using MeshHelper
                var meshHelper = new MeshHelper(mesh, version, !isBigEndian);
                meshHelper.Process();

                // Populate mesh attributes from extracted data
                PopulateMeshAttributesFromHelper(mesh, meshHelper);
            }

            return mesh;
        }).Where(mesh => mesh != null).Cast<UnityAssetParser.Classes.Mesh>().ToList();

        return meshes;
    }

    private static bool TryFindSerializedFileNode(
        BundleFile bundle,
        out UnityAssetParser.SerializedFile.SerializedFile serializedFile,
        out NodeInfo serializedNode,
        out string? error)
    {
        serializedFile = null!;
        serializedNode = default;
        error = null;

        if (bundle.Nodes.Count == 0)
        {
            error = "Bundle has no nodes";
            return false;
        }

        var candidates = bundle.Nodes
            .Where(n => !IsResourceNode(n.Path))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = bundle.Nodes.ToList();
        }

        var matches = new List<(NodeInfo Node, UnityAssetParser.SerializedFile.SerializedFile File, int MeshCount, int ObjectCount)>();

        foreach (var node in candidates)
        {
            try
            {
                var nodeData = bundle.ExtractNode(node);
                if (UnityAssetParser.SerializedFile.SerializedFile.TryParse(nodeData.Span, out var parsed, out _))
                {
                    var meshCount = parsed!.Objects.Count(o => o.ClassId == RenderableDetector.RenderableClassIds.Mesh);
                    matches.Add((node, parsed!, meshCount, parsed!.Objects.Count));
                }
            }
            catch
            {
                // Ignore nodes that can't be parsed as SerializedFile
            }
        }

        if (matches.Count == 0)
        {
            error = "No nodes could be parsed as a SerializedFile";
            return false;
        }

        var best = matches
            .OrderByDescending(m => m.MeshCount)
            .ThenByDescending(m => m.ObjectCount)
            .First();

        serializedFile = best.File;
        serializedNode = best.Node;
        return true;
    }

    private static bool IsResourceNode(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);
    }

    private static (int vertexCount, int triangleCount) ComputeMeshStats(IReadOnlyList<UnityAssetParser.Classes.Mesh> meshes)
    {
        int vertexCount = 0;
        int triangleCount = 0;

        foreach (var mesh in meshes)
        {
            vertexCount += mesh.Vertices?.Length ?? 0;

            if (mesh.IndexBuffer == null || mesh.IndexBuffer.Length == 0)
            {
                continue;
            }

            bool use32Bit = mesh.IndexFormat == 1 || (mesh.IndexFormat == null && mesh.Use16BitIndices == false);
            int indexCount = use32Bit ? mesh.IndexBuffer.Length / 4 : mesh.IndexBuffer.Length / 2;
            triangleCount += indexCount / 3;
        }

        return (vertexCount, triangleCount);
    }

    private static List<MaterialInfo> ExtractMaterials(FileIndexItem file, byte[] assetBytes)
    {
        var extractor = new MaterialExtractionService();
        if (file.Type == FileType.UnityFS)
        {
            return extractor.ExtractMaterialsFromBundle(assetBytes);
        }

        if (file.Type == FileType.SerializedFile)
        {
            return extractor.ExtractMaterialsFromSerializedFile(assetBytes);
        }

        return new List<MaterialInfo>();
    }

}
