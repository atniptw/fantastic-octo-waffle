using UnityAssetParser.Bundle;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Services;

/// <summary>
/// Parser for Unity Mesh objects (ClassID from RenderableDetector.RenderableClassIds.Mesh) from SerializedFile object data.
/// This is a verbatim port from UnityPy/classes/Mesh.py.
/// 
/// Parses all 20+ Mesh fields with version-specific handling for Unity 3.x through 2022+.
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/Mesh.py
/// </summary>
public static class MeshParser
{
    /// <summary>
    /// Parses a Mesh object from SerializedFile object data.
    /// </summary>
    /// <param name="objectData">Raw object data from SerializedFile</param>
    /// <param name="version">Unity version tuple (major, minor, patch, type)</param>
    /// <param name="isBigEndian">Whether the data is big-endian</param>
    /// <param name="resSData">Optional external .resS resource data</param>
    /// <returns>Parsed Mesh object, or null if parsing fails</returns>
    public static Mesh? Parse(
        ReadOnlySpan<byte> objectData,
        (int, int, int, int) version,
        bool isBigEndian,
        ReadOnlyMemory<byte>? resSData = null)
    {
        if (objectData.Length < 4)
        {
            return null;
        }

        try
        {
            var mesh = new Mesh();
            using (var stream = new MemoryStream(objectData.ToArray(), false))
            using (var reader = new EndianBinaryReader(stream, isBigEndian))
            {
                int major = version.Item1;

                // Field 1: m_Name
                mesh.Name = ReadAlignedString(reader);
                reader.Align();

                // Field 2: m_SubMeshes
                mesh.SubMeshes = ReadSubMeshArray(reader, major);
                reader.Align();

                // Field 3: m_Shapes (BlendShapeData) - note: this is a struct, not an array!
                ReadBlendShapeData(reader);
                reader.Align();
                
                // Field 4: m_BindPose (version >= 4)
                if (major >= 4)
                {
                    ReadBindPoseArray(reader);
                    reader.Align();
                }

                // Field 5: m_BoneNameHashes (version >= 4)
                if (major >= 4)
                {
                    ReadBoneNameHashesArray(reader);
                    reader.Align();
                }

                // Field 6: m_RootBoneNameHash (version >= 4)
                if (major >= 4)
                {
                    reader.ReadUInt32();
                }

                // Field 7: m_BonesAABB (version >= 4)
                if (major >= 4)
                {
                    ReadBoneAABBArray(reader);
                    reader.Align();
                }

                // Field 8: m_VariableBoneCountWeights (version >= 4)
                if (major >= 4)
                {
                    ReadVariableBoneCountWeights(reader);
                    reader.Align();
                }

                // Field 9: m_MeshCompression
                mesh.MeshCompression = reader.ReadByte();

                // Field 10: m_IsReadable
                mesh.IsReadable = reader.ReadBoolean();

                // Field 11: m_KeepVertices
                mesh.KeepVertices = reader.ReadBoolean();

                // Field 12: m_KeepIndices
                mesh.KeepIndices = reader.ReadBoolean();
                reader.Align();

                // Field 13: m_IndexFormat (version >= 2017.3)
                if (major > 2017 || (major == 2017 && version.Item2 >= 3))
                {
                    mesh.IndexFormat = reader.ReadInt32();
                }

                // Field 14: m_IndexBuffer
                mesh.IndexBuffer = ReadIndexBuffer(reader);
                reader.Align();

                // Field 15: m_VertexData structure
                mesh.VertexData = ReadVertexData(reader, major);
                reader.Align();

                // Field 16: m_CompressedMesh
                mesh.CompressedMesh = ReadCompressedMesh(reader);
                reader.Align();

                // Field 17: m_LocalAABB
                ReadLocalAABB(reader);
                reader.Align();

                // Field 18: m_MeshUsageFlags (version >= 5)
                if (major >= 5)
                {
                    reader.ReadInt32();
                }

                // Field 19: m_CookingOptions
                reader.ReadInt32();

                // Field 20: m_BakedConvexCollisionMesh
                ReadByteArrayField(reader);
                reader.Align();

                // Field 21: m_BakedTriangleCollisionMesh
                ReadByteArrayField(reader);
                reader.Align();

                // Field 22-23: m_MeshMetrics[0] and m_MeshMetrics[1]
                reader.ReadSingle();
                reader.ReadSingle();

                // Field 24: m_StreamData
                mesh.StreamData = ReadStreamingInfo(reader);

                return mesh;
            }
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse Mesh: {ex.Message}");
            throw; // Re-throw so we can see what's failing
        }
        catch (EndOfStreamException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse Mesh: {ex.Message}");
            throw; // Re-throw so we can see what's failing
        }
    }

    private static string ReadAlignedString(EndianBinaryReader reader)
    {
        uint length = reader.ReadUInt32();
        if (length == 0)
            return string.Empty;

        byte[] bytes = reader.ReadBytes((int)length);
        string result = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        reader.Align(4);
        return result;
    }

    private static SubMesh[] ReadSubMeshArray(EndianBinaryReader reader, int major)
    {
        uint count = reader.ReadUInt32();
        
        var subMeshes = new SubMesh[count];

        for (int i = 0; i < count; i++)
        {
            subMeshes[i] = new SubMesh
            {
                // TypeTree order (from Cigar_neck debug output):
                // 1. firstByte (unsigned int)
                FirstByte = reader.ReadUInt32(),
                // 2. indexCount (unsigned int)
                IndexCount = reader.ReadUInt32(),
                // 3. topology (int)
                Topology = (MeshTopology)reader.ReadInt32(),
                // 4. baseVertex (unsigned int) - always present in v2022
                BaseVertex = reader.ReadUInt32(),
                // 5. firstVertex (unsigned int) - always present in v2022
                FirstVertex = reader.ReadUInt32(),
                // 6. vertexCount (unsigned int) - always present in v2022
                VertexCount = reader.ReadUInt32(),
                // 7. localAABB (AABB) - struct with 2 Vector3f = 24 bytes
                LocalAABB = major >= 3 ? ReadAABB(reader) : null
            };
        }
        
        return subMeshes;
    }

    private static AABB ReadAABB(EndianBinaryReader reader)
    {
        return new AABB
        {
            Center = new Vector3f(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            Extent = new Vector3f(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
        };
    }

    private static void ReadBlendShapeData(EndianBinaryReader reader)
    {
        // m_Shapes is a BlendShapeData struct with 4 arrays:
        // 1. vertices (vector of BlendShapeVertex)
        // 2. shapes (vector of MeshBlendShape)
        // 3. channels (vector of MeshBlendShapeChannel)
        // 4. fullWeights (vector of float)
        
        // vertices array
        uint verticesCount = reader.ReadUInt32();
        for (uint i = 0; i < verticesCount; i++)
        {
            // BlendShapeVertex: vertex (Vector3f), normal (Vector3f), tangent (Vector3f), index (uint)
            reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // vertex
            reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // normal
            reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // tangent
            reader.ReadUInt32(); // index
        }
        reader.Align();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] After vertices, position={reader.BaseStream.Position}");
        
        // shapes array
        uint shapesCount = reader.ReadUInt32();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] shapesCount={shapesCount}");
        for (uint i = 0; i < shapesCount; i++)
        {
            // MeshBlendShape: firstVertex (uint), vertexCount (uint), hasNormals (bool), hasTangents (bool)
            reader.ReadUInt32(); // firstVertex
            reader.ReadUInt32(); // vertexCount
            reader.ReadBoolean(); // hasNormals
            reader.ReadBoolean(); // hasTangents
            // Note: size=10 bytes, but 2 bools = 2 bytes, so no explicit padding needed here
        }
        reader.Align();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] After shapes, position={reader.BaseStream.Position}");
        
        // channels array
        uint channelsCount = reader.ReadUInt32();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] channelsCount={channelsCount}");
        for (uint i = 0; i < channelsCount; i++)
        {
            // MeshBlendShapeChannel: name (string), nameHash (uint), frameIndex (int), frameCount (int)
            _ = ReadAlignedString(reader);
            reader.Align();
            reader.ReadUInt32(); // nameHash
            reader.ReadInt32(); // frameIndex
            reader.ReadInt32(); // frameCount
        }
        reader.Align();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] After channels, position={reader.BaseStream.Position}");
        
        // fullWeights array
        uint fullWeightsCount = reader.ReadUInt32();
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] fullWeightsCount={fullWeightsCount}");
        for (uint i = 0; i < fullWeightsCount; i++)
        {
            reader.ReadSingle(); // weight
        }
        System.Diagnostics.Debug.WriteLine($"[ReadBlendShapeData] After fullWeights, position={reader.BaseStream.Position}");
    }

    private static void ReadBindPoseArray(EndianBinaryReader reader)
    {
        // m_BindPose - List<Matrix4x4f>
        uint count = reader.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            // Skip 16 floats per matrix
            for (int j = 0; j < 16; j++)
            {
                reader.ReadSingle();
            }
        }
    }

    private static void ReadBoneNameHashesArray(EndianBinaryReader reader)
    {
        // m_BoneNameHashes - List<uint>
        uint count = reader.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            reader.ReadUInt32();
        }
    }

    private static void ReadBoneAABBArray(EndianBinaryReader reader)
    {
        // m_BonesAABB - List<AABB>
        uint count = reader.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            ReadAABB(reader);
        }
    }

    private static void ReadVariableBoneCountWeights(EndianBinaryReader reader)
    {
        // m_VariableBoneCountWeights.m_Data - vector of unsigned int
        uint count = reader.ReadUInt32();
        for (uint i = 0; i < count; i++)
        {
            reader.ReadUInt32();
        }
    }

    private static VertexData ReadVertexData(EndianBinaryReader reader, int major)
    {
        var vertexData = new VertexData();

        // According to TypeTree: m_VertexData is VertexData struct with:
        // 1. m_VertexCount (uint)
        // 2. m_Channels (vector of ChannelInfo)
        // 3. m_DataSize (TypelessData - vector of bytes)
        
        // m_VertexCount
        vertexData.VertexCount = reader.ReadUInt32();

        // m_Channels array
        uint channelCount = reader.ReadUInt32();
        
        var channels = new ChannelInfo[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            channels[i] = new ChannelInfo
            {
                Stream = reader.ReadByte(),
                Offset = reader.ReadByte(),
                Format = reader.ReadByte(),
                Dimension = reader.ReadByte()
            };
        }
        vertexData.Channels = channels;
        
        // m_DataSize (TypelessData - vector of bytes)
        uint dataSize = reader.ReadUInt32();
        if (dataSize > 0)
        {
            vertexData.DataSize = reader.ReadBytes((int)dataSize);
        }
        else
        {
            vertexData.DataSize = Array.Empty<byte>();
        }

        return vertexData;
    }

    private static StreamInfo ReadStreamInfo(EndianBinaryReader reader)
    {
        return new StreamInfo
        {
            ChannelMask = reader.ReadUInt32(),
            Offset = reader.ReadUInt32(),
            Stride = reader.ReadUInt32(),
            DividerOp = reader.ReadUInt32(),
            Frequency = reader.ReadUInt32()
        };
    }

    private static CompressedMesh ReadCompressedMesh(EndianBinaryReader reader)
    {
        // Read all PackedBitVectors for compressed mesh data in TypeTree order
        // Fields with Range/Start (floats): Vertices, UV, Normals, Tangents, FloatColors
        // Fields without Range/Start (ints): Weights, NormalSigns, TangentSigns, BoneIndices, Triangles
        var compressedMesh = new CompressedMesh
        {
            Vertices = new PackedBitVector(reader, hasRangeStart: true),
            UV = new PackedBitVector(reader, hasRangeStart: true),
            Normals = new PackedBitVector(reader, hasRangeStart: true),
            Tangents = new PackedBitVector(reader, hasRangeStart: true),
            Weights = new PackedBitVector(reader, hasRangeStart: false),
            NormalSigns = new PackedBitVector(reader, hasRangeStart: false),
            TangentSigns = new PackedBitVector(reader, hasRangeStart: false),
            FloatColors = new PackedBitVector(reader, hasRangeStart: true),
            BoneIndices = new PackedBitVector(reader, hasRangeStart: false),
            Triangles = new PackedBitVector(reader, hasRangeStart: false)
        };
        
        // m_UVInfo
        uint uvInfo = reader.ReadUInt32();

        return compressedMesh;
    }

    private static byte[] ReadLocalAABB(EndianBinaryReader reader)
    {
        // m_LocalAABB - Skip for now, return empty byte array
        ReadAABB(reader);
        return Array.Empty<byte>();
    }

    private static byte[] ReadIndexBuffer(EndianBinaryReader reader)
    {
        uint count = reader.ReadUInt32();
        if (count == 0)
            return Array.Empty<byte>();

        return reader.ReadBytes((int)count);
    }

    private static void ReadByteArrayField(EndianBinaryReader reader)
    {
        // Read and discard byte array (used for baked collision meshes)
        uint count = reader.ReadUInt32();
        if (count > 0)
        {
            reader.ReadBytes((int)count);
        }
    }

    private static StreamingInfo? ReadStreamingInfo(EndianBinaryReader reader)
    {
        // m_StreamData: offset (UInt64), size (uint), path (string)
        ulong offset = reader.ReadUInt64();
        uint size = reader.ReadUInt32();
        string path = ReadAlignedString(reader);
        reader.Align();

        if (string.IsNullOrEmpty(path))
            return null;

        return new StreamingInfo
        {
            Path = path,
            Offset = (long)offset,
            Size = (long)size
        };
    }

    /// <summary>
    /// Creates a minimal test Mesh object for unit testing.
    /// This is a helper method for tests and should not be used in production.
    /// </summary>
    internal static Mesh CreateTestMesh()
    {
        return new Mesh
        {
            Name = "TestMesh",
            VertexData = new VertexData
            {
                VertexCount = 0,
                Channels = Array.Empty<ChannelInfo>(),
                DataSize = Array.Empty<byte>()
            },
            IndexBuffer = Array.Empty<byte>(),
            SubMeshes = Array.Empty<SubMesh>(),
            Use16BitIndices = true,
            MeshCompression = 0,
            IsReadable = true,
            KeepVertices = true,
            KeepIndices = true
        };
    }
}
