using UnityAssetParser.Classes;
using UnityAssetParser.Export;
using SharpGLTF.Schema2;
using System.Text;

namespace UnityAssetParser.Tests.Export;

using UnityMesh = UnityAssetParser.Classes.Mesh;

/// <summary>
/// Unit tests for GltfExporter.
/// 
/// Tests validate:
/// - Mesh → glTF conversion correctness
/// - glTF binary format compliance (magic bytes, structure)
/// - Vertex/index/normal/uv accessor creation
/// - Error handling for malformed input
/// - Determinism (same input → same bytes)
/// </summary>
public class GltfExporterTests
{
    private readonly GltfExporter _exporter = new();

    #region Helper Methods

    /// <summary>
    /// Creates a simple test mesh with vertex data.
    /// </summary>
    private static UnityMesh CreateSimpleTriangleMesh()
    {
        var mesh = new Mesh
        {
            Name = "TestTriangle",
            Vertices = new[]
            {
                new Vector3f { X = 0, Y = 0, Z = 0 },
                new Vector3f { X = 1, Y = 0, Z = 0 },
                new Vector3f { X = 0, Y = 1, Z = 0 }
            },
            Normals = new[]
            {
                new Vector3f { X = 0, Y = 0, Z = 1 },
                new Vector3f { X = 0, Y = 0, Z = 1 },
                new Vector3f { X = 0, Y = 0, Z = 1 }
            },
            UV = new[]
            {
                new Vector2f { X = 0, Y = 0 },
                new Vector2f { X = 1, Y = 0 },
                new Vector2f { X = 0, Y = 1 }
            }
        };

        return mesh;
    }

    /// <summary>
    /// Creates a mesh without normals.
    /// </summary>
    private static UnityMesh CreateMeshWithoutNormals()
    {
        var mesh = new Mesh
        {
            Name = "NoNormals",
            Vertices = new[]
            {
                new Vector3f { X = 0, Y = 0, Z = 0 },
                new Vector3f { X = 1, Y = 0, Z = 0 },
                new Vector3f { X = 0, Y = 1, Z = 0 }
            },
            Normals = null
        };

        return mesh;
    }

    #endregion

    #region Basic Export Tests

    [Fact]
    public void MeshesToGltf_SingleSimpleMesh_CreatesValidModel()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<Mesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        Assert.NotNull(model);
        Assert.Single(model.LogicalMeshes);
        Assert.Equal("TestTriangle", model.LogicalMeshes[0].Name);
    }

    [Fact]
    public void MeshesToGltf_MultipleMeshes_AllIncluded()
    {
        // Arrange
        var mesh1 = CreateSimpleTriangleMesh();
        var mesh2 = CreateSimpleTriangleMesh();
        mesh2.Name = "SecondMesh";
        var meshes = new List<UnityMesh> { mesh1, mesh2 };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        Assert.NotNull(model);
        Assert.Equal(2, model.LogicalMeshes.Count);
        Assert.Contains(model.LogicalMeshes, m => m.Name == "TestTriangle");
        Assert.Contains(model.LogicalMeshes, m => m.Name == "SecondMesh");
    }

    [Fact]
    public void MeshesToGlb_ExportsValidBinary()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var glbData = _exporter.MeshesToGlb(meshes);

        // Assert
        Assert.NotNull(glbData);
        Assert.NotEmpty(glbData);

        // Validate glTF magic bytes (first 4 bytes: "glTF")
        var magic = Encoding.ASCII.GetString(glbData, 0, 4);
        Assert.Equal("glTF", magic);

        // Validate minimum GLB structure
        Assert.True(glbData.Length >= 20, "GLB must have at least 20-byte header");
    }

    #endregion

    #region Geometry Validation Tests

    [Fact]
    public void MeshesToGltf_PreservesVertexData()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        var gltfMesh = model.LogicalMeshes[0];
        var primitive = gltfMesh.Primitives[0];
        var positions = primitive.VertexAccessor("POSITION");

        Assert.NotNull(positions);
        Assert.Equal(3, positions.Count); // 3 vertices in triangle
    }

    [Fact]
    public void MeshesToGltf_PreservesNormalData()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        var gltfMesh = model.LogicalMeshes[0];
        var primitive = gltfMesh.Primitives[0];
        var normals = primitive.VertexAccessor("NORMAL");

        Assert.NotNull(normals);
        Assert.Equal(3, normals.Count); // Same as vertex count
    }

    [Fact]
    public void MeshesToGltf_PreservesUVData()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        var gltfMesh = model.LogicalMeshes[0];
        var primitive = gltfMesh.Primitives[0];
        var uvs = primitive.VertexAccessor("TEXCOORD_0");

        Assert.NotNull(uvs);
        Assert.Equal(3, uvs.Count); // Same as vertex count
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void MeshesToGltf_NullMeshes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _exporter.MeshesToGltf(null!));
    }

    [Fact]
    public void MeshesToGltf_EmptyMeshList_CreatesEmptyModel()
    {
        // Arrange
        var meshes = new List<UnityMesh>();

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        Assert.NotNull(model);
        Assert.Empty(model.LogicalMeshes);
    }

    [Fact]
    public void MeshesToGltf_MeshWithNoVertices_ThrowsInvalidOperationException()
    {
        // Arrange
        var mesh = new Mesh
        {
            Name = "EmptyMesh",
            Vertices = Array.Empty<Vector3f>() // No vertices
        };
        var meshes = new List<UnityMesh> { mesh };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _exporter.MeshesToGltf(meshes));
        Assert.Contains("no vertices", ex.Message);
    }

    [Fact]
    public void MeshesToGltf_NullMeshInList_IsSkipped()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
#pragma warning disable CS8625
        var meshes = new List<UnityMesh> { null, mesh, null };
#pragma warning restore CS8625

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        Assert.NotNull(model);
        Assert.Single(model.LogicalMeshes);
        Assert.Equal("TestTriangle", model.LogicalMeshes[0].Name);
    }

    [Fact]
    public void ExportToGlb_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _exporter.ExportToGlb(null!));
    }

    #endregion

    #region Determinism Tests

    [Fact]
    public void MeshesToGlb_IsDeterministic()
    {
        // Arrange
        var mesh = CreateSimpleTriangleMesh();
        var meshes = new List<Mesh> { mesh };

        // Act
        var glb1 = _exporter.MeshesToGlb(meshes);
        var glb2 = _exporter.MeshesToGlb(meshes);

        // Assert
        Assert.Equal(glb1, glb2);
        Assert.Equal(glb1.Length, glb2.Length);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MeshesToGltf_MeshWithoutNormals_SkipsNormalAccessor()
    {
        // Arrange
        var mesh = CreateMeshWithoutNormals();
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        var primitive = model.LogicalMeshes[0].Primitives[0];
        Assert.NotNull(primitive.VertexAccessor("POSITION"));
        Assert.Null(primitive.VertexAccessor("NORMAL")); // Should not have normals
    }

    [Fact]
    public void MeshesToGltf_MeshWithoutUVs_SkipsUVAccessor()
    {
        // Arrange
        var mesh = new Mesh
        {
            Name = "NoUVs",
            Vertices = new[]
            {
                new Vector3f { X = 0, Y = 0, Z = 0 },
                new Vector3f { X = 1, Y = 0, Z = 0 },
                new Vector3f { X = 0, Y = 1, Z = 0 }
            },
            UV = null // No UVs
        };
        var meshes = new List<UnityMesh> { mesh };

        // Act
        var model = _exporter.MeshesToGltf(meshes);

        // Assert
        var primitive = model.LogicalMeshes[0].Primitives[0];
        Assert.NotNull(primitive.VertexAccessor("POSITION"));
        Assert.Null(primitive.VertexAccessor("TEXCOORD_0")); // Should not have UVs
    }

    #endregion
}
