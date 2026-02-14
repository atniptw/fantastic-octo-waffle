using Xunit;
using UnityAssetParser.Services;
using System.Collections.Generic;

namespace UnityAssetParser.Tests.Services;

public class MeshExportServiceTests
{
    [Fact]
    public void ExportToThreeJS_WithValidMesh_ReturnsGeometryDictionary()
    {
        // Arrange
        var mesh = new Classes.Mesh
        {
            Name = "TestMesh",
            VertexData = new Classes.VertexData
            {
                VertexCount = 220,
                Channels = new Classes.ChannelInfo[3]
                {
                    new() { Stream = 0, Offset = 0, Format = 0, Dimension = 3 },
                    new() { Stream = 0, Offset = 12, Format = 1, Dimension = 52 },
                    new() { Stream = 0, Offset = 20, Format = 1, Dimension = 2 }
                }
            },
            SubMeshes = new Classes.SubMesh[1]
            {
                new() 
                { 
                    FirstByte = 0, 
                    IndexCount = 100, 
                    Topology = 0,
                    FirstVertex = 0,
                    VertexCount = 220
                }
            },
            IndexBuffer = new byte[400] // 100 indices * 4 bytes
        };

        // Act
        var result = MeshExportService.ExportToThreeJS(mesh);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestMesh", result["name"]);
        Assert.Equal("BufferGeometry", result["type"]);
        Assert.Equal(220u, result["vertexCount"]);
        Assert.Equal(100, result["indexCount"]);
        Assert.NotNull(result["channels"]);
        Assert.NotNull(result["submeshes"]);
    }

    [Fact]
    public void ExportToJSON_WithValidMesh_ReturnsValidJSON()
    {
        // Arrange
        var mesh = new Classes.Mesh
        {
            Name = "TestMesh",
            VertexData = new Classes.VertexData
            {
                VertexCount = 100
            },
            IndexBuffer = new byte[100]
        };

        // Act
        var json = MeshExportService.ExportToJSON(mesh);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("TestMesh", json);
        Assert.Contains("BufferGeometry", json);
        Assert.Contains("100", json); // vertexCount
    }
}
