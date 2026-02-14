using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using SharpGLTF.Scenes;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;

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
        var material = CreateMaterialBuilder(null);

        // Convert each Unity mesh to MeshBuilder
        var meshBuilders = new List<IMeshBuilder<MaterialBuilder>>();

        foreach (var mesh in meshes)
        {
            if (mesh == null)
            {
                continue;
            }

            foreach (var meshBuilder in ConvertUnityMesh(mesh, new List<MaterialBuilder> { material }))
            {
                meshBuilders.Add(meshBuilder);
            }
        }

        // Create glTF model from MeshBuilders
        if (meshBuilders.Count > 0)
        {
            var scene = new SceneBuilder("Scene");
            foreach (var meshBuilder in meshBuilders)
            {
                scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
            }

            return scene.ToGltf2();
        }

        return ModelRoot.CreateModel();
    }

    /// <summary>
    /// Converts meshes to glTF with a single shared material override.
    /// </summary>
    public ModelRoot MeshesToGltf(List<UnityMesh> meshes, MaterialInfo? materialInfo)
    {
        if (meshes == null)
        {
            throw new ArgumentNullException(nameof(meshes));
        }

        var material = CreateMaterialBuilder(materialInfo);
        var meshBuilders = new List<IMeshBuilder<MaterialBuilder>>();

        foreach (var mesh in meshes)
        {
            if (mesh == null)
            {
                continue;
            }

            foreach (var meshBuilder in ConvertUnityMesh(mesh, new List<MaterialBuilder> { material }))
            {
                meshBuilders.Add(meshBuilder);
            }
        }

        if (meshBuilders.Count > 0)
        {
            var scene = new SceneBuilder("Scene");
            foreach (var meshBuilder in meshBuilders)
            {
                scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
            }

            return scene.ToGltf2();
        }

        return ModelRoot.CreateModel();
    }

    /// <summary>
    /// Converts meshes to glTF using a material list (submesh mapping by index).
    /// </summary>
    public ModelRoot MeshesToGltf(List<UnityMesh> meshes, IReadOnlyList<MaterialInfo>? materials)
    {
        if (meshes == null)
        {
            throw new ArgumentNullException(nameof(meshes));
        }

        var materialBuilders = CreateMaterialBuilders(materials);
        var meshBuilders = new List<IMeshBuilder<MaterialBuilder>>();

        foreach (var mesh in meshes)
        {
            if (mesh == null)
            {
                continue;
            }

            foreach (var meshBuilder in ConvertUnityMesh(mesh, materialBuilders))
            {
                meshBuilders.Add(meshBuilder);
            }
        }

        if (meshBuilders.Count > 0)
        {
            var scene = new SceneBuilder("Scene");
            foreach (var meshBuilder in meshBuilders)
            {
                scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
            }

            return scene.ToGltf2();
        }

        return ModelRoot.CreateModel();
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
    private IEnumerable<IMeshBuilder<MaterialBuilder>> ConvertUnityMesh(UnityMesh unityMesh, IReadOnlyList<MaterialBuilder> materials)
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
        var colors = unityMesh.Colors;
        var indices = ExtractIndices(unityMesh);
        bool use32Bit = unityMesh.IndexFormat == 1 || (unityMesh.IndexFormat == null && unityMesh.Use16BitIndices == false);

        bool hasNormals = normals != null && normals.Length == vertices.Length;
        bool hasUVs = uvs != null && uvs.Length == vertices.Length;
        bool hasColors = colors != null && colors.Length == vertices.Length;

        if (unityMesh.SubMeshes != null &&
            unityMesh.SubMeshes.Length > 0 &&
            materials.Count >= unityMesh.SubMeshes.Length)
        {
            var builders = new List<IMeshBuilder<MaterialBuilder>>();
            for (int i = 0; i < unityMesh.SubMeshes.Length; i++)
            {
                var submesh = unityMesh.SubMeshes[i];
                var material = materials[i];
                var subIndices = SliceIndices(indices, submesh, use32Bit);
                var name = $"{unityMesh.Name ?? "Mesh"}_Sub{i}";

                builders.Add(BuildMeshWithAvailableAttributes(
                    name, vertices, normals, uvs, colors, subIndices, material, hasNormals, hasUVs, hasColors));
            }
            return builders;
        }

        return new[]
        {
            BuildMeshWithAvailableAttributes(
                unityMesh.Name ?? "Mesh",
                vertices,
                normals,
                uvs,
                colors,
                indices,
                materials.Count > 0 ? materials[0] : CreateMaterialBuilder(null),
                hasNormals,
                hasUVs,
                hasColors)
        };
    }

    /// <summary>
    /// Extracts indices from Unity mesh, handling both IndexBuffer and fallback scenarios.
    /// </summary>
    private int[] ExtractIndices(UnityMesh mesh)
    {
        if (mesh.IndexBuffer != null && mesh.IndexBuffer.Length > 0)
        {
            return ExtractIndicesFromBuffer(mesh.IndexBuffer, mesh.IndexFormat);
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
    /// Uses IndexFormat field to determine 16-bit vs 32-bit indices.
    /// Unity: IndexFormat 0 = UInt16, 1 = UInt32.
    /// Throws InvalidOperationException for unknown IndexFormat values.
    /// Fallback to size-based heuristic if IndexFormat is null.
    /// </summary>
    private int[] ExtractIndicesFromBuffer(byte[] buffer, int? indexFormat)
    {
        // Use IndexFormat field if available (0 = UInt16, 1 = UInt32)
        if (indexFormat.HasValue)
        {
            if (indexFormat.Value == 0)
            {
                // 16-bit indices - validate buffer length
                if (buffer.Length % 2 != 0)
                {
                    throw new InvalidOperationException(
                        $"Index buffer length {buffer.Length} is not a multiple of 2 for UInt16 indices (IndexFormat=0)");
                }

                var uint16Count = buffer.Length / 2;
                var result = new int[uint16Count];
                for (int i = 0; i < uint16Count; i++)
                {
                    result[i] = BitConverter.ToUInt16(buffer, i * 2);
                }
                return result;
            }
            else if (indexFormat.Value == 1)
            {
                // 32-bit indices - validate buffer length
                if (buffer.Length % 4 != 0)
                {
                    throw new InvalidOperationException(
                        $"Index buffer length {buffer.Length} is not a multiple of 4 for UInt32 indices (IndexFormat=1)");
                }

                var uint32Count = buffer.Length / 4;
                var result = new int[uint32Count];
                for (int i = 0; i < uint32Count; i++)
                {
                    result[i] = (int)BitConverter.ToUInt32(buffer, i * 4);
                }
                return result;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown IndexFormat value '{indexFormat.Value}'. Expected 0 (UInt16) or 1 (UInt32).");
            }
        }

        // Fallback: Assume 32-bit indices if divisible by 4, otherwise 16-bit
        // This heuristic fails when buffer size is ambiguous (e.g., 936 bytes could be 234 uint32 or 468 uint16)
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

    /// <summary>
    /// Convenience method: Meshes → glTF → GLB with a shared material override.
    /// </summary>
    public byte[] MeshesToGlb(List<UnityMesh> meshes, MaterialInfo? materialInfo)
    {
        var model = MeshesToGltf(meshes, materialInfo);
        return ExportToGlb(model);
    }

    /// <summary>
    /// Convenience method: Meshes → glTF → GLB with a material list.
    /// </summary>
    public byte[] MeshesToGlb(List<UnityMesh> meshes, IReadOnlyList<MaterialInfo>? materials)
    {
        var model = MeshesToGltf(meshes, materials);
        return ExportToGlb(model);
    }

    private static MaterialBuilder CreateMaterialBuilder(MaterialInfo? materialInfo)
    {
        var name = materialInfo?.Name ?? "default";
        var defaultBaseColor = new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
        var material = new MaterialBuilder(name)
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithMetallicRoughness(0.0f, 1.0f);

        if (materialInfo != null)
        {
            var baseColor = materialInfo.BaseColor;
            if (materialInfo.BaseColorTexture == null && IsWhite(baseColor))
            {
                baseColor = defaultBaseColor;
            }

            material.WithChannelParam(KnownChannel.BaseColor, baseColor);

            if (materialInfo.BaseColorTexture != null &&
                materialInfo.BaseColorTexture.Rgba32.Length > 0)
            {
                var pngBytes = PngWriter.EncodeRgba32(
                    materialInfo.BaseColorTexture.Width,
                    materialInfo.BaseColorTexture.Height,
                    materialInfo.BaseColorTexture.Rgba32);

                var image = ImageBuilder.From(pngBytes, "image/png");
                material.WithChannelImage(KnownChannel.BaseColor, image);
            }
        }
        else
        {
            material.WithChannelParam(KnownChannel.BaseColor, defaultBaseColor);
        }

        return material;
    }

    private static bool IsWhite(Vector4 color)
    {
        const float epsilon = 0.0001f;
        return Math.Abs(color.X - 1f) < epsilon &&
               Math.Abs(color.Y - 1f) < epsilon &&
               Math.Abs(color.Z - 1f) < epsilon &&
               Math.Abs(color.W - 1f) < epsilon;
    }

    private static IReadOnlyList<MaterialBuilder> CreateMaterialBuilders(IReadOnlyList<MaterialInfo>? materials)
    {
        if (materials == null || materials.Count == 0)
        {
            return new List<MaterialBuilder> { CreateMaterialBuilder(null) };
        }

        var list = new List<MaterialBuilder>();
        foreach (var material in materials)
        {
            list.Add(CreateMaterialBuilder(material));
        }
        return list;
    }

    private static int[] SliceIndices(int[] indices, SubMesh submesh, bool use32Bit)
    {
        if (indices.Length == 0)
        {
            return indices;
        }

        int bytesPerIndex = use32Bit ? 4 : 2;
        int start = (int)(submesh.FirstByte / (uint)bytesPerIndex);
        int count = (int)submesh.IndexCount;
        if (start < 0 || start >= indices.Length || count <= 0)
        {
            return Array.Empty<int>();
        }

        int maxCount = Math.Min(count, indices.Length - start);
        var slice = new int[maxCount];
        int baseVertex = (int)submesh.BaseVertex;
        for (int i = 0; i < maxCount; i++)
        {
            slice[i] = indices[start + i] + baseVertex;
        }

        return slice;
    }

    private IMeshBuilder<MaterialBuilder> BuildMeshWithAvailableAttributes(
        string name,
        Vector3f[] positions,
        Vector3f[]? normals,
        Vector2f[]? uvs,
        ColorRGBA[]? colors,
        int[] indices,
        MaterialBuilder material,
        bool hasNormals,
        bool hasUVs,
        bool hasColors)
    {
        if (hasNormals && hasUVs && hasColors && normals != null && uvs != null && colors != null)
        {
            return BuildMeshWithNormalsUVsAndColors(name, positions, normals, uvs, colors, indices, material);
        }
        else if (hasNormals && hasUVs && normals != null && uvs != null)
        {
            return BuildMeshWithNormalsAndUVs(name, positions, normals, uvs, indices, material);
        }
        else if (hasNormals && hasColors && normals != null && colors != null)
        {
            return BuildMeshWithNormalsAndColors(name, positions, normals, colors, indices, material);
        }
        else if (hasNormals && normals != null)
        {
            return BuildMeshWithNormals(name, positions, normals, indices, material);
        }
        else if (hasUVs && hasColors && uvs != null && colors != null)
        {
            return BuildMeshWithUVsAndColors(name, positions, uvs, colors, indices, material);
        }
        else if (hasUVs && uvs != null)
        {
            return BuildMeshWithUVs(name, positions, uvs, indices, material);
        }
        else if (hasColors && colors != null)
        {
            return BuildMeshWithColors(name, positions, colors, indices, material);
        }

        return BuildMeshPositionsOnly(name, positions, indices, material);
    }

    private IMeshBuilder<MaterialBuilder> BuildMeshWithNormalsUVsAndColors(
        string name,
        Vector3f[] positions,
        Vector3f[] normals,
        Vector2f[] uvs,
        ColorRGBA[] colors,
        int[] indices,
        MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i0]), ToVector3(normals[i0])),
                new VertexColor1Texture1(ToVector4(colors[i0]), ToVector2(uvs[i0])));
            var v1 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i1]), ToVector3(normals[i1])),
                new VertexColor1Texture1(ToVector4(colors[i1]), ToVector2(uvs[i1])));
            var v2 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i2]), ToVector3(normals[i2])),
                new VertexColor1Texture1(ToVector4(colors[i2]), ToVector2(uvs[i2])));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    private IMeshBuilder<MaterialBuilder> BuildMeshWithNormalsAndColors(
        string name,
        Vector3f[] positions,
        Vector3f[] normals,
        ColorRGBA[] colors,
        int[] indices,
        MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i0]), ToVector3(normals[i0])),
                new VertexColor1(ToVector4(colors[i0])));
            var v1 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i1]), ToVector3(normals[i1])),
                new VertexColor1(ToVector4(colors[i1])));
            var v2 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexEmpty>(
                new VertexPositionNormal(ToVector3(positions[i2]), ToVector3(normals[i2])),
                new VertexColor1(ToVector4(colors[i2])));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    private IMeshBuilder<MaterialBuilder> BuildMeshWithUVsAndColors(
        string name,
        Vector3f[] positions,
        Vector2f[] uvs,
        ColorRGBA[] colors,
        int[] indices,
        MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPosition, VertexColor1Texture1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexBuilder<VertexPosition, VertexColor1Texture1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i0])),
                new VertexColor1Texture1(ToVector4(colors[i0]), ToVector2(uvs[i0])));
            var v1 = new VertexBuilder<VertexPosition, VertexColor1Texture1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i1])),
                new VertexColor1Texture1(ToVector4(colors[i1]), ToVector2(uvs[i1])));
            var v2 = new VertexBuilder<VertexPosition, VertexColor1Texture1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i2])),
                new VertexColor1Texture1(ToVector4(colors[i2]), ToVector2(uvs[i2])));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    private IMeshBuilder<MaterialBuilder> BuildMeshWithColors(
        string name,
        Vector3f[] positions,
        ColorRGBA[] colors,
        int[] indices,
        MaterialBuilder material)
    {
        var mesh = new MeshBuilder<VertexPosition, VertexColor1, VertexEmpty>(name);
        var prim = mesh.UsePrimitive(material);

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var v0 = new VertexBuilder<VertexPosition, VertexColor1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i0])),
                new VertexColor1(ToVector4(colors[i0])));
            var v1 = new VertexBuilder<VertexPosition, VertexColor1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i1])),
                new VertexColor1(ToVector4(colors[i1])));
            var v2 = new VertexBuilder<VertexPosition, VertexColor1, VertexEmpty>(
                new VertexPosition(ToVector3(positions[i2])),
                new VertexColor1(ToVector4(colors[i2])));

            prim.AddTriangle(v0, v1, v2);
        }

        return mesh;
    }

    private static Vector3 ToVector3(Vector3f v) => new(v.X, v.Y, v.Z);
    private static Vector2 ToVector2(Vector2f v) => new(v.U, v.V);
    private static Vector4 ToVector4(ColorRGBA c) => new(c.R, c.G, c.B, c.A);
}
