using UnityAssetParser.Services;

namespace UnityAssetParser.Tests.Services;

/// <summary>
/// Unit tests for MeshGeometryDto.
/// </summary>
public class MeshGeometryDtoTests
{
    [Fact]
    public void Constructor_CreatesValidDto()
    {
        // Act
        var dto = new MeshGeometryDto();

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Empty(dto.Positions);
        Assert.Empty(dto.Indices);
        Assert.Null(dto.Normals);
        Assert.Null(dto.UVs);
        Assert.Empty(dto.Groups);
        Assert.Equal(0, dto.VertexCount);
        Assert.Equal(0, dto.TriangleCount);
        Assert.False(dto.Use16BitIndices);
    }

    [Fact]
    public void Positions_StoresFloat32Array()
    {
        // Arrange
        var dto = new MeshGeometryDto();
        var positions = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };

        // Act
        dto.Positions = positions;

        // Assert
        Assert.Equal(6, dto.Positions.Length);
        Assert.Equal(1.0f, dto.Positions[0]);
        Assert.Equal(2.0f, dto.Positions[1]);
        Assert.Equal(3.0f, dto.Positions[2]);
    }

    [Fact]
    public void Indices_StoresUInt32Array()
    {
        // Arrange
        var dto = new MeshGeometryDto();
        var indices = new uint[] { 0, 1, 2, 3, 4, 5 };

        // Act
        dto.Indices = indices;

        // Assert
        Assert.Equal(6, dto.Indices.Length);
        Assert.Equal(0u, dto.Indices[0]);
        Assert.Equal(1u, dto.Indices[1]);
        Assert.Equal(2u, dto.Indices[2]);
    }

    [Fact]
    public void Normals_CanBeNull()
    {
        // Arrange
        var dto = new MeshGeometryDto
        {
            Normals = null
        };

        // Assert
        Assert.Null(dto.Normals);
    }

    [Fact]
    public void UVs_CanBeNull()
    {
        // Arrange
        var dto = new MeshGeometryDto
        {
            UVs = null
        };

        // Assert
        Assert.Null(dto.UVs);
    }

    [Fact]
    public void SubMeshGroup_CreatesValidGroup()
    {
        // Act
        var group = new MeshGeometryDto.SubMeshGroup
        {
            Start = 0,
            Count = 12,
            MaterialIndex = 0
        };

        // Assert
        Assert.Equal(0, group.Start);
        Assert.Equal(12, group.Count);
        Assert.Equal(0, group.MaterialIndex);
    }

    [Fact]
    public void Groups_SupportsMultipleSubMeshes()
    {
        // Arrange
        var dto = new MeshGeometryDto();

        // Act
        dto.Groups.Add(new MeshGeometryDto.SubMeshGroup
        {
            Start = 0,
            Count = 12,
            MaterialIndex = 0
        });
        dto.Groups.Add(new MeshGeometryDto.SubMeshGroup
        {
            Start = 12,
            Count = 6,
            MaterialIndex = 1
        });

        // Assert
        Assert.Equal(2, dto.Groups.Count);
        Assert.Equal(0, dto.Groups[0].Start);
        Assert.Equal(12, dto.Groups[0].Count);
        Assert.Equal(12, dto.Groups[1].Start);
        Assert.Equal(6, dto.Groups[1].Count);
    }

    [Fact]
    public void Use16BitIndices_IndicatesIndexFormat()
    {
        // Arrange
        var dto = new MeshGeometryDto
        {
            Use16BitIndices = true
        };

        // Assert
        Assert.True(dto.Use16BitIndices);
    }

    [Fact]
    public void VertexCount_TracksVertexCount()
    {
        // Arrange
        var dto = new MeshGeometryDto
        {
            VertexCount = 100
        };

        // Assert
        Assert.Equal(100, dto.VertexCount);
    }

    [Fact]
    public void TriangleCount_TracksTriangleCount()
    {
        // Arrange
        var dto = new MeshGeometryDto
        {
            TriangleCount = 50
        };

        // Assert
        Assert.Equal(50, dto.TriangleCount);
    }
}
