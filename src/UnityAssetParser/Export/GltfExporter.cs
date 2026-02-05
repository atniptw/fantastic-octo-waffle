using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using UnityAssetParser.Classes;

namespace UnityAssetParser.Export;

using UnityMesh = UnityAssetParser.Classes.Mesh;

/// <summary>
/// Exports Unity Mesh objects to glTF 2.0 format (GLB binary).
/// 
/// This exporter uses SharpGLTF.Toolkit's MeshBuilder pattern (recommended approach).
/// Converts parsed Mesh objects from Unity asset bundles into glTF/GLB format
/// suitable for direct consumption by Three.js and other glTF consumers.
/// 
/// Key behaviors:
/// - Each Mesh becomes a glTF Mesh via MeshBuilder
/// - Vertices, normals, UVs are combined into typed vertex structs
/// - Triangles are added via AddTriangle() API
/// - Materials use default PBR material (non-metallic, full roughness)
/// 
/// Note: Material metadata is not exported in this phase (Phase 2).
/// Future enhancement: Extract PBR properties from Unity materials if available.
/// </summary>
public class GltfExporter
{
    /// <summary>
    /// Converts a list of parsed Mesh objects to glTF ModelRoot using MeshBuilder pattern.
    /// 
    /// Each Unity Mesh is converted to a MeshBuilder with appropriate vertex types
    /// based on available attributes (positions, normals, UVs). The MeshBuilders are
    /// then assembled into a glTF ModelRoot via CreateMeshes().
    /// </summary>
    /// <param name="meshes">Parsed Unity Mesh objects</param>
    /// <returns>glTF ModelRoot ready for export</returns>
    /// <exception cref="ArgumentNullException">Thrown if meshes is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if mesh data is malformed</exception>
    public ModelRoot MeshesToGltf(List<UnityMesh> meshes)
    {
        if (meshes == null)
        {
            throw new ArgumentNullException(nameof(meshes));
        }

        // Create default material
        var material = new MaterialBuilder("default")
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithMetallicRoughness(0.0f, 1.0f); // Non-metallic, full roughness

        // Convert each Unity mesh to MeshBuilder
        var meshBuilders = new List<IMeshBuilder<MaterialBuilder>>();

        foreach (var mesh in meshes)
        {
            if (mesh == null)
            {
                continue;
            }

            var meshBuilder = ConvertUnityMesh(mesh, material);
            meshBuilders.Add(meshBuilder);
        }

        // Create glTF model from MeshBuilders
        var model = ModelRoot.CreateModel();
        if (meshBuilders.Count > 0)
        {
            model.CreateMeshes(meshBuilders.ToArray());
        }

        return model;
    }

    /// <summary>
    /// Converts a Unity Mesh to a MeshBuilder with appropriate vertex types.
    /// 
    /// Selects vertex format based on available attributes:
    /// - Position + Normal + UV → VertexPositionNormal + VertexTexture1
    /// - Position + Normal → VertexPositionNormal only
    /// - Position + UV → VertexPosition + VertexTexture1
    /// - Position only → VertexPosition only
    /// </summary>
    /// <param name="unityMesh">Source Unity Mesh</param>
    /// <param name="material">Material to assign to primitives</param>
    /// <returns>MeshBuilder ready for glTF export</returns>
    private IMeshBuilder<MaterialBuilder> ConvertUnityMesh(UnityMesh unityMesh, MaterialBuilder material)
    {
        // Validate mesh has required vertex data
        if (unityMesh.Vertices == null || unityMesh.Vertices.Length == 0)
        {
            throw new InvalidOperationException(
                $"Mesh '{unityMesh.Name}' has no vertices");
        }

        var vertices = unityMesh.Vertices;
        var normals = unityMesh.Normals;
        var uvs = unityMesh.UV;
        var indices = ExtractIndices(unityMesh);

        bool hasNormals = normals != null && normals.Length == vertices.Length;
        bool hasUVs = uvs != null && uvs.Length == vertices.Length;

        // Select appropriate vertex format and build mesh
        if (hasNormals && hasUVs)
        {
            return BuildMeshWithNormalsAndUVs(unityMesh.Name ?? "Mesh", vertices, normals, uvs, indices, material);
        }
        else if (hasNormals)
        {
            return BuildMeshWithNormals(unityMesh.Name ?? "Mesh", vertices, normals, indices, material);
        }
        else if (hasUVs)
        {
            return BuildMeshWithUVs(unityMesh.Name ?? "Mesh", vertices, uvs, indices, material);
        }
        else
        {
            return BuildMeshPositionsOnly(unityMesh.Name ?? "Mesh", vertices, indices, material);
        }
    }

    /// <summary>
    /// Extracts indices from Unity mesh, handling both IndexBuffer and fallback scenarios.
    /// </summary>
    private int[] ExtractIndices(UnityMesh mesh)
    {
        if (mesh.IndexBuffer != null && mesh.IndexBuffer.Length > 0)
        {
            return ExtractIndicesFromBuffer(mesh.IndexBuffer);
        }
        else
        {
            // Fallback: create sequential indices (0, 1, 2, ...)
            return Enumerable.Range(0, mesh.Vertices!.Length).ToArray();
        }
    }

    /// <summary>
    /// Extracts int array from raw index buffer bytes.
    /// 
    /// Assumes 32-bit or 16-bit indices based on buffer size.
    /// Unity typically uses UInt16 or UInt32 for indices.
    /// </summary>
    private int[] ExtractIndicesFromBuffer(byte[] buffer)
    {
        // Assume 32-bit indices if divisible by 4, otherwise 16-bit
        if (buffer.Length % 4 == 0)
        {
            var uint32Count = buffer.Length / 4;
            var result = new int[uint32Count];
            for (int i = 0; i < uint32Count; i++)
            {
                result[i] = (int)BitConverter.ToUInt32(buffer, i * 4);
            }
            return result;
        }
        else if (buffer.Length % 2 == 0)
        {
            var uint16Count = buffer.Length / 2;
            var result = new int[uint16Count];
            for (int i = 0; i < uint16Count; i++)
            {
                result[i] = BitConverter.ToUInt16(buffer, i * 2);
            }
            return result;
        }

        throw new InvalidOperationException(
            $"Index buffer size {buffer.Length} is not a multiple of 2 or 4");
    }

    /// <summary>
    /// Builds mesh with Position + Normal + UV attributes.
    /// Uses MeshBuilder with VertexPositionNormal (geometry) and VertexTexture1 (material).
    /// </summary>
    private IMeshBuilder<MaterialBuilder> BuildMeshWithNormalsAndUVs(
        string name, Vector3f[] positions, Vector3f[] normals, Vector2f[] uvs,
        int[] indices, MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        // Add triangles (glTF uses counter-clockwise winding)
        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = (
                new VertexPositionNormal(
                    new Vector3(positions[i0].X, positions[i0].Y, positions[i0].Z),
                    new Vector3(normals[i0].X, normals[i0].Y, normals[i0].Z)),
                new VertexTexture1(new Vector2(uvs[i0].U, uvs[i0].V))
            );
            var v1 = (
                new VertexPositionNormal(
                    new Vector3(positions[i1].X, positions[i1].Y, positions[i1].Z),
                    new Vector3(normals[i1].X, normals[i1].Y, normals[i1].Z)),
                new VertexTexture1(new Vector2(uvs[i1].U, uvs[i1].V))
            );
            var v2 = (
                new VertexPositionNormal(
                    new Vector3(positions[i2].X, positions[i2].Y, positions[i2].Z),
                    new Vector3(normals[i2].X, normals[i2].Y, normals[i2].Z)),
                new VertexTexture1(new Vector2(uvs[i2].U, uvs[i2].V))
            );

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    /// <summary>
    /// Builds mesh with Position + Normal attributes only.
    /// Uses MeshBuilder with VertexPositionNormal (geometry) and VertexEmpty (no material attributes).
    /// </summary>
    private IMeshBuilder<MaterialBuilder> BuildMeshWithNormals(
        string name, Vector3f[] positions, Vector3f[] normals, int[] indices, MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexPositionNormal(
                new Vector3(positions[i0].X, positions[i0].Y, positions[i0].Z),
                new Vector3(normals[i0].X, normals[i0].Y, normals[i0].Z));
            var v1 = new VertexPositionNormal(
                new Vector3(positions[i1].X, positions[i1].Y, positions[i1].Z),
                new Vector3(normals[i1].X, normals[i1].Y, normals[i1].Z));
            var v2 = new VertexPositionNormal(
                new Vector3(positions[i2].X, positions[i2].Y, positions[i2].Z),
                new Vector3(normals[i2].X, normals[i2].Y, normals[i2].Z));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    /// <summary>
    /// Builds mesh with Position + UV attributes.
    /// Uses MeshBuilder with VertexPosition (geometry) and VertexTexture1 (material).
    /// </summary>
    private IMeshBuilder<MaterialBuilder> BuildMeshWithUVs(
        string name, Vector3f[] positions, Vector2f[] uvs, int[] indices, MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPosition, VertexTexture1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = (
                new VertexPosition(new Vector3(positions[i0].X, positions[i0].Y, positions[i0].Z)),
                new VertexTexture1(new Vector2(uvs[i0].U, uvs[i0].V))
            );
            var v1 = (
                new VertexPosition(new Vector3(positions[i1].X, positions[i1].Y, positions[i1].Z)),
                new VertexTexture1(new Vector2(uvs[i1].U, uvs[i1].V))
            );
            var v2 = (
                new VertexPosition(new Vector3(positions[i2].X, positions[i2].Y, positions[i2].Z)),
                new VertexTexture1(new Vector2(uvs[i2].U, uvs[i2].V))
            );

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    /// <summary>
    /// Builds mesh with Position attribute only.
    /// Uses MeshBuilder with VertexPosition (geometry) and VertexEmpty for both material and skinning.
    /// </summary>
    private IMeshBuilder<MaterialBuilder> BuildMeshPositionsOnly(
        string name, Vector3f[] positions, int[] indices, MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPosition, VertexEmpty, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexPosition(new Vector3(positions[i0].X, positions[i0].Y, positions[i0].Z));
            var v1 = new VertexPosition(new Vector3(positions[i1].X, positions[i1].Y, positions[i1].Z));
            var v2 = new VertexPosition(new Vector3(positions[i2].X, positions[i2].Y, positions[i2].Z));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    /// <summary>
    /// Exports glTF ModelRoot to GLB binary format.
    /// 
    /// GLB (glTF Binary) is a single-file format containing all geometry,
    /// materials, and metadata in an optimized binary layout.
    /// </summary>
    /// <param name="model">glTF ModelRoot to export</param>
    /// <returns>GLB binary data as byte array</returns>
    /// <exception cref="ArgumentNullException">Thrown if model is null</exception>
    /// <exception cref="IOException">Thrown if export fails</exception>
    public byte[] ExportToGlb(ModelRoot model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        try
        {
            using var memoryStream = new MemoryStream();
            model.WriteGLB(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new IOException("Failed to export model to GLB format", ex);
        }
    }

    /// <summary>
    /// Convenience method: Meshes → glTF → GLB in one call.
    /// 
    /// This is the typical entry point for exporting a batch of meshes.
    /// Uses the Toolkit MeshBuilder pattern throughout.
    /// </summary>
    /// <param name="meshes">Parsed Unity Mesh objects</param>
    /// <returns>GLB binary data</returns>
    public byte[] MeshesToGlb(List<UnityMesh> meshes)
    {
        var model = MeshesToGltf(meshes);
        return ExportToGlb(model);
    }
}
