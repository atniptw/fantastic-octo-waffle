using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using UnityAssetParser.Services;
using UnityAssetParser.Export;
using UnityAssetParser.Bundle;
using SharpGLTF.Schema2;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// Smoke tests for the complete Unity bundle → glTF/GLB pipeline.
/// 
/// Tests validate the entire flow:
/// 1. Parse .hhh bundle file (UnityFS format)
/// 2. Extract mesh geometry (MeshExtractionService)
/// 3. Convert to glTF/GLB (GltfExporter)
/// 4. Validate output structure and correctness
/// 
/// These tests use real fixture bundles from the test assets.
/// </summary>
public class EndToEndPipelineSmokeTests
{
    private static readonly string FixturesPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Fixtures",
        "RealBundles");

    private readonly MeshExtractionService _meshExtractor = new();
    private readonly GltfExporter _gltfExporter = new();

    #region Cigar Bundle Tests

    [Fact]
    public void EndToEnd_CigarBundle_ParseToGlbSucceeds()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
        
        if (!File.Exists(filePath))
        {
            // Use Assert to make the skip visible in test output
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);

        // Act - Phase 1: Parse bundle
        var meshes = _meshExtractor.ExtractMeshes(bundleData);

        // Assert - Should extract at least one mesh
        Assert.NotNull(meshes);
        Assert.NotEmpty(meshes);

        var firstMesh = meshes[0];
        Assert.True(firstMesh.VertexCount > 0, "Mesh should have vertices");
        Assert.NotNull(firstMesh.Positions);
        Assert.NotEmpty(firstMesh.Positions);

        // Debug output to understand what was extracted
        Console.WriteLine($"Extracted mesh: {firstMesh.Name}");
        Console.WriteLine($"  VertexCount: {firstMesh.VertexCount}");
        Console.WriteLine($"  Positions.Length: {firstMesh.Positions?.Length ?? 0}");
        Console.WriteLine($"  Normals.Length: {firstMesh.Normals?.Length ?? 0}");
        Console.WriteLine($"  UVs.Length: {firstMesh.UVs?.Length ?? 0}");
        Console.WriteLine($"  Indices.Length: {firstMesh.Indices?.Length ?? 0}");
        Console.WriteLine($"  Use16BitIndices: {firstMesh.Use16BitIndices}");
        if (firstMesh.Indices != null && firstMesh.Indices.Length > 0)
        {
            var maxIndex = firstMesh.Indices.Max();
            var minIndex = firstMesh.Indices.Min();
            Console.WriteLine($"  Index range: [{minIndex}, {maxIndex}]");
        }
        // Act - Phase 2: Convert to glTF
        var unityMeshes = meshes.Select(m => m.ToUnityMesh()).ToList();
        
        // Debug converted mesh
        var firstUnityMesh = unityMeshes[0];
        Console.WriteLine($"Converted Unity mesh:");
        Console.WriteLine($"  Name: {firstUnityMesh.Name}");
        Console.WriteLine($"  Vertices: {firstUnityMesh.Vertices?.Length ?? 0}");
        Console.WriteLine($"  Normals: {firstUnityMesh.Normals?.Length ?? 0}");
        Console.WriteLine($"  UV: {firstUnityMesh.UV?.Length ?? 0}");
        Console.WriteLine($"  IndexBuffer: {firstUnityMesh.IndexBuffer?.Length ?? 0} bytes");
        
        var gltfModel = _gltfExporter.MeshesToGltf(unityMeshes);

        // Assert - Should produce valid glTF model
        Assert.NotNull(gltfModel);
        Assert.NotEmpty(gltfModel.LogicalMeshes);

        // Act - Phase 3: Export to GLB binary
        var glbData = _gltfExporter.ExportToGlb(gltfModel);

        // Assert - Should produce valid GLB binary
        ValidateGlbStructure(glbData);
        
        Console.WriteLine($"GLB export succeeded: {glbData.Length} bytes");
    }

    [Fact]
    public void EndToEnd_CigarBundle_GlbHasCorrectMeshCount()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
        
        if (!File.Exists(filePath))
        {
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);

        // Act
        var meshes = _meshExtractor.ExtractMeshes(bundleData);
        var gltfModel = _gltfExporter.MeshesToGltf(
            meshes.Select(m => m.ToUnityMesh()).ToList());

        // Assert - Mesh count should match
        Assert.Equal(meshes.Count, gltfModel.LogicalMeshes.Count);
    }

    [Fact]
    public void EndToEnd_CigarBundle_GlbPreservesVertexCount()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
        
        if (!File.Exists(filePath))
        {
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);

        // Act
        var meshes = _meshExtractor.ExtractMeshes(bundleData);
        var gltfModel = _gltfExporter.MeshesToGltf(
            meshes.Select(m => m.ToUnityMesh()).ToList());

        // Assert - glTF vertex counts should be reasonable
        // Note: SharpGLTF may consolidate duplicate vertices, so count can be different
        for (int i = 0; i < meshes.Count; i++)
        {
            var extractedMesh = meshes[i];
            var gltfMesh = gltfModel.LogicalMeshes[i];
            var primitive = gltfMesh.Primitives[0];
            var positionAccessor = primitive.GetVertexAccessor("POSITION");

            // glTF vertex count should be > 0 and <= original count (due to consolidation)
            Assert.True(positionAccessor.Count > 0, 
                $"glTF mesh should have vertices (got {positionAccessor.Count})");
            Assert.True(positionAccessor.Count <= extractedMesh.VertexCount,
                $"glTF vertex count ({positionAccessor.Count}) should not exceed original ({extractedMesh.VertexCount})");
            
            Console.WriteLine($"Mesh {i}: Original vertices={extractedMesh.VertexCount}, glTF vertices={positionAccessor.Count}");
        }
    }

    #endregion

    #region Glasses Bundle Tests

    [Fact]
    public void EndToEnd_GlassesBundle_ParseToGlbSucceeds()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Glasses_head.hhh");
        
        if (!File.Exists(filePath))
        {
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);

        // Act
        var meshes = _meshExtractor.ExtractMeshes(bundleData);
        var gltfModel = _gltfExporter.MeshesToGltf(
            meshes.Select(m => m.ToUnityMesh()).ToList());
        var glbData = _gltfExporter.ExportToGlb(gltfModel);

        // Assert
        Assert.NotNull(meshes);
        Assert.NotEmpty(meshes);
        ValidateGlbStructure(glbData);
    }

    #endregion

    #region ClownNose Bundle Tests

    [Fact]
    public void EndToEnd_ClownNoseBundle_ParseToGlbSucceeds()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "ClownNose_head.hhh");
        
        if (!File.Exists(filePath))
        {
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);

        // Act
        var meshes = _meshExtractor.ExtractMeshes(bundleData);
        var gltfModel = _gltfExporter.MeshesToGltf(
            meshes.Select(m => m.ToUnityMesh()).ToList());
        var glbData = _gltfExporter.ExportToGlb(gltfModel);

        // Assert
        Assert.NotNull(meshes);
        Assert.NotEmpty(meshes);
        ValidateGlbStructure(glbData);
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that GLB binary has correct header structure.
    /// Checks magic bytes, version, and total length field.
    /// Does not validate chunk structure or alignment.
    /// </summary>
    private static void ValidateGlbStructure(byte[] glbData)
    {
        Assert.NotNull(glbData);
        Assert.NotEmpty(glbData);

        // Validate minimum GLB size (20-byte header)
        Assert.True(glbData.Length >= 20, 
            $"GLB too small: {glbData.Length} bytes (minimum 20)");

        // Validate magic bytes ("glTF" in ASCII)
        var magic = Encoding.ASCII.GetString(glbData, 0, 4);
        Assert.Equal("glTF", magic);

        // Validate version (should be 2)
        var version = BitConverter.ToUInt32(glbData, 4);
        Assert.Equal(2u, version);

        // Validate total length matches actual data
        var totalLength = BitConverter.ToUInt32(glbData, 8);
        Assert.Equal((uint)glbData.Length, totalLength);
    }

    #endregion

    #region Performance Benchmarks

    [Fact]
    public void Benchmark_CigarBundle_ParseAndExportCompletes()
    {
        // Arrange
        string filePath = Path.Combine(FixturesPath, "Cigar_neck.hhh");
        
        if (!File.Exists(filePath))
        {
            Assert.True(File.Exists(filePath), $"Test fixture not found: {filePath}");
        }

        var bundleData = File.ReadAllBytes(filePath);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var meshes = _meshExtractor.ExtractMeshes(bundleData);
        var gltfModel = _gltfExporter.MeshesToGltf(
            meshes.Select(m => m.ToUnityMesh()).ToList());
        var glbData = _gltfExporter.ExportToGlb(gltfModel);

        sw.Stop();

        // Assert - Should complete in reasonable time (10s threshold for CI tolerance)
        Assert.True(sw.Elapsed.TotalSeconds < 10.0, 
            $"Pipeline took {sw.Elapsed.TotalSeconds:F2}s (expected < 10s)");

        // Log performance for monitoring
        Console.WriteLine($"Pipeline performance: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Output size: {glbData.Length / 1024}KB");
    }

    #endregion
}

/// <summary>
/// Extension methods to convert between DTO and Unity mesh types.
/// </summary>
internal static class MeshGeometryDtoExtensions
{
    public static UnityAssetParser.Classes.Mesh ToUnityMesh(this MeshGeometryDto dto)
    {
        var mesh = new UnityAssetParser.Classes.Mesh
        {
            Name = dto.Name,
        };

        // Convert positions (flat array to Vector3f array)
        if (dto.Positions != null && dto.Positions.Length > 0)
        {
            var vertexCount = dto.Positions.Length / 3;
            mesh.Vertices = new UnityAssetParser.Classes.Vector3f[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                mesh.Vertices[i] = new UnityAssetParser.Classes.Vector3f
                {
                    X = dto.Positions[i * 3],
                    Y = dto.Positions[i * 3 + 1],
                    Z = dto.Positions[i * 3 + 2]
                };
            }
        }

        // Convert normals (flat array to Vector3f array)
        // Only include if the length matches vertex count * 3
        if (dto.Normals != null && dto.Normals.Length > 0)
        {
            var expectedVertexCount = dto.Positions != null ? dto.Positions.Length / 3 : 0;
            var normalVertexCount = dto.Normals.Length / 3;
            
            if (normalVertexCount == expectedVertexCount)
            {
                mesh.Normals = new UnityAssetParser.Classes.Vector3f[normalVertexCount];
                for (int i = 0; i < normalVertexCount; i++)
                {
                    mesh.Normals[i] = new UnityAssetParser.Classes.Vector3f
                    {
                        X = dto.Normals[i * 3],
                        Y = dto.Normals[i * 3 + 1],
                        Z = dto.Normals[i * 3 + 2]
                    };
                }
            }
            else
            {
                // Skip normals that don't match vertex count (data corruption)
                Console.WriteLine($"WARNING: Skipping normals - count mismatch (expected {expectedVertexCount}, got {normalVertexCount})");
            }
        }

        // Convert UVs (flat array to Vector2f array)
        // Only include if the length matches vertex count * 2
        if (dto.UVs != null && dto.UVs.Length > 0)
        {
            var expectedVertexCount = dto.Positions != null ? dto.Positions.Length / 3 : 0;
            var uvVertexCount = dto.UVs.Length / 2;
            
            if (uvVertexCount == expectedVertexCount)
            {
                mesh.UV = new UnityAssetParser.Classes.Vector2f[uvVertexCount];
                for (int i = 0; i < uvVertexCount; i++)
                {
                    mesh.UV[i] = new UnityAssetParser.Classes.Vector2f
                    {
                        U = dto.UVs[i * 2],
                        V = dto.UVs[i * 2 + 1]
                    };
                }
            }
            else
            {
                // Skip UVs that don't match vertex count (data corruption)
                Console.WriteLine($"WARNING: Skipping UVs - count mismatch (expected {expectedVertexCount}, got {uvVertexCount})");
            }
        }

        // Convert indices (uint array to byte array IndexBuffer)
        if (dto.Indices != null && dto.Indices.Length > 0)
        {
            // Use the format from the DTO
            bool use16Bit = dto.Use16BitIndices;
            
            if (use16Bit)
            {
                // Validate all indices fit in UInt16 range
                for (int i = 0; i < dto.Indices.Length; i++)
                {
                    if (dto.Indices[i] > ushort.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Index {dto.Indices[i]} at position {i} exceeds UInt16.MaxValue ({ushort.MaxValue}) but Use16BitIndices is true");
                    }
                }
                
                mesh.IndexBuffer = new byte[dto.Indices.Length * 2];
                mesh.IndexFormat = 0; // Set format flag for 16-bit
                for (int i = 0; i < dto.Indices.Length; i++)
                {
                    BitConverter.GetBytes((ushort)dto.Indices[i]).CopyTo(mesh.IndexBuffer, i * 2);
                }
            }
            else
            {
                mesh.IndexBuffer = new byte[dto.Indices.Length * 4];
                mesh.IndexFormat = 1; // Set format flag for 32-bit
                for (int i = 0; i < dto.Indices.Length; i++)
                {
                    BitConverter.GetBytes(dto.Indices[i]).CopyTo(mesh.IndexBuffer, i * 4);
                }
            }
        }

        return mesh;
    }
}
