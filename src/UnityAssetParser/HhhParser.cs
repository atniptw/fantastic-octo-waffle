using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace UnityAssetParser;

public sealed class HhhParser
{
    public byte[] ConvertToGlb(byte[] hhhBytes, BaseAssetsContext? baseAssets = null)
    {
        if (hhhBytes is null)
        {
            throw new ArgumentNullException(nameof(hhhBytes));
        }

        var context = baseAssets ?? new BaseAssetsContext();
        SkeletonParser.Parse(hhhBytes, "hhh", context);

        return GlbBuilder.BuildFromContext(context);
    }

    private static class GlbBuilder
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static byte[] BuildFromContext(BaseAssetsContext context)
        {
            var export = GlbExportModel.FromContext(context);
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(export.Json, JsonOptions);
            return BuildGlb(jsonBytes, export.BinaryChunk);
        }

        private static byte[] BuildGlb(byte[] jsonBytes, byte[]? binaryBytes)
        {
            var jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
            var jsonLength = jsonBytes.Length + jsonPadding;

            var hasBinaryChunk = binaryBytes is { Length: > 0 };
            var binaryLength = 0;
            var binaryPadding = 0;
            if (hasBinaryChunk)
            {
                binaryLength = binaryBytes!.Length;
                binaryPadding = (4 - (binaryLength % 4)) % 4;
            }

            var totalLength = 12 + 8 + jsonLength + (hasBinaryChunk ? 8 + binaryLength + binaryPadding : 0);

            var buffer = new byte[totalLength];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), 0x46546C67);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), (uint)totalLength);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12, 4), (uint)jsonLength);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 0x4E4F534A);

            jsonBytes.CopyTo(buffer, 20);
            for (var i = 0; i < jsonPadding; i++)
            {
                buffer[20 + jsonBytes.Length + i] = 0x20;
            }

            if (hasBinaryChunk)
            {
                var binaryHeaderOffset = 20 + jsonLength;
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(binaryHeaderOffset, 4), (uint)(binaryLength + binaryPadding));
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(binaryHeaderOffset + 4, 4), 0x004E4942);
                binaryBytes!.CopyTo(buffer, binaryHeaderOffset + 8);
            }

            return buffer;
        }
    }

    private sealed class GlbExportModel
    {
        private const int BufferViewTargetArrayBuffer = 34962;
        private const int BufferViewTargetElementArrayBuffer = 34963;
        private const int AccessorComponentTypeFloat = 5126;
        private const int AccessorComponentTypeUnsignedInt = 5125;
        private const int PrimitiveModeTriangles = 4;

        private readonly BaseAssetsContext _context;
        private readonly MemoryStream _binaryStream = new();
        private readonly List<Dictionary<string, object>> _nodes = new();
        private readonly List<Dictionary<string, object>> _meshes = new();
        private readonly List<Dictionary<string, object>> _bufferViews = new();
        private readonly List<Dictionary<string, object>> _accessors = new();
        private readonly Dictionary<long, int> _transformNodeIndices = new();
        private readonly Dictionary<long, int> _meshIndicesByPathId = new();
        private readonly Dictionary<long, long> _meshPathByGameObjectPathId;
        private readonly Dictionary<long, string> _gameObjectNameByPathId;
        private readonly List<string> _conversionWarnings = new();

        private GlbExportModel(BaseAssetsContext context)
        {
            _context = context;
            _meshPathByGameObjectPathId = context.SemanticMeshFilters
                .GroupBy(link => link.GameObjectPathId)
                .ToDictionary(group => group.Key, group => group.First().MeshPathId);
            _gameObjectNameByPathId = context.SemanticGameObjects
                .GroupBy(item => item.PathId)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        public Dictionary<string, object> Json { get; private set; } = new();

        public byte[]? BinaryChunk => _binaryStream.Length > 0 ? _binaryStream.ToArray() : null;

        public static GlbExportModel FromContext(BaseAssetsContext context)
        {
            var model = new GlbExportModel(context);
            model.Build();
            return model;
        }

        private void Build()
        {
            BuildMeshes();
            var rootNodes = BuildNodes();

            Json = new Dictionary<string, object>
            {
                ["asset"] = new Dictionary<string, object>
                {
                    ["version"] = "2.0",
                    ["generator"] = "UnityAssetParser"
                }
            };

            if (_conversionWarnings.Count > 0)
            {
                Json["extras"] = new Dictionary<string, object>
                {
                    ["conversionWarnings"] = _conversionWarnings
                };
            }

            if (_nodes.Count > 0)
            {
                Json["scene"] = 0;
                Json["scenes"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["nodes"] = rootNodes
                    }
                };
                Json["nodes"] = _nodes;
            }

            if (_meshes.Count > 0)
            {
                Json["meshes"] = _meshes;
                Json["materials"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["name"] = "DefaultMaterial",
                        ["pbrMetallicRoughness"] = new Dictionary<string, object>
                        {
                            ["baseColorFactor"] = new[] { 1f, 1f, 1f, 1f },
                            ["metallicFactor"] = 0f,
                            ["roughnessFactor"] = 1f
                        },
                        ["doubleSided"] = true
                    }
                };
            }

            if (_binaryStream.Length > 0)
            {
                Json["buffers"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["byteLength"] = (int)_binaryStream.Length
                    }
                };
                Json["bufferViews"] = _bufferViews;
                Json["accessors"] = _accessors;
            }
        }

        private void BuildMeshes()
        {
            foreach (var mesh in _context.SemanticMeshes)
            {
                var meshIndex = TryBuildMesh(mesh);
                if (meshIndex >= 0)
                {
                    _meshIndicesByPathId[mesh.PathId] = meshIndex;
                }
            }
        }

        private int TryBuildMesh(SemanticMeshInfo mesh)
        {
            var vertexCount = mesh.VertexCount > 0 ? mesh.VertexCount : mesh.DecodedPositions.Count;
            if (vertexCount <= 0 || mesh.DecodedPositions.Count < vertexCount)
            {
                AddWarning($"GLB export skipped mesh '{mesh.Name}' ({mesh.PathId}): missing positions.");
                return -1;
            }

            if (mesh.DecodedIndices.Count == 0 || mesh.IndexElementSizeBytes <= 0)
            {
                AddWarning($"GLB export skipped mesh '{mesh.Name}' ({mesh.PathId}): missing indices.");
                return -1;
            }

            var positionsAccessor = AddVec3Accessor(mesh.DecodedPositions, vertexCount, true);
            var normalsAccessor = mesh.DecodedNormals.Count >= vertexCount
                ? AddVec3Accessor(mesh.DecodedNormals, vertexCount, false)
                : (int?)null;
            var uv0Accessor = mesh.DecodedUv0.Count >= vertexCount
                ? AddVec2Accessor(mesh.DecodedUv0, vertexCount)
                : (int?)null;

            var primitives = new List<Dictionary<string, object>>();
            foreach (var subMesh in mesh.SubMeshes)
            {
                if (!IsTrianglesTopology(subMesh.Topology))
                {
                    continue;
                }

                var firstIndex = subMesh.FirstByte / mesh.IndexElementSizeBytes;
                var indexCount = subMesh.IndexCount;
                if (firstIndex < 0 || indexCount <= 0 || firstIndex + indexCount > mesh.DecodedIndices.Count)
                {
                    AddWarning($"GLB export skipped submesh in '{mesh.Name}' ({mesh.PathId}): index range out of bounds.");
                    continue;
                }

                var indices = new uint[indexCount];
                var hasOutOfRangeIndex = false;
                for (var i = 0; i < indexCount; i++)
                {
                    var value = mesh.DecodedIndices[firstIndex + i];
                    if (value >= vertexCount)
                    {
                        hasOutOfRangeIndex = true;
                        break;
                    }

                    indices[i] = value;
                }

                if (hasOutOfRangeIndex)
                {
                    AddWarning($"GLB export skipped submesh in '{mesh.Name}' ({mesh.PathId}): index exceeds vertex count.");
                    continue;
                }

                var indicesAccessor = AddScalarUintAccessor(indices);
                var attributes = new Dictionary<string, object>
                {
                    ["POSITION"] = positionsAccessor
                };

                if (normalsAccessor.HasValue)
                {
                    attributes["NORMAL"] = normalsAccessor.Value;
                }

                if (uv0Accessor.HasValue)
                {
                    attributes["TEXCOORD_0"] = uv0Accessor.Value;
                }

                primitives.Add(new Dictionary<string, object>
                {
                    ["attributes"] = attributes,
                    ["indices"] = indicesAccessor,
                    ["mode"] = PrimitiveModeTriangles,
                    ["material"] = 0
                });
            }

            if (primitives.Count == 0)
            {
                AddWarning($"GLB export skipped mesh '{mesh.Name}' ({mesh.PathId}): no triangle primitives.");
                return -1;
            }

            var meshObject = new Dictionary<string, object>
            {
                ["name"] = string.IsNullOrWhiteSpace(mesh.Name) ? $"Mesh_{mesh.PathId}" : mesh.Name,
                ["primitives"] = primitives
            };

            var meshIndex = _meshes.Count;
            _meshes.Add(meshObject);
            return meshIndex;
        }

        private List<int> BuildNodes()
        {
            foreach (var transform in _context.SemanticTransforms)
            {
                var node = new Dictionary<string, object>();
                if (_gameObjectNameByPathId.TryGetValue(transform.GameObjectPathId, out var name)
                    && !string.IsNullOrWhiteSpace(name))
                {
                    node["name"] = name;
                }

                if (!IsDefaultTranslation(transform.LocalPosition))
                {
                    node["translation"] = new[]
                    {
                        transform.LocalPosition.X,
                        transform.LocalPosition.Y,
                        transform.LocalPosition.Z
                    };
                }

                if (!IsDefaultRotation(transform.LocalRotation))
                {
                    node["rotation"] = new[]
                    {
                        transform.LocalRotation.X,
                        transform.LocalRotation.Y,
                        transform.LocalRotation.Z,
                        transform.LocalRotation.W
                    };
                }

                if (!IsDefaultScale(transform.LocalScale))
                {
                    node["scale"] = new[]
                    {
                        transform.LocalScale.X,
                        transform.LocalScale.Y,
                        transform.LocalScale.Z
                    };
                }

                if (_meshPathByGameObjectPathId.TryGetValue(transform.GameObjectPathId, out var meshPathId)
                    && _meshIndicesByPathId.TryGetValue(meshPathId, out var meshIndex))
                {
                    node["mesh"] = meshIndex;
                }

                var nodeIndex = _nodes.Count;
                _nodes.Add(node);
                _transformNodeIndices[transform.PathId] = nodeIndex;
            }

            foreach (var transform in _context.SemanticTransforms)
            {
                if (!_transformNodeIndices.TryGetValue(transform.PathId, out var nodeIndex))
                {
                    continue;
                }

                var childNodeIndices = transform.ChildrenPathIds
                    .Where(child => _transformNodeIndices.ContainsKey(child))
                    .Select(child => _transformNodeIndices[child])
                    .ToList();

                if (childNodeIndices.Count > 0)
                {
                    _nodes[nodeIndex]["children"] = childNodeIndices;
                }
            }

            return _context.SemanticTransforms
                .Where(transform => transform.ParentPathId is null || !_transformNodeIndices.ContainsKey(transform.ParentPathId.Value))
                .Select(transform => _transformNodeIndices[transform.PathId])
                .ToList();
        }

        private static bool IsDefaultTranslation(SemanticVector3 value)
        {
            return value.X == 0f && value.Y == 0f && value.Z == 0f;
        }

        private static bool IsDefaultScale(SemanticVector3 value)
        {
            return value.X == 1f && value.Y == 1f && value.Z == 1f;
        }

        private static bool IsDefaultRotation(SemanticQuaternion value)
        {
            return value.X == 0f && value.Y == 0f && value.Z == 0f && value.W == 1f;
        }

        private static bool IsTrianglesTopology(int topology)
        {
            return topology == 0;
        }

        private void AddWarning(string warning)
        {
            _conversionWarnings.Add(warning);
        }

        private int AddVec3Accessor(IReadOnlyList<SemanticVector3> values, int count, bool includeMinMax)
        {
            var data = new byte[count * 3 * sizeof(float)];
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var minZ = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            var maxZ = float.MinValue;
            var writeOffset = 0;

            for (var i = 0; i < count; i++)
            {
                var value = values[i];
                WriteFloat32(data, ref writeOffset, value.X);
                WriteFloat32(data, ref writeOffset, value.Y);
                WriteFloat32(data, ref writeOffset, value.Z);

                if (includeMinMax)
                {
                    minX = Math.Min(minX, value.X);
                    minY = Math.Min(minY, value.Y);
                    minZ = Math.Min(minZ, value.Z);
                    maxX = Math.Max(maxX, value.X);
                    maxY = Math.Max(maxY, value.Y);
                    maxZ = Math.Max(maxZ, value.Z);
                }
            }

            var bufferView = AddBufferView(data, BufferViewTargetArrayBuffer);
            return AddAccessor(
                bufferView,
                AccessorComponentTypeFloat,
                count,
                "VEC3",
                includeMinMax ? new[] { minX, minY, minZ } : null,
                includeMinMax ? new[] { maxX, maxY, maxZ } : null);
        }

        private int AddVec2Accessor(IReadOnlyList<SemanticVector2> values, int count)
        {
            var data = new byte[count * 2 * sizeof(float)];
            var writeOffset = 0;
            for (var i = 0; i < count; i++)
            {
                var value = values[i];
                WriteFloat32(data, ref writeOffset, value.X);
                WriteFloat32(data, ref writeOffset, value.Y);
            }

            var bufferView = AddBufferView(data, BufferViewTargetArrayBuffer);
            return AddAccessor(bufferView, AccessorComponentTypeFloat, count, "VEC2", null, null);
        }

        private int AddScalarUintAccessor(IReadOnlyList<uint> values)
        {
            var count = values.Count;
            var data = new byte[count * sizeof(uint)];
            var writeOffset = 0;
            uint min = uint.MaxValue;
            uint max = uint.MinValue;
            for (var i = 0; i < count; i++)
            {
                var value = values[i];
                WriteUInt32(data, ref writeOffset, value);
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            var bufferView = AddBufferView(data, BufferViewTargetElementArrayBuffer);
            return AddAccessor(
                bufferView,
                AccessorComponentTypeUnsignedInt,
                count,
                "SCALAR",
                new[] { (float)min },
                new[] { (float)max });
        }

        private int AddBufferView(byte[] data, int target)
        {
            AlignBinaryTo4();
            var byteOffset = checked((int)_binaryStream.Position);
            _binaryStream.Write(data, 0, data.Length);

            var bufferView = new Dictionary<string, object>
            {
                ["buffer"] = 0,
                ["byteOffset"] = byteOffset,
                ["byteLength"] = data.Length,
                ["target"] = target
            };

            var bufferViewIndex = _bufferViews.Count;
            _bufferViews.Add(bufferView);
            return bufferViewIndex;
        }

        private int AddAccessor(int bufferViewIndex, int componentType, int count, string type, float[]? min, float[]? max)
        {
            var accessor = new Dictionary<string, object>
            {
                ["bufferView"] = bufferViewIndex,
                ["componentType"] = componentType,
                ["count"] = count,
                ["type"] = type
            };

            if (min is not null)
            {
                accessor["min"] = min;
            }

            if (max is not null)
            {
                accessor["max"] = max;
            }

            var accessorIndex = _accessors.Count;
            _accessors.Add(accessor);
            return accessorIndex;
        }

        private void AlignBinaryTo4()
        {
            while ((_binaryStream.Position % 4) != 0)
            {
                _binaryStream.WriteByte(0);
            }
        }

        private static void WriteFloat32(byte[] buffer, ref int offset, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)), (uint)BitConverter.SingleToInt32Bits(value));
            offset += sizeof(float);
        }

        private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)), value);
            offset += sizeof(uint);
        }
    }
}
