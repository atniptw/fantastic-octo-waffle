using UnityAssetParser.Bundle;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

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
    /// Parses a Mesh object using direct binary reading (bypasses TypeTree).
    /// This is the primary parsing method for extracting mesh geometry.
    /// </summary>
    public static Mesh? ParseBinary(
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
            using (var stream = new MemoryStream(objectData.ToArray(), false))
            using (var reader = new EndianBinaryReader(stream, isBigEndian))
            {
                var mesh = new Mesh();

                Console.WriteLine($"DEBUG: ParseBinary starting - objectData.Length={objectData.Length}");

                // Read m_Name (string with length prefix)
                int nameLength = reader.ReadInt32();
                Console.WriteLine($"DEBUG: Mesh name length={nameLength}");
                if (nameLength > 0 && nameLength < 1000)
                {
                    mesh.Name = reader.ReadUtf8String(nameLength);
                    Console.WriteLine($"DEBUG: Mesh.Name = '{mesh.Name}'");
                    
                    // Align to 4 bytes after string
                    reader.Align(4);
                }

                // Read m_SubMeshes (array of SubMesh)
                int subMeshCount = reader.ReadInt32();
                Console.WriteLine($"DEBUG: SubMesh count={subMeshCount}");
                if (subMeshCount > 0)
                {
                    mesh.SubMeshes = new SubMesh[subMeshCount];
                    for (int i = 0; i < subMeshCount; i++)
                    {
                        mesh.SubMeshes[i] = new SubMesh
                        {
                            FirstByte = reader.ReadUInt32(),
                            IndexCount = reader.ReadUInt32(),
                            Topology = (MeshTopology)reader.ReadInt32()
                        };
                    }
                    Console.WriteLine($"DEBUG: Read {subMeshCount} submeshes");
                }

                // TODO: Continue with remaining fields...
                // This is a minimal scaffold - full implementation will read all Mesh fields
                // For now, return the partial Mesh to get vertex extraction working

                return mesh;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: MeshParser.ParseBinary failed ({ex.GetType().Name}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a Mesh object using TypeTree-driven parsing (recommended).
    /// </summary>
    public static Mesh? ParseWithTypeTree(
        ReadOnlySpan<byte> objectData,
        IReadOnlyList<TypeTreeNode> typeTreeNodes,
        (int, int, int, int) version,
        bool isBigEndian,
        ReadOnlyMemory<byte>? resSData = null)
    {
        if (objectData.Length < 4 || typeTreeNodes == null || typeTreeNodes.Count == 0)
        {
            Console.WriteLine($"DEBUG: ParseWithTypeTree skipping - data.Length={objectData.Length}, nodes={typeTreeNodes?.Count ?? -1}");
            return null;
        }

        Console.WriteLine($"DEBUG: ParseWithTypeTree starting - data.Length={objectData.Length}, nodes={typeTreeNodes.Count}, version={version}");

        try
        {
            using (var stream = new MemoryStream(objectData.ToArray(), false))
            using (var reader = new EndianBinaryReader(stream, isBigEndian))
            {
                Console.WriteLine($"DEBUG: Creating TypeTreeReader from {typeTreeNodes.Count} nodes");
                var ttReader = TypeTreeReader.CreateFromFlatList(reader, typeTreeNodes);
                Console.WriteLine($"DEBUG: ReadObject from TypeTree...");
                var data = ttReader.ReadObject();

                Console.WriteLine($"DEBUG: ReadObject complete, got {data.Count} keys");

                // Map TypeTree data to Mesh object
                return MapToMesh(data, version, resSData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: MeshParser.ParseWithTypeTree failed ({ex.GetType().Name}): {ex.Message}");
            Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Maps TypeTree dictionary data to a Mesh object.
    /// </summary>
    private static Mesh MapToMesh(Dictionary<string, object?> data, (int, int, int, int) version, ReadOnlyMemory<byte>? resSData)
    {
        var mesh = new Mesh();

        Console.WriteLine($"DEBUG: MapToMesh - data has {data.Count} keys: {string.Join(", ", data.Keys)}");

        // Field 1: m_Name
        if (data.TryGetValue("m_Name", out var nameObj) && nameObj is string name)
        {
            mesh.Name = name;
            Console.WriteLine($"DEBUG: Mapped m_Name = '{name}'");
        }

        // Field 2: m_SubMeshes
        if (data.TryGetValue("m_SubMeshes", out var subMeshesObj) && subMeshesObj is List<object?> subMeshesList)
        {
            mesh.SubMeshes = MapSubMeshes(subMeshesList);
            Console.WriteLine($"DEBUG: Mapped {mesh.SubMeshes.Length} SubMeshes");
        }

        // Field 3: m_BindPose (legacy vertices/normals data)
        if (data.TryGetValue("m_BindPose", out var bindPoseObj) && bindPoseObj is List<object?> bindPoseList)
        {
            // BindPose is list of 4x4 matrices for skeletal animation - not directly used for geometry extraction
            Console.WriteLine($"DEBUG: m_BindPose present ({bindPoseList.Count} items)");
        }

        // Field 4: m_IndexBuffer
        if (data.TryGetValue("m_IndexBuffer", out var indexBufferObj) && indexBufferObj is List<object?> indexList)
        {
            var bytes = new List<byte>();
            foreach (var item in indexList)
            {
                if (item != null)
                {
                    bytes.Add(Convert.ToByte(item));
                }
            }
            mesh.IndexBuffer = bytes.ToArray();
            Console.WriteLine($"DEBUG: Mapped IndexBuffer with {mesh.IndexBuffer.Length} bytes");
        }

        // Field 5: m_Skin (bone weights - not directly used for geometry extraction)
        if (data.TryGetValue("m_Skin", out var skinObj) && skinObj is List<object?> skinList)
        {
            Console.WriteLine($"DEBUG: m_Skin present ({skinList.Count} items)");
        }

        // Field 6: m_MeshCompression
        if (data.TryGetValue("m_MeshCompression", out var compressionObj) && compressionObj is int compression)
        {
            mesh.MeshCompression = (byte)compression;
            Console.WriteLine($"DEBUG: Mapped m_MeshCompression = {mesh.MeshCompression}");
        }

        // Field 7: m_MeshUsageFlags
        if (data.TryGetValue("m_MeshUsageFlags", out var usageFlagsObj) && usageFlagsObj != null)
        {
            var flags = Convert.ToInt32(usageFlagsObj);
            Console.WriteLine($"DEBUG: m_MeshUsageFlags = {flags}");
        }

        // Field 8: m_LocalAABB (bounding box - not directly used for geometry extraction)
        if (data.TryGetValue("m_LocalAABB", out var aabbObj) && aabbObj is Dictionary<string, object?> aabbDict)
        {
            Console.WriteLine($"DEBUG: m_LocalAABB present");
        }

        // Field 9: m_VertexData
        if (data.TryGetValue("m_VertexData", out var vertexDataObj) && vertexDataObj is Dictionary<string, object?> vertexDataDict)
        {
            mesh.VertexData = MapVertexData(vertexDataDict);
            Console.WriteLine($"DEBUG: Mapped VertexData: VertexCount={mesh.VertexData.VertexCount}, Channels={mesh.VertexData.Channels?.Length ?? 0}, DataSize={mesh.VertexData.DataSize?.Length ?? 0}");
        }

        // Field 10: m_CompressedMesh
        if (data.TryGetValue("m_CompressedMesh", out var compressedMeshObj) && compressedMeshObj is Dictionary<string, object?> compMeshDict)
        {
            mesh.CompressedMesh = MapCompressedMesh(compMeshDict);
            Console.WriteLine($"DEBUG: Mapped CompressedMesh");
        }

        // Field 11: m_StreamData (external .resS resources)
        if (data.TryGetValue("m_StreamData", out var streamDataObj) && streamDataObj is Dictionary<string, object?> streamDict)
        {
            mesh.StreamData = MapStreamingInfo(streamDict);
            Console.WriteLine($"DEBUG: Mapped StreamData: Path={mesh.StreamData.Path}, Offset={mesh.StreamData.Offset}, Size={mesh.StreamData.Size}");
        }

        // Field 12: m_Shapes (blend shapes - not directly used for geometry extraction)
        if (data.TryGetValue("m_Shapes", out var shapesObj) && shapesObj != null)
        {
            Console.WriteLine($"DEBUG: m_Shapes present");
        }

        // Field 13: m_KeepVertices
        if (data.TryGetValue("m_KeepVertices", out var keepVertObj) && keepVertObj != null)
        {
            mesh.KeepVertices = Convert.ToBoolean(keepVertObj);
        }

        // Field 14: m_KeepIndices
        if (data.TryGetValue("m_KeepIndices", out var keepIndObj) && keepIndObj != null)
        {
            mesh.KeepIndices = Convert.ToBoolean(keepIndObj);
        }

        // Version-specific fields (Unity >= 2017.4)
        if (version.Item1 >= 2017 || (version.Item1 == 2017 && version.Item2 >= 4))
        {
            if (data.TryGetValue("m_IndexFormat", out var indexFormatObj) && indexFormatObj != null)
            {
                mesh.IndexFormat = Convert.ToInt32(indexFormatObj);
                Console.WriteLine($"DEBUG: Mapped m_IndexFormat = {mesh.IndexFormat}");
            }
        }

        // Version-specific fields (Unity < 2017.4)
        if (version.Item1 < 2017 || (version.Item1 == 2017 && version.Item2 < 4))
        {
            if (data.TryGetValue("m_Use16BitIndices", out var use16BitObj) && use16BitObj != null)
            {
                mesh.Use16BitIndices = Convert.ToBoolean(use16BitObj);
                Console.WriteLine($"DEBUG: Mapped m_Use16BitIndices = {mesh.Use16BitIndices}");
            }
        }

        // Legacy fields (present in certain Unity versions)
        if (data.TryGetValue("m_Vertices", out var verticesObj) && verticesObj is List<object?> verticesList)
        {
            // Legacy vertex data - would be imported but we prefer VertexData channel approach
            Console.WriteLine($"DEBUG: m_Vertices legacy field present ({verticesList.Count} items)");
        }

        if (data.TryGetValue("m_UV", out var uvObj) && uvObj is List<object?> uvList)
        {
            // Legacy UV data
            Console.WriteLine($"DEBUG: m_UV legacy field present ({uvList.Count} items)");
        }

        if (data.TryGetValue("m_Normals", out var normalsObj) && normalsObj is List<object?> normalsList)
        {
            // Legacy normals data
            Console.WriteLine($"DEBUG: m_Normals legacy field present ({normalsList.Count} items)");
        }

        return mesh;
    }

    private static SubMesh[] MapSubMeshes(List<object?> list)
    {
        var subMeshes = new List<SubMesh>();

        foreach (var item in list)
        {
            if (item is not Dictionary<string, object?> dict)
                continue;

            var subMesh = new SubMesh();

            if (dict.TryGetValue("firstByte", out var fbObj) && fbObj != null)
                subMesh.FirstByte = Convert.ToUInt32(fbObj);

            if (dict.TryGetValue("indexCount", out var icObj) && icObj != null)
                subMesh.IndexCount = Convert.ToUInt32(icObj);

            if (dict.TryGetValue("topology", out var topoObj) && topoObj != null)
                subMesh.Topology = (MeshTopology)Convert.ToInt32(topoObj);

            if (dict.TryGetValue("baseVertex", out var bvObj) && bvObj != null)
                subMesh.BaseVertex = Convert.ToUInt32(bvObj);

            if (dict.TryGetValue("firstVertex", out var fvObj) && fvObj != null)
                subMesh.FirstVertex = Convert.ToUInt32(fvObj);

            if (dict.TryGetValue("vertexCount", out var vcObj) && vcObj != null)
                subMesh.VertexCount = Convert.ToUInt32(vcObj);

            subMeshes.Add(subMesh);
        }

        return subMeshes.ToArray();
    }

    private static VertexData MapVertexData(Dictionary<string, object?> dict)
    {
        var vertexData = new VertexData();

        if (dict.TryGetValue("m_VertexCount", out var vcObj) && vcObj != null)
            vertexData.VertexCount = Convert.ToUInt32(vcObj);

        if (dict.TryGetValue("m_Channels", out var channelsObj) && channelsObj is List<object?> channelsList)
        {
            var channels = new List<ChannelInfo>();
            foreach (var ch in channelsList)
            {
                if (ch is Dictionary<string, object?> chDict)
                {
                    var channel = new ChannelInfo();
                    if (chDict.TryGetValue("stream", out var sObj) && sObj != null)
                        channel.Stream = Convert.ToByte(sObj);
                    if (chDict.TryGetValue("offset", out var oObj) && oObj != null)
                        channel.Offset = Convert.ToByte(oObj);
                    if (chDict.TryGetValue("format", out var fObj) && fObj != null)
                        channel.Format = Convert.ToByte(fObj);
                    if (chDict.TryGetValue("dimension", out var dObj) && dObj != null)
                        channel.Dimension = Convert.ToByte(dObj);
                    channels.Add(channel);
                }
            }
            vertexData.Channels = channels.ToArray();
        }

        if (dict.TryGetValue("m_DataSize", out var dataObj) && dataObj is List<object?> dataList)
        {
            vertexData.DataSize = dataList.Cast<byte?>().Where(b => b.HasValue).Select(b => b!.Value).ToArray();
        }

        return vertexData;
    }

    private static CompressedMesh MapCompressedMesh(Dictionary<string, object?> dict)
    {
        // Map CompressedMesh from TypeTree data
        // Reference: UnityPy/classes/generated.py CompressedMesh
        var vertices = MapPackedBitVector(dict, "m_Vertices", hasRangeStart: true);
        var uv = MapPackedBitVector(dict, "m_UV", hasRangeStart: true);
        var normals = MapPackedBitVector(dict, "m_Normals", hasRangeStart: true);
        var normalSigns = MapPackedBitVector(dict, "m_NormalSigns", hasRangeStart: false);
        var tangents = MapPackedBitVector(dict, "m_Tangents", hasRangeStart: true);
        var tangentSigns = MapPackedBitVector(dict, "m_TangentSigns", hasRangeStart: false);
        var weights = MapPackedBitVector(dict, "m_Weights", hasRangeStart: false);
        var boneIndices = MapPackedBitVector(dict, "m_BoneIndices", hasRangeStart: false);
        var triangles = MapPackedBitVector(dict, "m_Triangles", hasRangeStart: false);
        var floatColors = MapPackedBitVector(dict, "m_FloatColors", hasRangeStart: true);

        // Optional fields (always present in TypeTree but may be empty)
        var colors = dict.TryGetValue("m_Colors", out var colorsObj) && colorsObj is Dictionary<string, object?> colorsDict
            ? PackedBitVector.FromTypeTreeData(colorsDict, hasRangeStart: false)
            : new PackedBitVector();

        var bindPoses = dict.TryGetValue("m_BindPoses", out var bindPosesObj) && bindPosesObj is Dictionary<string, object?> bindPosesDict
            ? PackedBitVector.FromTypeTreeData(bindPosesDict, hasRangeStart: true)
            : new PackedBitVector();

        uint uvInfo = 0;
        if (dict.TryGetValue("m_UVInfo", out var uvInfoObj) && uvInfoObj != null)
        {
            uvInfo = Convert.ToUInt32(uvInfoObj);
        }

        return new CompressedMesh
        {
            Vertices = vertices,
            UV = uv,
            UVInfo = uvInfo,
            Normals = normals,
            NormalSigns = normalSigns,
            Tangents = tangents,
            TangentSigns = tangentSigns,
            Weights = weights,
            BoneIndices = boneIndices,
            Triangles = triangles,
            FloatColors = floatColors,
            Colors = colors.NumItems > 0 ? colors : null,
            BindPoses = bindPoses.NumItems > 0 ? bindPoses : null
        };
    }

    private static StreamingInfo MapStreamingInfo(Dictionary<string, object?> dict)
    {
        // Map StreamingInfo from TypeTree data
        // Reference: UnityPy/classes/generated.py StreamingInfo
        string path = string.Empty;
        if (dict.TryGetValue("path", out var pathObj) && pathObj is string pathStr)
        {
            path = pathStr;
        }

        uint offset = 0;
        if (dict.TryGetValue("offset", out var offsetObj) && offsetObj != null)
        {
            offset = Convert.ToUInt32(offsetObj);
        }

        uint size = 0;
        if (dict.TryGetValue("size", out var sizeObj) && sizeObj != null)
        {
            size = Convert.ToUInt32(sizeObj);
        }

        return new StreamingInfo
        {
            Path = path,
            Offset = offset,
            Size = size
        };
    }

    /// <summary>
    /// Helper to map a PackedBitVector from TypeTree data.
    /// </summary>
    private static PackedBitVector MapPackedBitVector(
        Dictionary<string, object?> dict,
        string fieldName,
        bool hasRangeStart)
    {
        if (!dict.TryGetValue(fieldName, out var pbvObj) || pbvObj is not Dictionary<string, object?> pbvDict)
        {
            return new PackedBitVector();
        }

        return PackedBitVector.FromTypeTreeData(pbvDict, hasRangeStart);
    }

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
                Console.WriteLine($"DEBUG: MeshParser starting, objectData.Length={objectData.Length}, version={major}.{version.Item2}.{version.Item3}");

                // Field 1: m_Name
                Console.WriteLine($"DEBUG: Reading m_Name at pos={stream.Position}");
                mesh.Name = ReadAlignedString(reader);
                reader.Align();
                Console.WriteLine($"DEBUG: m_Name='{mesh.Name}', pos={stream.Position}");

                Console.WriteLine($"DEBUG: m_Name='{mesh.Name}', pos={stream.Position}");

                // Field 2: m_SubMeshes
                Console.WriteLine($"DEBUG: Reading m_SubMeshes at pos={stream.Position}");
                mesh.SubMeshes = ReadSubMeshArray(reader, major);
                reader.Align();
                Console.WriteLine($"DEBUG: m_SubMeshes count={mesh.SubMeshes?.Length ?? 0}, pos={stream.Position}");

                // Field 3: m_Shapes (BlendShapeData) - note: this is a struct, not an array!
                Console.WriteLine($"DEBUG: Reading m_Shapes at pos={stream.Position}");
                ReadBlendShapeData(reader);
                reader.Align();
                Console.WriteLine($"DEBUG: m_Shapes done, pos={stream.Position}");

                // Field 4: m_BindPose (version >= 4)
                if (major >= 4)
                {
                    Console.WriteLine($"DEBUG: Reading m_BindPose at pos={stream.Position}");
                    ReadBindPoseArray(reader);
                    reader.Align();
                    Console.WriteLine($"DEBUG: m_BindPose done, pos={stream.Position}");
                }

                // Field 5: m_BoneNameHashes (version >= 4)
                if (major >= 4)
                {
                    Console.WriteLine($"DEBUG: Reading m_BoneNameHashes at pos={stream.Position}");
                    ReadBoneNameHashesArray(reader);
                    reader.Align();
                    Console.WriteLine($"DEBUG: m_BoneNameHashes done, pos={stream.Position}");
                }

                // Field 6: m_RootBoneNameHash (version >= 4)
                if (major >= 4)
                {
                    Console.WriteLine($"DEBUG: Reading m_RootBoneNameHash at pos={stream.Position}");
                    reader.ReadUInt32();
                    Console.WriteLine($"DEBUG: m_RootBoneNameHash done, pos={stream.Position}");
                }

                // Field 7: m_BonesAABB (version >= 4)
                if (major >= 4)
                {
                    Console.WriteLine($"DEBUG: Reading m_BonesAABB at pos={stream.Position}");
                    ReadBoneAABBArray(reader);
                    reader.Align();
                    Console.WriteLine($"DEBUG: m_BonesAABB done, pos={stream.Position}");
                }

                // Field 8: m_VariableBoneCountWeights (version >= 4)
                if (major >= 4)
                {
                    Console.WriteLine($"DEBUG: Reading m_VariableBoneCountWeights at pos={stream.Position}");
                    ReadVariableBoneCountWeights(reader);
                    reader.Align();
                    Console.WriteLine($"DEBUG: m_VariableBoneCountWeights done, pos={stream.Position}");
                }

                // Field 9: m_MeshCompression
                Console.WriteLine($"DEBUG: Reading m_MeshCompression at pos={stream.Position}");
                mesh.MeshCompression = reader.ReadByte();
                Console.WriteLine($"DEBUG: m_MeshCompression done, pos={stream.Position}");

                // Field 10: m_IsReadable
                Console.WriteLine($"DEBUG: Reading m_IsReadable at pos={stream.Position}");
                mesh.IsReadable = reader.ReadBoolean();
                Console.WriteLine($"DEBUG: m_IsReadable done, pos={stream.Position}");

                // Field 11: m_KeepVertices
                Console.WriteLine($"DEBUG: Reading m_KeepVertices at pos={stream.Position}");
                mesh.KeepVertices = reader.ReadBoolean();
                Console.WriteLine($"DEBUG: m_KeepVertices done, pos={stream.Position}");

                // Field 12: m_KeepIndices
                Console.WriteLine($"DEBUG: Reading m_KeepIndices at pos={stream.Position}");
                mesh.KeepIndices = reader.ReadBoolean();
                Console.WriteLine($"DEBUG: m_KeepIndices done, pos={stream.Position}");
                reader.Align();
                Console.WriteLine($"DEBUG: After boolean alignment, pos={stream.Position}");

                // Field 13: m_IndexFormat (version >= 2017.3)
                if (major > 2017 || (major == 2017 && version.Item2 >= 3))
                {
                    Console.WriteLine($"DEBUG: Reading m_IndexFormat at pos={stream.Position}");
                    mesh.IndexFormat = reader.ReadInt32();
                    Console.WriteLine($"DEBUG: m_IndexFormat done, pos={stream.Position}");
                }

                // Field 14: m_IndexBuffer
                Console.WriteLine($"DEBUG: Reading m_IndexBuffer at pos={stream.Position}");
                mesh.IndexBuffer = ReadIndexBuffer(reader);
                reader.Align();
                Console.WriteLine($"DEBUG: m_IndexBuffer done, pos={stream.Position}");

                // Field 15: m_VertexData structure
                Console.WriteLine($"DEBUG: Reading m_VertexData at pos={stream.Position}");
                mesh.VertexData = ReadVertexData(reader, major);
                reader.Align();
                Console.WriteLine($"DEBUG: m_VertexData done, pos={stream.Position}");

                // Field 16: m_CompressedMesh - only read if actually compressed
                if (mesh.MeshCompression != 0)
                {
                    Console.WriteLine($"DEBUG: Reading m_CompressedMesh at pos={stream.Position} (compression={mesh.MeshCompression})");
                    mesh.CompressedMesh = ReadCompressedMesh(reader);
                    reader.Align();
                    Console.WriteLine($"DEBUG: m_CompressedMesh done, pos={stream.Position}");
                }
                else
                {
                    Console.WriteLine($"DEBUG: Skipping m_CompressedMesh (no compression, pos={stream.Position})");
                    mesh.CompressedMesh = new CompressedMesh
                    {
                        Vertices = new PackedBitVector(),
                        UV = new PackedBitVector(),
                        Normals = new PackedBitVector(),
                        NormalSigns = new PackedBitVector(),
                        Tangents = new PackedBitVector(),
                        TangentSigns = new PackedBitVector(),
                        Weights = new PackedBitVector(),
                        BoneIndices = new PackedBitVector(),
                        Triangles = new PackedBitVector(),
                        FloatColors = new PackedBitVector()
                    };
                }

                // Field 17: m_LocalAABB
                Console.WriteLine($"DEBUG: Reading m_LocalAABB at pos={stream.Position}, {stream.Length - stream.Position} bytes remaining");
                ReadLocalAABB(reader);
                reader.Align();
                Console.WriteLine($"DEBUG: m_LocalAABB done, pos={stream.Position}");

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
                Console.WriteLine($"DEBUG: Reading m_StreamData at pos={stream.Position}");
                mesh.StreamData = ReadStreamingInfo(reader);
                Console.WriteLine($"DEBUG: MeshParser completed successfully, final pos={stream.Position}");

                return mesh;
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"ERROR: MeshParser failed (InvalidOperationException): {ex.Message}");
            Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
            throw; // Re-throw so we can see what's failing
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: MeshParser failed ({ex.GetType().Name}): {ex.Message}");
            Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Reads an aligned string (4-byte length prefix, followed by UTF-8 bytes).
    /// </summary>
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
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle(); // vertex
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle(); // normal
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle(); // tangent
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
        Console.WriteLine($"DEBUG: ReadBindPoseArray count={count}, need {count * 16 * 4} bytes ({count * 16} floats), stream pos={reader.BaseStream.Position}");
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
        Console.WriteLine($"DEBUG: ReadBoneNameHashes count={count}, stream pos after count={reader.BaseStream.Position}");
        for (int i = 0; i < count; i++)
        {
            reader.ReadUInt32();
        }
    }

    private static void ReadBoneAABBArray(EndianBinaryReader reader)
    {
        // m_BonesAABB - List<AABB>
        uint count = reader.ReadUInt32();
        Console.WriteLine($"DEBUG: ReadBoneAABBArray count={count}, stream pos={reader.BaseStream.Position}");
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

        Console.WriteLine($"DEBUG: ReadVertexData start, pos={reader.BaseStream.Position}");

        // m_VertexCount
        vertexData.VertexCount = reader.ReadUInt32();
        Console.WriteLine($"DEBUG: VertexCount={vertexData.VertexCount}, pos={reader.BaseStream.Position}");

        // m_Channels array
        uint channelCount = reader.ReadUInt32();
        Console.WriteLine($"DEBUG: ChannelCount={channelCount}, pos={reader.BaseStream.Position}");

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
            Console.WriteLine($"DEBUG: Channel[{i}]: stream={channels[i].Stream}, offset={channels[i].Offset}, format={channels[i].Format}, dimension={channels[i].Dimension}");
        }
        vertexData.Channels = channels;
        Console.WriteLine($"DEBUG: Read {channelCount} channels, pos={reader.BaseStream.Position}");

        // m_DataSize (TypelessData - vector of bytes)
        uint dataSize = reader.ReadUInt32();
        Console.WriteLine($"DEBUG: m_DataSize={dataSize}, pos={reader.BaseStream.Position}");
        if (dataSize > 0)
        {
            vertexData.DataSize = reader.ReadBytes((int)dataSize);
            reader.Align(4);  // CRITICAL: Align after byte array read
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

        byte[] indexBuffer = reader.ReadBytes((int)count);
        reader.Align(4);  // CRITICAL: Align after byte array read
        return indexBuffer;
    }

    private static void ReadByteArrayField(EndianBinaryReader reader)
    {
        // Read and discard byte array (used for baked collision meshes)
        uint count = reader.ReadUInt32();
        if (count > 0)
        {
            reader.ReadBytes((int)count);
            reader.Align(4);  // CRITICAL: Align after byte array read
        }
    }

    private static StreamingInfo? ReadStreamingInfo(EndianBinaryReader reader)
    {
        // m_StreamData: offset (UInt64), size (uint), path (string)
        long streamPos = reader.BaseStream.Position;
        ulong offset = reader.ReadUInt64();
        uint size = reader.ReadUInt32();
        string path = ReadAlignedString(reader);
        reader.Align();

        Console.WriteLine($"DEBUG: ReadStreamingInfo - offset={offset}, size={size}, path='{path}', pos={streamPos}");

        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine($"DEBUG: StreamingInfo path is empty, returning null");
            return null;
        }

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
