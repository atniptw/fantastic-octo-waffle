using System;
using UnityAssetParser.Classes;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Services;

/// <summary>
/// Extracts and decompresses vertex data from Mesh objects.
/// Handles both inline vertex data and external StreamingInfo resources.
/// </summary>
public static class VertexDataExtractor
{
    /// <summary>
    /// Extracts vertex positions from a Mesh object.
    /// Supports both inline VertexData and external .resS resources via StreamingInfo.
    /// </summary>
    /// <param name="mesh">Parsed Mesh object</param>
    /// <param name="externalResourceData">Raw bytes from external .resS resource file (optional)</param>
    /// <returns>Float array of vertex positions (3 floats per vertex: X, Y, Z)</returns>
    public static float[] ExtractVertexPositions(Mesh mesh, byte[]? externalResourceData = null)
    {
        Console.WriteLine($"DEBUG: ExtractVertexPositions - VertexCount={mesh.VertexData?.VertexCount ?? 0}");
        
        if (mesh.VertexData?.VertexCount == 0)
        {
            Console.WriteLine($"DEBUG: No vertices - VertexCount is 0");
            return Array.Empty<float>();
        }

        // Check if vertices are in CompressedMesh
        if (mesh.CompressedMesh?.Vertices != null && mesh.CompressedMesh.Vertices.NumItems > 0)
        {
            Console.WriteLine($"DEBUG: Using CompressedMesh.Vertices, NumItems={mesh.CompressedMesh.Vertices.NumItems}");
            return DecompressVertices(mesh.CompressedMesh.Vertices);
        }

        // Check if vertices are in external resource (StreamingInfo)
        if (mesh.StreamData != null && externalResourceData != null)
        {
            Console.WriteLine($"DEBUG: Using StreamData - Path={mesh.StreamData.Path}, Offset={mesh.StreamData.Offset}, Size={mesh.StreamData.Size}, ExternalDataSize={externalResourceData.Length}");
            return ExtractFromStreamingInfo(mesh, externalResourceData);
        }

        // Check if vertices are inline in VertexData.DataSize
        if (mesh.VertexData?.DataSize != null && mesh.VertexData.DataSize.Length > 0)
        {
            Console.WriteLine($"DEBUG: Using VertexData.DataSize, size={mesh.VertexData.DataSize.Length}");
            return ExtractFromVertexDataBuffer(mesh);
        }

        // When StreamData is null but we have external resource and valid channels, try extracting from external resource
        if (externalResourceData != null && mesh.VertexData?.Channels != null && mesh.VertexData.Channels.Length > 0)
        {
            Console.WriteLine($"DEBUG: Attempting to extract from external resource (no StreamData path), ExternalDataSize={externalResourceData.Length}");
            return ExtractFromExternalResource(mesh, externalResourceData);
        }

        // No vertex data found
        Console.WriteLine($"DEBUG: No vertex data found - CompressedMesh={mesh.CompressedMesh != null}, StreamData={mesh.StreamData != null}, DataSize={mesh.VertexData?.DataSize?.Length ?? 0}");
        return Array.Empty<float>();
    }

    /// <summary>
    /// Extracts vertex normals from a Mesh object.
    /// </summary>
    public static float[] ExtractVertexNormals(Mesh mesh, byte[]? externalResourceData = null)
    {
        if (mesh.VertexData?.VertexCount == 0)
        {
            return Array.Empty<float>();
        }

        // Check if normals are in CompressedMesh
        if (mesh.CompressedMesh?.Normals != null && mesh.CompressedMesh.Normals.NumItems > 0)
        {
            return DecompressNormals(mesh.CompressedMesh.Normals);
        }

        return Array.Empty<float>();
    }

    /// <summary>
    /// Extracts UV coordinates from a Mesh object.
    /// </summary>
    public static float[] ExtractVertexUVs(Mesh mesh, byte[]? externalResourceData = null)
    {
        if (mesh.VertexData?.VertexCount == 0)
        {
            return Array.Empty<float>();
        }

        // Check if UVs are in CompressedMesh
        if (mesh.CompressedMesh?.UV != null && mesh.CompressedMesh.UV.NumItems > 0)
        {
            return DecompressUV(mesh.CompressedMesh.UV);
        }

        return Array.Empty<float>();
    }

    /// <summary>
    /// Decompresses vertex positions from a PackedBitVector.
    /// Applies range scaling if present.
    /// </summary>
    private static float[] DecompressVertices(PackedBitVector pbv)
    {
        if (pbv.NumItems == 0)
        {
            return Array.Empty<float>();
        }

        byte bitSize = pbv.BitSize ?? 16; // Default to 16 bits if not specified
        var positions = new float[pbv.NumItems * 3];
        int posIndex = 0;

        for (int i = 0; i < pbv.NumItems; i++)
        {
            // Each vertex has 3 components (X, Y, Z)
            for (int j = 0; j < 3; j++)
            {
                uint packedValue = ExtractPackedValue(pbv, i * 3 + j, bitSize);

                // Apply range scaling if present
                float value;
                if (bitSize == 32)
                {
                    // Already a full float
                    value = BitConverter.Int32BitsToSingle((int)packedValue);
                }
                else if (pbv.Range.HasValue && pbv.Start.HasValue)
                {
                    // Quantized value: dequantize using range and start
                    float maxQuantized = (1 << bitSize) - 1; // (2^bitSize) - 1
                    value = pbv.Start.Value + ((float)packedValue / maxQuantized) * pbv.Range.Value;
                }
                else
                {
                    // No range info, treat as raw float bits
                    value = BitConverter.Int32BitsToSingle((int)packedValue);
                }

                positions[posIndex++] = value;
            }
        }

        return positions;
    }

    /// <summary>
    /// Decompresses vertex normals from a PackedBitVector.
    /// </summary>
    private static float[] DecompressNormals(PackedBitVector pbv)
    {
        // Similar to DecompressVertices but for 3-component normals
        return DecompressVertices(pbv);
    }

    /// <summary>
    /// Decompresses UV coordinates from a PackedBitVector.
    /// </summary>
    private static float[] DecompressUV(PackedBitVector pbv)
    {
        if (pbv.NumItems == 0)
        {
            return Array.Empty<float>();
        }

        byte bitSize = pbv.BitSize ?? 16; // Default to 16 bits if not specified
        var uvs = new float[pbv.NumItems * 2];
        int posIndex = 0;

        for (int i = 0; i < pbv.NumItems; i++)
        {
            for (int j = 0; j < 2; j++) // 2 components for UV
            {
                uint packedValue = ExtractPackedValue(pbv, i * 2 + j, bitSize);
                
                float value;
                if (bitSize == 32)
                {
                    value = BitConverter.Int32BitsToSingle((int)packedValue);
                }
                else if (pbv.Range.HasValue && pbv.Start.HasValue)
                {
                    float maxQuantized = (1 << bitSize) - 1;
                    value = pbv.Start.Value + ((float)packedValue / maxQuantized) * pbv.Range.Value;
                }
                else
                {
                    value = BitConverter.Int32BitsToSingle((int)packedValue);
                }

                uvs[posIndex++] = value;
            }
        }

        return uvs;
    }

    /// <summary>
    /// Extracts a single packed value from PackedBitVector data.
    /// </summary>
    private static uint ExtractPackedValue(PackedBitVector pbv, int index, byte bitSize)
    {
        if (pbv.Data == null || pbv.Data.Length == 0)
        {
            return 0;
        }

        int bitOffset = index * bitSize;
        int byteOffset = bitOffset / 8;
        int bitInByte = bitOffset % 8;

        uint value = 0;
        int bitsRead = 0;

        while (bitsRead < bitSize && byteOffset < pbv.Data.Length)
        {
            int bitsAvailableInByte = 8 - bitInByte;
            int bitsToRead = Math.Min(bitSize - bitsRead, bitsAvailableInByte);

            byte mask = (byte)((1 << bitsToRead) - 1);
            byte bits = (byte)((pbv.Data[byteOffset] >> bitInByte) & mask);

            value |= (uint)bits << bitsRead;

            bitsRead += bitsToRead;
            byteOffset++;
            bitInByte = 0;
        }

        return value;
    }

    /// <summary>
    /// Extracts vertex data from external StreamingInfo resource.
    /// </summary>
    private static float[] ExtractFromStreamingInfo(Mesh mesh, byte[] resourceData)
    {
        if (mesh.StreamData == null)
        {
            return Array.Empty<float>();
        }

        int offset = (int)mesh.StreamData.Offset;
        int size = (int)mesh.StreamData.Size;

        if (offset + size > resourceData.Length)
        {
            return Array.Empty<float>();
        }

        // Extract the vertex data region from resource
        var vertexBytes = new ReadOnlySpan<byte>(resourceData, offset, size);

        // Parse as vertex data based on VertexData channels
        return ParseVertexDataFromChannels(mesh, vertexBytes);
    }

    /// <summary>
    /// Extracts vertex data directly from external resource when StreamData.Path is empty.
    /// Unity stores vertex data in external resource (.resS) without a path reference.
    /// </summary>
    private static float[] ExtractFromExternalResource(Mesh mesh, byte[] resourceData)
    {
        Console.WriteLine($"DEBUG: ExtractFromExternalResource - resourceData.Length={resourceData.Length}");
        
        // Use the entire resource buffer - Unity stores vertex data at the beginning
        // The channels tell us the layout (stream, offset, format, dimension)
        return ParseVertexDataFromChannels(mesh, resourceData.AsSpan());
    }

    /// <summary>
    /// Extracts vertex data from inline VertexData.DataSize buffer.
    /// </summary>
    private static float[] ExtractFromVertexDataBuffer(Mesh mesh)
    {
        if (mesh.VertexData?.DataSize == null || mesh.VertexData.DataSize.Length == 0)
        {
            return Array.Empty<float>();
        }

        return ParseVertexDataFromChannels(mesh, mesh.VertexData.DataSize.AsSpan());
    }

    /// <summary>
    /// Parses vertex positions from raw vertex data using channel information.
    /// Expects interleaved vertex data based on VertexData.m_Channels.
    /// </summary>
    private static float[] ParseVertexDataFromChannels(Mesh mesh, ReadOnlySpan<byte> vertexData)
    {
        if (mesh.VertexData?.Channels == null || mesh.VertexData.Channels.Length == 0)
        {
            Console.WriteLine($"DEBUG: ParseVertexDataFromChannels - No channels");
            return Array.Empty<float>();
        }

        int vertexCount = (int)(mesh.VertexData?.VertexCount ?? 0u);
        if (vertexCount == 0)
        {
            Console.WriteLine($"DEBUG: ParseVertexDataFromChannels - VertexCount is 0");
            return Array.Empty<float>();
        }

        // Find position channel (typically Stream=0, dimension bits indicate 3 components)
        // Note: Dimension field uses low 4 bits, so we need to mask it
        var positionChannel = Array.Find(mesh.VertexData.Channels,
            ch => ch.Stream == 0 && (ch.Dimension & 0xF) == 3 && ch.Format == 0);

        if (positionChannel == null)
        {
            Console.WriteLine($"DEBUG: ParseVertexDataFromChannels - No position channel found");
            foreach (var ch in mesh.VertexData.Channels)
            {
                Console.WriteLine($"DEBUG: Channel: stream={ch.Stream}, offset={ch.Offset}, format={ch.Format}, dimension={ch.Dimension} (masked={(ch.Dimension & 0xF)})");
            }
            return Array.Empty<float>();
        }

        Console.WriteLine($"DEBUG: Found position channel - offset={positionChannel.Offset}, format={positionChannel.Format}, dimension={positionChannel.Dimension & 0xF}");

        // Calculate vertex stride (max offset + size of that channel)
        int maxOffset = 0;
        int maxChannelSize = 0;
        foreach (var ch in mesh.VertexData.Channels)
        {
            if (ch.Stream == 0 && (ch.Dimension & 0xF) > 0)
            {
                int channelSize = GetChannelSize(ch);
                if (ch.Offset + channelSize > maxOffset + maxChannelSize)
                {
                    maxOffset = ch.Offset;
                    maxChannelSize = channelSize;
                }
            }
        }
        int vertexStride = maxOffset + maxChannelSize;
        Console.WriteLine($"DEBUG: Calculated vertex stride = {vertexStride} bytes");

        var positions = new float[vertexCount * 3];

        // Parse vertex positions from interleaved data using channel offset
        for (int i = 0; i < vertexCount; i++)
        {
            int vertexOffset = i * vertexStride + positionChannel.Offset;

            if (vertexOffset + 12 <= vertexData.Length)
            {
                positions[i * 3 + 0] = BitConverter.ToSingle(vertexData.Slice(vertexOffset, 4));
                positions[i * 3 + 1] = BitConverter.ToSingle(vertexData.Slice(vertexOffset + 4, 4));
                positions[i * 3 + 2] = BitConverter.ToSingle(vertexData.Slice(vertexOffset + 8, 4));
            }
            else
            {
                Console.WriteLine($"DEBUG: Not enough data for vertex {i}: need {vertexOffset + 12}, have {vertexData.Length}");
                break;
            }
        }

        Console.WriteLine($"DEBUG: Extracted {positions.Length / 3} vertex positions");
        return positions;
    }

    /// <summary>
    /// Calculates the size in bytes of a vertex channel based on format and dimension.
    /// </summary>
    private static int GetChannelSize(ChannelInfo channel)
    {
        int dimension = channel.Dimension & 0xF; // Mask to get actual dimension
        if (dimension == 0) return 0;

        // Format: 0=Float, 1=Float16, 2=UNorm8, 3=SNorm8, 4=UNorm16, 5=SNorm16, 6=UInt8, 7=SInt8, 8=UInt16, 9=SInt16, 10=UInt32, 11=SInt32
        int bytesPerComponent = channel.Format switch
        {
            0 => 4,  // Float32
            1 => 2,  // Float16/Half
            2 => 1,  // UNorm8
            3 => 1,  // SNorm8
            4 => 2,  // UNorm16
            5 => 2,  // SNorm16
            6 => 1,  // UInt8
            7 => 1,  // SInt8
            8 => 2,  // UInt16
            9 => 2,  // SInt16
            10 => 4, // UInt32
            11 => 4, // SInt32
            _ => 4   // Default to 4
        };

        return dimension * bytesPerComponent;
    }
}
