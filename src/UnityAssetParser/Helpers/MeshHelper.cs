using UnityAssetParser.Bundle;
using UnityAssetParser.Classes;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Helper class for extracting renderable mesh data from Unity Mesh objects.
/// This is a verbatim port from UnityPy/helpers/MeshHelper.py.
/// 
/// Extracts positions, indices, normals, and UVs from Unity mesh formats, handling:
/// - VertexData channels/streams (version-specific layouts)
/// - CompressedMesh (PackedBitVector unpacking)
/// - Index buffer format selection (UInt16 vs UInt32)
/// - External .resS streaming data
/// - Multiple Unity version formats (< 4, 4, >= 5, >= 2018)
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/MeshHelper.py
/// </summary>
#pragma warning disable CA1819 // Properties should not return arrays (performance requirement for vertex data)
#pragma warning disable CA1031 // Do not catch general Exception type (streaming resolver needs broad error handling)
#pragma warning disable CA1502 // Avoid excessive complexity (format enumeration requires many cases)
#pragma warning disable CA1508 // Avoid dead code (false positive on null checks)
public sealed class MeshHelper
{
    private readonly Mesh _mesh;
    private readonly (int, int, int, int) _version;
    private readonly bool _isLittleEndian;
    private readonly StreamingInfoResolver? _streamingResolver;
    private readonly IReadOnlyList<NodeInfo>? _nodes;
    private readonly DataRegion? _dataRegion;

    // Extracted geometry data (output)
    private int _vertexCount;
    private float[]? _positions;  // Flat XYZ array: [x0,y0,z0, x1,y1,z1, ...]
    private float[]? _normals;    // Flat XYZ array
    private float[]? _uvs;        // Flat UV array: [u0,v0, u1,v1, ...]
    private float[]? _colors;     // Flat RGBA array: [r0,g0,b0,a0, r1,g1,b1,a1, ...]
    private float[]? _tangents;   // Flat XYZW array: [x0,y0,z0,w0, x1,y1,z1,w1, ...]
    private float[]? _uv2;        // Additional UV set (TexCoord1)
    private float[]? _uv3;        // Third UV set (TexCoord2)
    private uint[]? _indices;     // Triangle indices
    private bool _use16BitIndices = true;
    private bool _isBigEndian => !_isLittleEndian;

    /// <summary>
    /// Gets the vertex count.
    /// </summary>
    public int VertexCount => _vertexCount;

    /// <summary>
    /// Gets the positions as a flat float array (length = VertexCount * 3).
    /// Format: [x0, y0, z0, x1, y1, z1, ...]
    /// </summary>
    public float[]? Positions => _positions;

    /// <summary>
    /// Gets the normals as a flat float array (length = VertexCount * 3), or null if not present.
    /// Format: [nx0, ny0, nz0, nx1, ny1, nz1, ...]
    /// </summary>
    public float[]? Normals => _normals;

    /// <summary>
    /// Gets the UVs as a flat float array (length = VertexCount * 2), or null if not present.
    /// Format: [u0, v0, u1, v1, ...]
    /// </summary>
    public float[]? UVs => _uvs;

    /// <summary>
    /// Gets the colors as a flat float array (length = VertexCount * 4), or null if not present.
    /// Format: [r0, g0, b0, a0, r1, g1, b1, a1, ...]
    /// </summary>
    public float[]? Colors => _colors;

    /// <summary>
    /// Gets the tangents as a flat float array (length = VertexCount * 4), or null if not present.
    /// Format: [x0, y0, z0, w0, x1, y1, z1, w1, ...]
    /// </summary>
    public float[]? Tangents => _tangents;

    /// <summary>
    /// Gets the second UV set (TexCoord1) as a flat float array (length = VertexCount * 2), or null if not present.
    /// Format: [u0, v0, u1, v1, ...]
    /// </summary>
    public float[]? UV2 => _uv2;

    /// <summary>
    /// Gets the third UV set (TexCoord2) as a flat float array (length = VertexCount * 2), or null if not present.
    /// Format: [u0, v0, u1, v1, ...]
    /// </summary>
    public float[]? UV3 => _uv3;

    /// <summary>
    /// Gets the triangle indices as a uint array (length = IndexCount).
    /// For triangles topology, this is a multiple of 3.
    /// </summary>
    public uint[]? Indices => _indices;

    /// <summary>
    /// Gets whether 16-bit indices are used (vs 32-bit).
    /// </summary>
    public bool Use16BitIndices => _use16BitIndices;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshHelper"/> class.
    /// </summary>
    /// <param name="mesh">The mesh to extract geometry from.</param>
    /// <param name="version">Unity version tuple (major, minor, patch, type).</param>
    /// <param name="isLittleEndian">Whether the mesh data is little-endian (default: true).</param>
    /// <param name="nodes">Optional list of nodes for resolving StreamingInfo references.</param>
    /// <param name="dataRegion">Optional data region for loading external .resS resource data.</param>
    public MeshHelper(
        Mesh mesh,
        (int, int, int, int) version,
        bool isLittleEndian = true,
        IReadOnlyList<NodeInfo>? nodes = null,
        DataRegion? dataRegion = null)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _version = version;
        _isLittleEndian = isLittleEndian;
        _nodes = nodes;
        _dataRegion = dataRegion;
        _streamingResolver = nodes != null && dataRegion != null ? new StreamingInfoResolver() : null;
    }

    /// <summary>
    /// Processes the mesh and extracts all geometry data.
    /// This is the main entry point that orchestrates the extraction pipeline.
    /// Verbatim port of MeshHandler.process() from UnityPy.
    /// </summary>
    public void Process()
    {
        var vertexData = _mesh.VertexData;
        if (vertexData == null)
        {
            throw new InvalidOperationException("Mesh has no VertexData");
        }

        ChannelInfo[] channels;
        StreamInfo[] streams;

        // Version-specific channel/stream extraction (verbatim from UnityPy)
        if (_version.Item1 < 4)
        {
            // Unity version < 4: streams stored separately
            if (vertexData.Streams0 == null || vertexData.Streams1 == null ||
                vertexData.Streams2 == null || vertexData.Streams3 == null)
            {
                throw new InvalidOperationException("Legacy streams not present for Unity < 4");
            }

            streams = new[]
            {
                vertexData.Streams0,
                vertexData.Streams1,
                vertexData.Streams2,
                vertexData.Streams3
            };
            channels = GetChannels(streams);
        }
        else if (_version.Item1 == 4)
        {
            // Unity version 4: both channels and streams are explicit
            if (vertexData.Streams == null || vertexData.Channels == null)
            {
                throw new InvalidOperationException("Streams or Channels not present for Unity 4");
            }

            streams = vertexData.Streams;
            channels = vertexData.Channels;
        }
        else
        {
            // Unity version >= 5: compute streams from channels
            if (vertexData.Channels == null)
            {
                throw new InvalidOperationException("Channels not present for Unity >= 5");
            }

            channels = vertexData.Channels;
            streams = GetStreams(channels, vertexData.VertexCount);
        }

        // Handle streaming data from external .resS file
        if (_mesh.StreamData != null && !string.IsNullOrEmpty(_mesh.StreamData.Path))
        {
            if (_streamingResolver != null && _nodes != null && _dataRegion != null && vertexData != null && vertexData.DataSize == null)
            {
                try
                {
                    var externalData = _streamingResolver.Resolve(_nodes, _dataRegion, _mesh.StreamData);
                    vertexData.DataSize = externalData.ToArray();
                    Console.WriteLine($"DEBUG: Loaded external .resS data: Path={_mesh.StreamData.Path}, Size={vertexData.DataSize.Length} bytes");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Failed to load external .resS data: {ex.Message}");
                    // Continue without external data - vertex extraction will fail gracefully
                }
            }
            else if (_streamingResolver == null)
            {
                Console.WriteLine($"DEBUG: StreamingInfo present but no resolver available. External data not loaded.");
            }
        }

        // Handle index buffer format
        if (_mesh.Use16BitIndices != null)
        {
            _use16BitIndices = _mesh.Use16BitIndices.Value;
        }
        else if ((CompareVersion(_version, (2017, 4, 0, 0)) >= 0 ||
                 (_version.Item1 == 2017 && _version.Item2 == 3 && _mesh.MeshCompression == 0)) &&
                 _mesh.IndexFormat != null)
        {
            // Unity 2017.4+ or 2017.3 with no compression: use IndexFormat field
            _use16BitIndices = _mesh.IndexFormat.Value == 0;
        }

        // Copy index buffer if present
        if (_mesh.IndexBuffer != null && _mesh.IndexBuffer.Length > 0)
        {
            _indices = UnpackIndexBuffer(_mesh.IndexBuffer, _use16BitIndices, _isLittleEndian);
        }

        // Read vertex data from channels/streams (Unity 3.5+)
        if (CompareVersion(_version, (3, 5, 0, 0)) >= 0)
        {
            ReadVertexData(channels, streams, vertexData);
        }

        // Decompress compressed mesh (Unity 2.6+)
        if (CompareVersion(_version, (2, 6, 0, 0)) >= 0 && _mesh.CompressedMesh != null)
        {
            DecompressCompressedMesh(_mesh.CompressedMesh);
        }

        // Finalize vertex count
        if (_vertexCount == 0 && _positions != null)
        {
            _vertexCount = _positions.Length / 3;
        }
    }

    /// <summary>
    /// Unpacks the index buffer from raw bytes to uint array.
    /// Respects endianness flag (UnityPy: MeshHelper.py lines 159-185).
    /// CRITICAL: Must use little-endian format string per UnityPy struct.unpack("<...") usage.
    /// </summary>
    private static uint[] UnpackIndexBuffer(byte[] rawIndices, bool use16Bit, bool isLittleEndian)
    {
        if (use16Bit)
        {
            // 16-bit indices (UInt16)
            var count = rawIndices.Length / 2;
            var indices = new uint[count];
            for (var i = 0; i < count; i++)
            {
                ushort value = BitConverter.ToUInt16(rawIndices, i * 2);

                // If big-endian, byte-swap the 16-bit value
                if (!isLittleEndian)
                {
                    value = (ushort)((value << 8) | (value >> 8));
                }

                indices[i] = value;
            }
            return indices;
        }
        else
        {
            // 32-bit indices (UInt32)
            var count = rawIndices.Length / 4;
            var indices = new uint[count];
            for (var i = 0; i < count; i++)
            {
                uint value = BitConverter.ToUInt32(rawIndices, i * 4);

                // If big-endian, byte-swap the 32-bit value
                if (!isLittleEndian)
                {
                    value = ((value & 0x000000FF) << 24) |
                            ((value & 0x0000FF00) << 8) |
                            ((value & 0x00FF0000) >> 8) |
                            ((value & 0xFF000000) >> 24);
                }

                indices[i] = value;
            }
            return indices;
        }
    }

    /// <summary>
    /// Computes stream descriptors from channel descriptors.
    /// Verbatim port of MeshHandler.get_streams() from UnityPy.
    /// </summary>
    private StreamInfo[] GetStreams(ChannelInfo[] channels, uint vertexCount)
    {
        // Find the maximum stream index to determine stream count
        var streamCount = 1 + channels.Max(ch => ch.Stream);
        var streamsList = new List<StreamInfo>();
        uint offset = 0;

        for (byte s = 0; s < streamCount; s++)
        {
            uint channelMask = 0;
            uint stride = 0;

            // Calculate mask and stride for this stream
            foreach (var (chn, channel) in channels.Select((ch, idx) => (idx, ch)))
            {
                if (channel.Stream == s && channel.Dimension > 0)
                {
                    channelMask |= 1u << chn;
                    var componentSize = GetChannelComponentSize(channel);
                    stride += (uint)((channel.Dimension & 0xF) * componentSize);
                }
            }

            streamsList.Add(new StreamInfo
            {
                ChannelMask = channelMask,
                Offset = offset,
                Stride = stride,
                DividerOp = 0,
                Frequency = 0
            });

            offset += vertexCount * stride;
            offset = (offset + 15) & ~15u;  // Align to 16 bytes
        }

        return streamsList.ToArray();
    }

    /// <summary>
    /// Creates channel descriptors from stream descriptors (legacy Unity &lt; 4).
    /// Verbatim port of MeshHandler.get_channels() from UnityPy.
    /// </summary>
    private ChannelInfo[] GetChannels(StreamInfo[] streams)
    {
        var channels = new ChannelInfo[6];
        for (var i = 0; i < 6; i++)
        {
            channels[i] = new ChannelInfo
            {
                Dimension = 0,
                Format = 0,
                Offset = 0,
                Stream = 0
            };
        }

        for (byte s = 0; s < streams.Length; s++)
        {
            var stream = streams[s];
            var channelMask = stream.ChannelMask;
            byte offset = 0;

            for (var i = 0; i < 6; i++)
            {
                if ((channelMask & (1u << i)) != 0)
                {
                    var channel = channels[i];
                    channel.Stream = s;
                    channel.Offset = offset;

                    if (i == 0 || i == 1)
                    {
                        // Vertex or Normal
                        channel.Format = 0;  // Float
                        channel.Dimension = 3;
                    }
                    else if (i == 2)
                    {
                        // Color
                        channel.Format = 2;  // Color
                        channel.Dimension = 4;
                    }
                    else if (i == 3 || i == 4)
                    {
                        // TexCoord0 or TexCoord1
                        channel.Format = 0;  // Float
                        channel.Dimension = 2;
                    }
                    else if (i == 5)
                    {
                        // Tangent
                        channel.Format = 0;  // Float
                        channel.Dimension = 4;
                    }

                    var componentSize = GetChannelComponentSize(channel);
                    offset += (byte)(channel.Dimension * componentSize);
                }
            }
        }

        return channels;
    }

    /// <summary>
    /// Gets the byte size of a single component for a channel based on its format.
    /// Verbatim port of MeshHandler.get_channel_component_size() from UnityPy.
    /// </summary>
    private int GetChannelComponentSize(ChannelInfo channel)
    {
        // Map format enum to byte size based on Unity version
        if (_version.Item1 < 2017)
        {
            return channel.Format switch
            {
                0 => 4,  // Float
                1 => 2,  // Float16
                2 => 1,  // Color (byte)
                3 => 1,  // Byte
                4 => 4,  // UInt32
                _ => throw new NotSupportedException($"Unknown channel format: {channel.Format}")
            };
        }
        else if (_version.Item1 < 2019)
        {
            return channel.Format switch
            {
                0 => 4,  // Float
                1 => 2,  // Float16
                2 => 1,  // Color
                3 => 1,  // UNorm8
                4 => 1,  // SNorm8
                5 => 2,  // UNorm16
                6 => 2,  // SNorm16
                7 => 1,  // UInt8
                8 => 1,  // SInt8
                9 => 2,  // UInt16
                10 => 2, // SInt16
                11 => 4, // UInt32
                12 => 4, // SInt32
                _ => throw new NotSupportedException($"Unknown channel format: {channel.Format}")
            };
        }
        else
        {
            return channel.Format switch
            {
                0 => 4,  // Float
                1 => 2,  // Float16
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
                _ => throw new NotSupportedException($"Unknown channel format: {channel.Format}")
            };
        }
    }

    /// <summary>
    /// Reads vertex data from channels and streams, extracting positions, normals, and UVs.
    /// Verbatim port of MeshHandler.read_vertex_data() from UnityPy.
    /// </summary>
    private void ReadVertexData(ChannelInfo[] channels, StreamInfo[] streams, VertexData vertexData)
    {
        if (vertexData.DataSize == null || vertexData.DataSize.Length == 0)
        {
            return;
        }

        _vertexCount = (int)vertexData.VertexCount;
        var vertexDataRaw = vertexData.DataSize;

        foreach (var (chn, channel) in channels.Select((ch, idx) => (idx, ch)))
        {
            if (channel.Dimension == 0)
            {
                continue;
            }

            var stream = streams[channel.Stream];
            var channelMask = Convert.ToString(stream.ChannelMask, 2).PadLeft(32, '0');
            var channelMaskReversed = new string(channelMask.Reverse().ToArray());

            if (chn >= channelMaskReversed.Length || channelMaskReversed[chn] != '1')
            {
                continue;
            }

            // Handle Color channel format adjustment for Unity < 2018
            var effectiveChannel = channel;
            if (_version.Item1 < 2018 && chn == 2 && channel.Format == 2)
            {
                // Color channel
                effectiveChannel = new ChannelInfo
                {
                    Dimension = 4,
                    Format = 2,
                    Offset = channel.Offset,
                    Stream = channel.Stream
                };
            }

            var componentByteSize = GetChannelComponentSize(effectiveChannel);
            var channelDimension = effectiveChannel.Dimension & 0xF;
            var swap = !_isLittleEndian && componentByteSize > 1;

            // Extract component data for this channel
            var componentBytes = new byte[_vertexCount * channelDimension * componentByteSize];
            var vertexBaseOffset = (int)stream.Offset + effectiveChannel.Offset;

            for (var v = 0; v < _vertexCount; v++)
            {
                var vertexOffset = vertexBaseOffset + (int)stream.Stride * v;
                for (var d = 0; d < channelDimension; d++)
                {
                    var componentOffset = vertexOffset + componentByteSize * d;
                    var componentDataDst = componentByteSize * (v * channelDimension + d);

                    if (componentOffset + componentByteSize > vertexDataRaw.Length)
                    {
                        throw new InvalidOperationException(
                            $"Channel data out of bounds: offset={componentOffset}, " +
                            $"size={componentByteSize}, buffer={vertexDataRaw.Length}");
                    }

                    if (swap)
                    {
                        // Byte swap for big-endian
                        for (var b = 0; b < componentByteSize; b++)
                        {
                            componentBytes[componentDataDst + b] =
                                vertexDataRaw[componentOffset + (componentByteSize - 1 - b)];
                        }
                    }
                    else
                    {
                        Array.Copy(vertexDataRaw, componentOffset, componentBytes, componentDataDst, componentByteSize);
                    }
                }
            }

            // Unpack component bytes to floats and assign to appropriate channel
            var componentData = UnpackComponentData(componentBytes, effectiveChannel.Format, channelDimension);
            AssignChannelData(chn, componentData, channelDimension);
        }
    }

    /// <summary>
    /// Unpacks component bytes to float array based on format.
    /// Verbatim port from UnityPy/helpers/MeshHelper.py unpack_vertexdata.
    /// </summary>
    private float[] UnpackComponentData(byte[] componentBytes, byte format, int dimension)
    {
        var count = componentBytes.Length / GetFormatSize(format) / dimension;
        var result = new float[count * dimension];

        // Map format to C# type and unpack
        if (_version.Item1 < 2017)
        {
            switch ((VertexChannelFormat)format)
            {
                case VertexChannelFormat.Float:
                    UnpackFloats(componentBytes, result, dimension);
                    break;
                case VertexChannelFormat.Float16:
                    UnpackFloat16(componentBytes, result, dimension);
                    break;
                case VertexChannelFormat.Color:
                case VertexChannelFormat.Byte:
                    UnpackUNorm8(componentBytes, result, dimension);
                    break;
                case VertexChannelFormat.UInt32:
                    UnpackUInt32(componentBytes, result, dimension);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported format: {format}");
            }
        }
        else if (_version.Item1 < 2019)
        {
            switch ((VertexFormat2017)format)
            {
                case VertexFormat2017.Float:
                    UnpackFloats(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.Float16:
                    UnpackFloat16(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.Color:
                case VertexFormat2017.UNorm8:
                    UnpackUNorm8(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.SNorm8:
                    UnpackSNorm8(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.UInt8:
                    UnpackUInt8(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.SInt8:
                    UnpackSInt8(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.UNorm16:
                    UnpackUNorm16(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.SNorm16:
                    UnpackSNorm16(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.UInt16:
                    UnpackUInt16(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.SInt16:
                    UnpackSInt16(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.UInt32:
                    UnpackUInt32(componentBytes, result, dimension);
                    break;
                case VertexFormat2017.SInt32:
                    UnpackSInt32(componentBytes, result, dimension);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported format: {format}");
            }
        }
        else
        {
            switch ((VertexFormat)format)
            {
                case VertexFormat.Float:
                    UnpackFloats(componentBytes, result, dimension);
                    break;
                case VertexFormat.Float16:
                    UnpackFloat16(componentBytes, result, dimension);
                    break;
                case VertexFormat.UNorm8:
                    UnpackUNorm8(componentBytes, result, dimension);
                    break;
                case VertexFormat.SNorm8:
                    UnpackSNorm8(componentBytes, result, dimension);
                    break;
                case VertexFormat.UInt8:
                    UnpackUInt8(componentBytes, result, dimension);
                    break;
                case VertexFormat.SInt8:
                    UnpackSInt8(componentBytes, result, dimension);
                    break;
                case VertexFormat.UNorm16:
                    UnpackUNorm16(componentBytes, result, dimension);
                    break;
                case VertexFormat.SNorm16:
                    UnpackSNorm16(componentBytes, result, dimension);
                    break;
                case VertexFormat.UInt16:
                    UnpackUInt16(componentBytes, result, dimension);
                    break;
                case VertexFormat.SInt16:
                    UnpackSInt16(componentBytes, result, dimension);
                    break;
                case VertexFormat.UInt32:
                    UnpackUInt32(componentBytes, result, dimension);
                    break;
                case VertexFormat.SInt32:
                    UnpackSInt32(componentBytes, result, dimension);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported format: {format}");
            }
        }

        return result;
    }

    private int GetFormatSize(byte format)
    {
        if (_version.Item1 < 2017)
        {
            return ((VertexChannelFormat)format) switch
            {
                VertexChannelFormat.Float => 4,
                VertexChannelFormat.Float16 => 2,
                VertexChannelFormat.Color => 1,
                VertexChannelFormat.Byte => 1,
                VertexChannelFormat.UInt32 => 4,
                _ => 4
            };
        }
        else if (_version.Item1 < 2019)
        {
            return ((VertexFormat2017)format) switch
            {
                VertexFormat2017.Float => 4,
                VertexFormat2017.Float16 => 2,
                VertexFormat2017.Color or VertexFormat2017.UNorm8 or VertexFormat2017.SNorm8 or
                VertexFormat2017.UInt8 or VertexFormat2017.SInt8 => 1,
                VertexFormat2017.UNorm16 or VertexFormat2017.SNorm16 or
                VertexFormat2017.UInt16 or VertexFormat2017.SInt16 => 2,
                VertexFormat2017.UInt32 or VertexFormat2017.SInt32 => 4,
                _ => 4
            };
        }
        else
        {
            return ((VertexFormat)format) switch
            {
                VertexFormat.Float => 4,
                VertexFormat.Float16 => 2,
                VertexFormat.UNorm8 or VertexFormat.SNorm8 or VertexFormat.UInt8 or VertexFormat.SInt8 => 1,
                VertexFormat.UNorm16 or VertexFormat.SNorm16 or VertexFormat.UInt16 or VertexFormat.SInt16 => 2,
                VertexFormat.UInt32 or VertexFormat.SInt32 => 4,
                _ => 4
            };
        }
    }

    private static void UnpackFloats(byte[] src, float[] dst, int dimension)
    {
        var floatCount = src.Length / 4;
        for (var i = 0; i < floatCount; i++)
        {
            dst[i] = BitConverter.ToSingle(src, i * 4);
        }
    }

    private static void UnpackFloat16(byte[] src, float[] dst, int dimension)
    {
        var halfCount = src.Length / 2;
        for (var i = 0; i < halfCount; i++)
        {
            var half = BitConverter.ToUInt16(src, i * 2);
            // Convert Half to float using BitConverter in .NET
            dst[i] = (float)BitConverter.UInt16BitsToHalf(half);
        }
    }

    private void UnpackUNorm8(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = src[i] / 255f;
        }
    }

    private void UnpackSNorm8(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < src.Length; i++)
        {
            int signedByte = (sbyte)src[i];
            dst[i] = signedByte < 0 ? signedByte / 128f : signedByte / 127f;
        }
    }

    private void UnpackUInt8(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = src[i];
        }
    }

    private void UnpackSInt8(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = (sbyte)src[i];
        }
    }

    private void UnpackUNorm16(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            ushort value = BitConverter.ToUInt16(src, i * 2);
            if (_version.Item1 >= 2020 && _isBigEndian)
                value = (ushort)((value << 8) | (value >> 8));
            dst[i] = value / 65535f;
        }
    }

    private void UnpackSNorm16(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            short value = BitConverter.ToInt16(src, i * 2);
            if (_version.Item1 >= 2020 && _isBigEndian)
                value = (short)((value << 8) | (value >> 8));
            dst[i] = value < 0 ? value / 32768f : value / 32767f;
        }
    }

    private void UnpackUInt16(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            ushort value = BitConverter.ToUInt16(src, i * 2);
            if (_version.Item1 >= 2020 && _isBigEndian)
                value = (ushort)((value << 8) | (value >> 8));
            dst[i] = value;
        }
    }

    private void UnpackSInt16(byte[] src, float[] dst, int dimension)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            short value = BitConverter.ToInt16(src, i * 2);
            if (_version.Item1 >= 2020 && _isBigEndian)
                value = (short)((value << 8) | (value >> 8));
            dst[i] = value;
        }
    }

    private static void UnpackUInt32(byte[] src, float[] dst, int dimension)
    {
        var intCount = src.Length / 4;
        for (var i = 0; i < intCount; i++)
        {
            dst[i] = BitConverter.ToUInt32(src, i * 4);
        }
    }

    private void UnpackSInt32(byte[] src, float[] dst, int dimension)
    {
        var intCount = src.Length / 4;
        for (var i = 0; i < intCount; i++)
        {
            int value = BitConverter.ToInt32(src, i * 4);
            if (_isBigEndian)
                value = (int)(((uint)value << 24) | (((uint)value << 8) & 0x00ff0000) | (((uint)value >> 8) & 0x0000ff00) | ((uint)value >> 24));
            dst[i] = value;
        }
    }

    /// <summary>
    /// Assigns unpacked channel data to the appropriate vertex attribute array.
    /// Verbatim port of MeshHandler.assign_channel_vertex_data() from UnityPy.
    /// Supports all 7 vertex channels across Unity versions (<2018, 2017-2018, >=2019).
    /// </summary>
    private void AssignChannelData(int channel, float[] data, int dimension)
    {
        if (_version.Item1 >= 2018)
        {
            // Unity 2018+: channels are 0=Vertex, 1=Normal, 2=Color, 3=TexCoord0, 4=TexCoord1, 5=Tangent, 6=TexCoord2
            switch (channel)
            {
                case 0:  // Vertex (positions)
                    _positions = data;
                    break;
                case 1:  // Normal
                    _normals = data;
                    break;
                case 2:  // Color (RGBA, 4D)
                    _colors = data;
                    break;
                case 3:  // TexCoord0 or TexCoord1 (depends on version)
                    // In 2019+, this is typically unused; TexCoord0 is at channel 4
                    if (_version.Item1 < 2019)
                    {
                        _uvs = data;  // TexCoord0
                    }
                    break;
                case 4:  // TexCoord0 or TexCoord1 (2019+) 
                    // In 2019+, this is TexCoord0
                    _uvs = data;
                    break;
                case 5:  // TexCoord1 (or Tangent in some versions)
                    // Typically TexCoord1 for UV data, but can also be Tangent (4D: XYZW)
                    if (dimension == 4)
                    {
                        _tangents = data;  // Tangent (XYZW)
                    }
                    else if (dimension == 2)
                    {
                        _uv2 = data;  // TexCoord1 (2D)
                    }
                    break;
                case 6:  // Tangent (4D: XYZW)
                    _tangents = data;
                    break;
                case 7:  // TexCoord2 (rarely used)
                    _uv2 = data;
                    break;
            }
        }
        else
        {
            // Unity <2018: channels are 0=Vertex, 1=Normal, 2=Color, 3=TexCoord0, 4=TexCoord1, 5=Tangent, 6=TexCoord2
            switch (channel)
            {
                case 0:  // Vertex (positions)
                    _positions = data;
                    break;
                case 1:  // Normal
                    _normals = data;
                    break;
                case 2:  // Color (RGBA, 4D)
                    _colors = data;
                    break;
                case 3:  // TexCoord0
                    _uvs = data;
                    break;
                case 4:  // TexCoord1
                    _uv2 = data;
                    break;
                case 5:  // Tangent (XYZW)
                    _tangents = data;
                    break;
                case 6:  // TexCoord2
                    _uv3 = data;
                    break;
            }
        }
    }

    /// <summary>
    /// Decompresses compressed mesh data using PackedBitVector.
    /// Verbatim port of MeshHandler.decompress_compressed_mesh() from UnityPy.
    /// </summary>
    private void DecompressCompressedMesh(CompressedMesh compressedMesh)
    {
        // Vertex positions
        _vertexCount = (int)(compressedMesh.Vertices.NumItems / 3);

        if (compressedMesh.Vertices.NumItems > 0)
        {
            var verts = compressedMesh.Vertices.UnpackFloats();
            _positions = new float[_vertexCount * 3];
            for (var i = 0; i < _vertexCount; i++)
            {
                _positions[i * 3 + 0] = verts[i * 3 + 0];
                _positions[i * 3 + 1] = verts[i * 3 + 1];
                _positions[i * 3 + 2] = verts[i * 3 + 2];
            }
        }

        // UV coordinates
        if (compressedMesh.UV.NumItems > 0)
        {
            var uvs = compressedMesh.UV.UnpackFloats();
            _uvs = new float[_vertexCount * 2];
            for (var i = 0; i < _vertexCount; i++)
            {
                _uvs[i * 2 + 0] = uvs[i * 2 + 0];
                _uvs[i * 2 + 1] = uvs[i * 2 + 1];
            }
        }

        // Normals (XY stored, Z reconstructed)
        if (compressedMesh.Normals.NumItems > 0)
        {
            var normalData = compressedMesh.Normals.UnpackFloats();
            var signs = compressedMesh.NormalSigns.UnpackInts();

            _normals = new float[_vertexCount * 3];
            for (var i = 0; i < _vertexCount; i++)
            {
                var x = normalData[i * 2 + 0];
                var y = normalData[i * 2 + 1];
                var zsqr = 1.0f - x * x - y * y;
                float z;

                if (zsqr >= 0)
                {
                    z = MathF.Sqrt(zsqr);
                }
                else
                {
                    // Normalize if invalid
                    z = 0;
                    var length = MathF.Sqrt(x * x + y * y + z * z);
                    if (length > 0.00001f)
                    {
                        var invNorm = 1.0f / length;
                        x *= invNorm;
                        y *= invNorm;
                        z *= invNorm;
                    }
                }

                if (signs[i] == 0)
                {
                    z = -z;
                }

                _normals[i * 3 + 0] = x;
                _normals[i * 3 + 1] = y;
                _normals[i * 3 + 2] = z;
            }
        }

        // Triangle indices
        if (compressedMesh.Triangles.NumItems > 0)
        {
            _indices = compressedMesh.Triangles.UnpackInts();
        }
    }

    /// <summary>
    /// Extracts triangles from the mesh, handling submeshes and topology.
    /// Verbatim port of MeshHandler.get_triangles() from UnityPy.
    /// </summary>
    /// <returns>List of submeshes, where each submesh is a list of triangle tuples.</returns>
    public List<List<(uint, uint, uint)>> GetTriangles()
    {
        if (_indices == null)
        {
            throw new InvalidOperationException("Index buffer not available. Call Process() first.");
        }

        if (_mesh.SubMeshes == null || _mesh.SubMeshes.Length == 0)
        {
            throw new InvalidOperationException("No submeshes defined");
        }

        var submeshes = new List<List<(uint, uint, uint)>>();

        foreach (var subMesh in _mesh.SubMeshes)
        {
            var firstIndex = (int)(subMesh.FirstByte / 2);
            if (!_use16BitIndices)
            {
                firstIndex /= 2;
            }

            var indexCount = (int)subMesh.IndexCount;
            var topology = subMesh.Topology;

            List<(uint, uint, uint)> triangles;

            if (topology == MeshTopology.Triangles)
            {
                // Standard triangle list
                triangles = new List<(uint, uint, uint)>();
                for (var i = firstIndex; i < firstIndex + indexCount; i += 3)
                {
                    triangles.Add((_indices[i], _indices[i + 1], _indices[i + 2]));
                }
            }
            else if (_version.Item1 < 4 || topology == MeshTopology.TriangleStrip)
            {
                // Triangle strip with degenerate removal
                triangles = new List<(uint, uint, uint)>();
                for (var i = firstIndex; i < firstIndex + indexCount - 2; i++)
                {
                    var a = _indices[i];
                    var b = _indices[i + 1];
                    var c = _indices[i + 2];

                    // Skip degenerates
                    if (a == b || a == c || b == c)
                    {
                        continue;
                    }

                    // Winding flip-flop for strips
                    if ((i - firstIndex) % 2 == 1)
                    {
                        triangles.Add((b, a, c));
                    }
                    else
                    {
                        triangles.Add((a, b, c));
                    }
                }
            }
            else if (topology == MeshTopology.Quads)
            {
                // Quads: each quad becomes two triangles
                triangles = new List<(uint, uint, uint)>();
                for (var i = firstIndex; i < firstIndex + indexCount; i += 4)
                {
                    var a = _indices[i];
                    var b = _indices[i + 1];
                    var c = _indices[i + 2];
                    var d = _indices[i + 3];

                    triangles.Add((a, b, c));
                    triangles.Add((a, c, d));
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Submesh topology {topology} (lines or points) is not supported for triangle extraction.");
            }

            submeshes.Add(triangles);
        }

        return submeshes;
    }

    /// <summary>
    /// Compares two version tuples.
    /// Returns: &lt; 0 if v1 &lt; v2, 0 if equal, &gt; 0 if v1 &gt; v2.
    /// </summary>
    private static int CompareVersion((int, int, int, int) v1, (int, int, int, int) v2)
    {
        if (v1.Item1 != v2.Item1)
            return v1.Item1.CompareTo(v2.Item1);
        if (v1.Item2 != v2.Item2)
            return v1.Item2.CompareTo(v2.Item2);
        if (v1.Item3 != v2.Item3)
            return v1.Item3.CompareTo(v2.Item3);
        return v1.Item4.CompareTo(v2.Item4);
    }
}
