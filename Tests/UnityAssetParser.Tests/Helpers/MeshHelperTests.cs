using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Helpers;

/// <summary>
/// Unit tests for MeshHelper.
/// Tests mesh geometry extraction including positions, normals, UVs, and indices.
/// Based on UnityPy reference implementation.
/// </summary>
public class MeshHelperTests
{
    /// <summary>
    /// Test that MeshHelper constructor validates input.
    /// </summary>
    [Fact]
    public void Constructor_NullMesh_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MeshHelper(null!, (2020, 1, 0, 0)));
    }

    /// <summary>
    /// Test that Process throws when VertexData is missing.
    /// </summary>
    [Fact]
    public void Process_MissingVertexData_ThrowsInvalidOperationException()
    {
        // Arrange
        var mesh = new Mesh
        {
            VertexData = null
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => helper.Process());
        Assert.Contains("no VertexData", ex.Message);
    }

    /// <summary>
    /// Test UnpackIndexBuffer with 16-bit indices.
    /// </summary>
    [Fact]
    public void UnpackIndexBuffer_16Bit_ReturnsCorrectIndices()
    {
        // Arrange: Create index buffer with UInt16 values
        var indices16 = new ushort[] { 0, 1, 2, 3, 4, 5 };
        var buffer = new byte[indices16.Length * 2];
        Buffer.BlockCopy(indices16, 0, buffer, 0, buffer.Length);

        var mesh = new Mesh
        {
            IndexBuffer = buffer,
            Use16BitIndices = true,
            VertexData = new VertexData
            {
                VertexCount = 6,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));

        // Act
        helper.Process();

        // Assert
        Assert.NotNull(helper.Indices);
        Assert.Equal(6, helper.Indices.Length);
        Assert.Equal(0u, helper.Indices[0]);
        Assert.Equal(1u, helper.Indices[1]);
        Assert.Equal(2u, helper.Indices[2]);
        Assert.Equal(3u, helper.Indices[3]);
        Assert.Equal(4u, helper.Indices[4]);
        Assert.Equal(5u, helper.Indices[5]);
        Assert.True(helper.Use16BitIndices);
    }

    /// <summary>
    /// Test UnpackIndexBuffer with 32-bit indices.
    /// </summary>
    [Fact]
    public void UnpackIndexBuffer_32Bit_ReturnsCorrectIndices()
    {
        // Arrange: Create index buffer with UInt32 values
        var indices32 = new uint[] { 100000, 200000, 300000 };
        var buffer = new byte[indices32.Length * 4];
        Buffer.BlockCopy(indices32, 0, buffer, 0, buffer.Length);

        var mesh = new Mesh
        {
            IndexBuffer = buffer,
            Use16BitIndices = false,
            VertexData = new VertexData
            {
                VertexCount = 3,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));

        // Act
        helper.Process();

        // Assert
        Assert.NotNull(helper.Indices);
        Assert.Equal(3, helper.Indices.Length);
        Assert.Equal(100000u, helper.Indices[0]);
        Assert.Equal(200000u, helper.Indices[1]);
        Assert.Equal(300000u, helper.Indices[2]);
        Assert.False(helper.Use16BitIndices);
    }

    /// <summary>
    /// Test that IndexFormat field is used in Unity 2017.4+.
    /// </summary>
    [Fact]
    public void Process_Unity2017_4_UsesIndexFormat()
    {
        // Arrange
        var mesh = new Mesh
        {
            IndexFormat = 0,  // 0 = UInt16
            IndexBuffer = new byte[6],
            VertexData = new VertexData
            {
                VertexCount = 3,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            }
        };
        var helper = new MeshHelper(mesh, (2017, 4, 0, 0));

        // Act
        helper.Process();

        // Assert
        Assert.True(helper.Use16BitIndices);
    }

    /// <summary>
    /// Test GetStreams for Unity 5+ (compute streams from channels).
    /// </summary>
    [Fact]
    public void Process_Unity5Plus_ComputesStreamsFromChannels()
    {
        // Arrange: Create a simple mesh with position and normal channels
        var channels = new[]
        {
            new ChannelInfo { Stream = 0, Offset = 0, Format = 0, Dimension = 3 },  // Positions (float3)
            new ChannelInfo { Stream = 0, Offset = 12, Format = 0, Dimension = 3 }  // Normals (float3)
        };

        var mesh = new Mesh
        {
            VertexData = new VertexData
            {
                VertexCount = 2,
                Channels = channels,
                DataSize = new byte[48]  // 2 vertices * 24 bytes (3+3 floats)
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));

        // Act
        helper.Process();

        // Assert - should compute streams and process without error
        // VertexCount should be 2 as specified in VertexData
        Assert.Equal(2, helper.VertexCount);
    }

    /// <summary>
    /// Test GetChannels for Unity &lt; 4 (compute channels from streams).
    /// </summary>
    [Fact]
    public void Process_Unity3_ComputesChannelsFromStreams()
    {
        // Arrange: Unity < 4 uses separate stream properties
        var stream = new StreamInfo
        {
            ChannelMask = 0b000011,  // Bits 0 and 1 set (vertex + normal)
            Offset = 0,
            Stride = 24,  // 6 floats per vertex
            DividerOp = 0,
            Frequency = 0
        };

        var mesh = new Mesh
        {
            VertexData = new VertexData
            {
                VertexCount = 1,
                Streams0 = stream,
                Streams1 = new StreamInfo(),
                Streams2 = new StreamInfo(),
                Streams3 = new StreamInfo(),
                DataSize = new byte[24]
            }
        };
        var helper = new MeshHelper(mesh, (3, 5, 0, 0));

        // Act
        helper.Process();

        // Assert - should compute channels and process without error
        // VertexCount should be 1 as specified in VertexData
        Assert.Equal(1, helper.VertexCount);
    }

    /// <summary>
    /// Test DecompressCompressedMesh with positions.
    /// Note: Skipped because proper bit-packed test data generation is complex.
    /// This would require real Unity bundle fixtures for accurate testing.
    /// </summary>
    [Fact(Skip = "Requires real Unity fixtures with proper bit-packed data")]
    public void Process_CompressedMesh_ExtractsPositions()
    {
        // This test is skipped because generating proper bit-packed test data
        // requires matching UnityPy's exact bit packing format.
        // Full testing should use real Unity bundle fixtures.
    }

    /// <summary>
    /// Test GetTriangles with standard triangle topology.
    /// </summary>
    [Fact]
    public void GetTriangles_TriangleTopology_ReturnsCorrectTriangles()
    {
        // Arrange
        var indices = new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        var buffer = new byte[indices.Length * 2];
        for (int i = 0; i < indices.Length; i++)
        {
            BitConverter.GetBytes((ushort)indices[i]).CopyTo(buffer, i * 2);
        }

        var mesh = new Mesh
        {
            IndexBuffer = buffer,
            Use16BitIndices = true,
            VertexData = new VertexData
            {
                VertexCount = 9,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            },
            SubMeshes = new[]
            {
                new SubMesh
                {
                    FirstByte = 0,
                    IndexCount = 9,
                    Topology = MeshTopology.Triangles
                }
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));
        helper.Process();

        // Act
        var triangles = helper.GetTriangles();

        // Assert
        Assert.Single(triangles);  // One submesh
        Assert.Equal(3, triangles[0].Count);  // 3 triangles
        Assert.Equal((0u, 1u, 2u), triangles[0][0]);
        Assert.Equal((3u, 4u, 5u), triangles[0][1]);
        Assert.Equal((6u, 7u, 8u), triangles[0][2]);
    }

    /// <summary>
    /// Test GetTriangles with triangle strip topology.
    /// </summary>
    [Fact]
    public void GetTriangles_TriangleStripTopology_HandlesWindingOrder()
    {
        // Arrange: Triangle strip with 5 indices creates 3 triangles
        var indices = new uint[] { 0, 1, 2, 3, 4 };
        var buffer = new byte[indices.Length * 2];
        for (int i = 0; i < indices.Length; i++)
        {
            BitConverter.GetBytes((ushort)indices[i]).CopyTo(buffer, i * 2);
        }

        var mesh = new Mesh
        {
            IndexBuffer = buffer,
            Use16BitIndices = true,
            VertexData = new VertexData
            {
                VertexCount = 5,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            },
            SubMeshes = new[]
            {
                new SubMesh
                {
                    FirstByte = 0,
                    IndexCount = 5,
                    Topology = MeshTopology.TriangleStrip
                }
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));
        helper.Process();

        // Act
        var triangles = helper.GetTriangles();

        // Assert
        Assert.Single(triangles);
        Assert.Equal(3, triangles[0].Count);  // 3 triangles from 5 indices
        // First triangle: normal order
        Assert.Equal((0u, 1u, 2u), triangles[0][0]);
        // Second triangle: flipped order
        Assert.Equal((2u, 1u, 3u), triangles[0][1]);
        // Third triangle: normal order
        Assert.Equal((2u, 3u, 4u), triangles[0][2]);
    }

    /// <summary>
    /// Test GetTriangles with quad topology.
    /// </summary>
    [Fact]
    public void GetTriangles_QuadTopology_ConvertsTwoTriangles()
    {
        // Arrange: One quad (4 indices) becomes 2 triangles
        var indices = new uint[] { 0, 1, 2, 3 };
        var buffer = new byte[indices.Length * 2];
        for (int i = 0; i < indices.Length; i++)
        {
            BitConverter.GetBytes((ushort)indices[i]).CopyTo(buffer, i * 2);
        }

        var mesh = new Mesh
        {
            IndexBuffer = buffer,
            Use16BitIndices = true,
            VertexData = new VertexData
            {
                VertexCount = 4,
                Channels = new[] { new ChannelInfo { Dimension = 0 } },  // Dummy channel
                DataSize = Array.Empty<byte>()
            },
            SubMeshes = new[]
            {
                new SubMesh
                {
                    FirstByte = 0,
                    IndexCount = 4,
                    Topology = MeshTopology.Quads
                }
            }
        };
        var helper = new MeshHelper(mesh, (2020, 1, 0, 0));
        helper.Process();

        // Act
        var triangles = helper.GetTriangles();

        // Assert
        Assert.Single(triangles);
        Assert.Equal(2, triangles[0].Count);  // 2 triangles from 1 quad
        Assert.Equal((0u, 1u, 2u), triangles[0][0]);
        Assert.Equal((0u, 2u, 3u), triangles[0][1]);
    }

    /// <summary>
    /// Helper to create PackedBitVector for testing.
    /// </summary>
    private static PackedBitVector CreatePackedBitVector(
        uint numItems,
        float range,
        float start,
        uint bitSize,
        byte[] data)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        writer.Write(numItems);
        writer.Write(range);
        writer.Write(start);
        writer.Write(bitSize);
        writer.Write(data.Length);
        writer.Write(data);

        // Add padding for alignment
        var padding = (4 - (data.Length % 4)) % 4;
        for (var i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }

        stream.Position = 0;
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        return new PackedBitVector(reader);
    }
}
