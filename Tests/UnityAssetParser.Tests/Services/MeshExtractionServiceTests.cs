using UnityAssetParser.Exceptions;
using UnityAssetParser.Services;

namespace UnityAssetParser.Tests.Services;

/// <summary>
/// Integration tests for MeshExtractionService.
/// Tests the full pipeline: Bundle → SerializedFile → Mesh → DTO.
/// </summary>
public class MeshExtractionServiceTests
{
    [Fact]
    public void ExtractMeshes_NullBundleData_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new MeshExtractionService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.ExtractMeshes(null!));
    }

    [Fact]
    public void ExtractMeshes_EmptyBundle_ThrowsHeaderParseException()
    {
        // Arrange
        var service = new MeshExtractionService();
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        // Empty bundle will fail during header parsing
        var ex = Assert.Throws<HeaderParseException>(() => service.ExtractMeshes(emptyData));
        Assert.Contains("Failed to parse UnityFS header", ex.Message);
    }

    [Fact]
    public void ExtractMeshes_InvalidBundleData_ThrowsInvalidBundleSignatureException()
    {
        // Arrange
        var service = new MeshExtractionService();
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act & Assert
        // Invalid data will fail during signature validation
        var ex = Assert.Throws<InvalidBundleSignatureException>(() => service.ExtractMeshes(invalidData));
        Assert.Contains("UnityFS", ex.Message);
    }

    // Note: Real integration tests with actual .hhh files would go here
    // These would test:
    // - Extracting meshes from valid bundles
    // - UInt16 vs UInt32 index formats
    // - Meshes with/without normals and UVs
    // - Multiple submeshes
    // - External .resS streaming data
    //
    // For now, these tests are placeholders since we need actual test fixtures.
    // The issue says "gracefully skip if fixture assets absent", so these tests
    // will be implemented once fixtures are available or can be generated.

    [Fact(Skip = "Requires test fixture with actual bundle data")]
    public void ExtractMeshes_ValidBundle_ReturnsMeshes()
    {
        // This test would load a real .hhh file and validate extraction
        // Example:
        // var bundleData = File.ReadAllBytes("fixtures/test_mesh.hhh");
        // var service = new MeshExtractionService();
        // var meshes = service.ExtractMeshes(bundleData);
        // Assert.NotEmpty(meshes);
        // Assert.True(meshes[0].VertexCount > 0);
    }

    [Fact(Skip = "Requires test fixture with UInt16 indices")]
    public void ExtractMeshes_SmallMesh_UsesUInt16Indices()
    {
        // Test that meshes with < 65536 vertices use UInt16 indices
    }

    [Fact(Skip = "Requires test fixture with UInt32 indices")]
    public void ExtractMeshes_LargeMesh_UsesUInt32Indices()
    {
        // Test that meshes with >= 65536 vertices use UInt32 indices
    }

    [Fact(Skip = "Requires test fixture with multi-material mesh")]
    public void ExtractMeshes_MultiMaterial_ExtractsGroups()
    {
        // Test that submeshes are correctly converted to groups
    }

    [Fact(Skip = "Requires test fixture with external streaming")]
    public void ExtractMeshes_ExternalStreaming_HandlesResS()
    {
        // Test that external .resS resource data is handled
    }
}
