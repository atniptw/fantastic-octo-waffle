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

    [Fact(Skip = "Fixture bundle data corrupted - bundle name not properly null-terminated")]
    public void ExtractMeshes_CigarNeck_ExtractsMesh()
    {
            // Arrange
            string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
            var bundleData = File.ReadAllBytes(filePath);
            var service = new MeshExtractionService();

            // Act
            var meshes = service.ExtractMeshes(bundleData);

            // Assert - should extract at least one mesh
            Assert.NotNull(meshes);
            Assert.NotEmpty(meshes); // Now we expect mesh extraction to work
        }

        [Fact(Skip = "Fixture bundle data corrupted - bundle name not properly null-terminated")]
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

    [Fact(Skip = "Fixture bundle data corrupted - bundle name not properly null-terminated")]
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

    [Fact(Skip = "Fixture bundle data corrupted - bundle name not properly null-terminated")]
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
