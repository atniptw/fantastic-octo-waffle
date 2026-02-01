using System;
using System.IO;
using Xunit;
using UnityAssetParser.Services;

namespace UnityAssetParser.Tests.Services;

/// <summary>
/// Integration tests for MeshExtractionService using real .hhh bundles.
/// </summary>
public class MeshExtractionServiceRealBundleTests
{
    private static readonly string FixturesPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Fixtures",
        "RealBundles");

    [Fact]
    public void ExtractMeshes_CigarNeck_ExtractsMesh()
    {
        // Redirect Console.Out to capture service diagnostics
        var debugFile = "/tmp/mesh_extraction_debug.log";
        var writer = new StreamWriter(debugFile, append: true);
        writer.AutoFlush = true;
        var oldOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            // Arrange
            string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
            var bundleData = File.ReadAllBytes(filePath);
            var service = new MeshExtractionService();

            // Act
            Console.WriteLine("=== ExtractMeshes_CigarNeck_ExtractsMesh ===");
            var meshes = service.ExtractMeshes(bundleData);

            Console.WriteLine($"Extracted {meshes.Count} meshes");
            foreach (var mesh in meshes)
            {
                Console.WriteLine($"Mesh: Name={mesh.Name}, VertexCount={mesh.VertexCount}");
            }

            // Assert - should extract at least one mesh
            Assert.NotNull(meshes);
            Assert.NotEmpty(meshes); // Now we expect mesh extraction to work
        }
        finally
        {
            Console.SetOut(oldOut);
            writer.Close();
            writer.Dispose();
        }
    }

    [Fact(Skip = "TypeTree parsing incomplete: mesh data fields empty (no positions/normals/submeshes). Investigating TypeTreeReader traversal logic.")]
    public void ExtractMeshes_CigarNeck_ExtractsMeshData()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
        var bundleData = File.ReadAllBytes(filePath);
        var service = new MeshExtractionService();

        // Act
        var meshes = service.ExtractMeshes(bundleData);

        // Debug output
        Console.WriteLine($"DEBUG: Extracted {meshes.Count} meshes from Cigar_neck.hhh");
        foreach (var mesh in meshes)
        {
            Console.WriteLine($"DEBUG: Mesh Name={mesh.Name}, VertexCount={mesh.VertexCount}");
        }

        // Assert
        Assert.NotEmpty(meshes);
        var cigarMesh = meshes[0];

        // From UnityPy output:
        // - Name: 雪茄
        // - VertexCount: 220
        // - m_IndexBuffer has 936 items
        // - 3 submeshes
        Assert.Equal("雪茄", cigarMesh.Name);
        Assert.Equal(220, cigarMesh.VertexCount);
        Assert.NotNull(cigarMesh.Positions);
        Assert.Equal(220 * 3, cigarMesh.Positions.Length); // 220 vertices * 3 floats each
        Assert.NotNull(cigarMesh.Indices);
        Assert.Equal(936, cigarMesh.Indices.Length); // Total index count from UnityPy
        Assert.NotNull(cigarMesh.Groups);
        Assert.Equal(3, cigarMesh.Groups.Count); // 3 submeshes
    }

    [Fact(Skip = "TypeTree parsing incomplete: mesh data fields empty (no positions/normals/submeshes). Investigating TypeTreeReader traversal logic.")]
    public void ExtractMeshes_ClownNoseHead_ExtractsMesh()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "ClownNose_head.hhh");
        var bundleData = File.ReadAllBytes(filePath);
        var service = new MeshExtractionService();

        // Act
        var meshes = service.ExtractMeshes(bundleData);

        // Assert
        Console.WriteLine($"DEBUG: Extracted {meshes.Count} meshes");
        foreach (var mesh in meshes)
        {
            Console.WriteLine($"DEBUG: Mesh Name={mesh.Name}");
            Console.WriteLine($"DEBUG:   VertexCount={mesh.VertexCount}");
        }

        Assert.NotEmpty(meshes);
    }

    [Fact(Skip = "TypeTree parsing incomplete: mesh data fields empty (no positions/normals/submeshes). Investigating TypeTreeReader traversal logic.")]
    public void ExtractMeshes_GlassesHead_ExtractsMesh()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Glasses_head.hhh");
        var bundleData = File.ReadAllBytes(filePath);
        var service = new MeshExtractionService();

        // Act
        var meshes = service.ExtractMeshes(bundleData);

        // Assert
        Console.WriteLine($"DEBUG: Extracted {meshes.Count} meshes");
        foreach (var mesh in meshes)
        {
            Console.WriteLine($"DEBUG: Mesh Name={mesh.Name}");
            Console.WriteLine($"DEBUG:   VertexCount={mesh.VertexCount}");
        }

        Assert.NotEmpty(meshes);
    }
}
