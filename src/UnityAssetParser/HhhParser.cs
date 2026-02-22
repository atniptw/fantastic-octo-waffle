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

    public byte[] ConvertToGlbFromContext(BaseAssetsContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return GlbBuilder.BuildFromContext(context);
    }

    /// <summary>
    /// Parse a .hhh decoration file into an existing context (e.g., skeleton from a .unitypackage).
    /// This allows decorations to be merged with a skeleton for proper bone-relative positioning.
    /// </summary>
    /// <param name="hhhBytes">The .hhh decoration file bytes</param>
    /// <param name="existingContext">Existing context (skeleton) to merge into</param>
    /// <param name="targetBoneName">Optional: The skeleton bone to parent the decoration to (e.g., "head", "neck")</param>
    /// <returns>True if merge successful, false if target bone not found or merge failed</returns>
    public bool TryMergeDecorationIntoContext(byte[] hhhBytes, BaseAssetsContext existingContext, string? targetBoneName = null)
    {
        if (hhhBytes is null || existingContext is null)
        {
            return false;
        }

        // Parse the decoration into a temporary context first
        var decorationContext = new BaseAssetsContext();
        try
        {
            SkeletonParser.Parse(hhhBytes, "hhh_decoration", decorationContext);
        }
        catch
        {
            return false;
        }

        // If no decorations were parsed, fail
        if (decorationContext.SemanticGameObjects.Count == 0)
        {
            return false;
        }

        // Find the root decoration GameObject(s) - typically the ones without parents
        var decorationRoots = decorationContext.SemanticTransforms
            .Where(t => !t.ParentPathId.HasValue)
            .Where(t => decorationContext.SemanticGameObjects.Any(go => go.PathId == t.GameObjectPathId))
            .ToList();

        if (decorationRoots.Count == 0)
        {
            return false;
        }

        // Find target bone in skeleton if specified
        long? targetBonePathId = null;
        if (!string.IsNullOrEmpty(targetBoneName))
        {
            // Map bone tags to actual anchor point GameObject names used in REPO
            // Based on MoreHead mod's parentPathMap (last node in each path)
            var boneAnchorNames = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "head", new[] { "code_head_top", "ANIM HEAD TOP" } },
                { "neck", new[] { "code_head_bot_side", "ANIM HEAD BOT" } },
                { "body", new[] { "ANIM BODY TOP SCALE", "ANIM BODY TOP" } },
                { "hip", new[] { "ANIM BODY BOT" } },
                { "leftarm", new[] { "code_arm_l", "ANIM ARM L" } },
                { "rightarm", new[] { "ANIM ARM R SCALE", "code_arm_r" } },
                { "leftleg", new[] { "ANIM LEG L TOP" } },
                { "rightleg", new[] { "ANIM LEG R TOP" } }
            };

            if (!boneAnchorNames.TryGetValue(targetBoneName, out var possibleNames))
            {
                return false; // Unknown bone tag
            }

            // Try to find one of the possible anchor points
            SemanticGameObjectInfo? targetBone = null;
            foreach (var anchorName in possibleNames)
            {
                targetBone = existingContext.SemanticGameObjects
                    .FirstOrDefault(go => go.Name.Equals(anchorName, System.StringComparison.OrdinalIgnoreCase));
                
                if (targetBone != null)
                {
                    System.Console.WriteLine($"[HhhParser] Found anchor point '{anchorName}' for bone tag '{targetBoneName}'");
                    break;
                }
            }
            
            if (targetBone is null)
            {
                System.Console.WriteLine($"[HhhParser] Failed to find anchor point for bone tag '{targetBoneName}'. Tried: {string.Join(", ", possibleNames)}");
                return false; // Target bone not found
            }

            var boneTransform = existingContext.SemanticTransforms
                .FirstOrDefault(t => t.GameObjectPathId == targetBone.PathId);
            
            if (boneTransform is null)
            {
                return false;
            }

            targetBonePathId = boneTransform.PathId;
        }

        // Merge the decoration context into the existing context
        foreach (var decorationGameObj in decorationContext.SemanticGameObjects)
        {
            existingContext.SemanticGameObjects.Add(decorationGameObj);
        }

        foreach (var decorationTransform in decorationContext.SemanticTransforms)
        {
            var decorationRoot = decorationRoots.FirstOrDefault(r => r.PathId == decorationTransform.PathId);
            
            // If this is a root decoration transform and we have a target bone, re-parent it
            if (decorationRoot != null && targetBonePathId.HasValue)
            {
                var newTransform = new SemanticTransformInfo(
                    decorationTransform.PathId,
                    decorationTransform.GameObjectPathId,
                    targetBonePathId, // Parent to target bone instead of null
                    decorationTransform.ChildrenPathIds,
                    decorationTransform.LocalPosition,
                    decorationTransform.LocalRotation,
                    decorationTransform.LocalScale);
                
                // Update target bone's children list to include this decoration
                var targetBoneTransform = existingContext.SemanticTransforms.FirstOrDefault(t => t.PathId == targetBonePathId);
                if (targetBoneTransform != null)
                {
                    var updatedChildren = new List<long>(targetBoneTransform.ChildrenPathIds) { decorationTransform.PathId };
                    var updatedBoneTransform = new SemanticTransformInfo(
                        targetBoneTransform.PathId,
                        targetBoneTransform.GameObjectPathId,
                        targetBoneTransform.ParentPathId,
                        updatedChildren,
                        targetBoneTransform.LocalPosition,
                        targetBoneTransform.LocalRotation,
                        targetBoneTransform.LocalScale);
                    
                    existingContext.SemanticTransforms.Remove(targetBoneTransform);
                    existingContext.SemanticTransforms.Add(updatedBoneTransform);
                }
                
                existingContext.SemanticTransforms.Add(newTransform);
            }
            else
            {
                existingContext.SemanticTransforms.Add(decorationTransform);
            }
        }

        // Merge other semantic data (meshes, materials, textures, etc.)
        // These must be merged for colors and textures to render properly
        foreach (var semanticObject in decorationContext.SemanticObjects)
        {
            existingContext.SemanticObjects.Add(semanticObject);
        }

        foreach (var mesh in decorationContext.SemanticMeshes)
        {
            existingContext.SemanticMeshes.Add(mesh);
        }

        foreach (var meshFilter in decorationContext.SemanticMeshFilters)
        {
            existingContext.SemanticMeshFilters.Add(meshFilter);
        }

        foreach (var meshRenderer in decorationContext.SemanticMeshRenderers)
        {
            existingContext.SemanticMeshRenderers.Add(meshRenderer);
        }

        foreach (var material in decorationContext.SemanticMaterials)
        {
            existingContext.SemanticMaterials.Add(material);
        }

        foreach (var texture in decorationContext.SemanticTextures)
        {
            existingContext.SemanticTextures.Add(texture);
        }

        foreach (var anchorPoint in decorationContext.SemanticAnchorPoints)
        {
            existingContext.SemanticAnchorPoints.Add(anchorPoint);
        }

        Console.WriteLine($"[HhhParser] Merged decoration - Objects: {decorationContext.SemanticObjects.Count}, Textures: {decorationContext.SemanticTextures.Count}, Materials: {decorationContext.SemanticMaterials.Count}");

        return true;
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
        private readonly Dictionary<long, List<long>> _gameObjectPathIdToMaterialPathIds;
        private Dictionary<long, int>? _materialPathIdToIndex;
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
            
            // Map GameObject -> Material PathIds from MeshRenderers
            _gameObjectPathIdToMaterialPathIds = context.SemanticMeshRenderers
                .GroupBy(renderer => renderer.GameObjectPathId)
                .ToDictionary(group => group.Key, group => group.First().MaterialPathIds.ToList());
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
            
            // Log hierarchy structure for diagnostics
            LogNodeHierarchy(rootNodes);

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
                Json["materials"] = BuildMaterials();
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
            // Build a map of mesh pathId -> material pathIds by finding GameObjects that use each mesh
            var meshPathIdToMaterials = new Dictionary<long, List<long>>();
            foreach (var meshFilter in _context.SemanticMeshFilters)
            {
                var meshPathId = meshFilter.MeshPathId;
                var gameObjectPathId = meshFilter.GameObjectPathId;
                
                if (!meshPathIdToMaterials.ContainsKey(meshPathId))
                {
                    List<long> materials = new();
                    if (_gameObjectPathIdToMaterialPathIds.TryGetValue(gameObjectPathId, out var materialList))
                    {
                        materials = materialList;
                    }
                    meshPathIdToMaterials[meshPathId] = materials;
                }
            }

            foreach (var mesh in _context.SemanticMeshes)
            {
                var materialPathIds = meshPathIdToMaterials.ContainsKey(mesh.PathId) 
                    ? meshPathIdToMaterials[mesh.PathId] 
                    : new List<long>();
                
                var meshIndex = TryBuildMesh(mesh, materialPathIds);
                if (meshIndex >= 0)
                {
                    _meshIndicesByPathId[mesh.PathId] = meshIndex;
                }
            }
        }

        private int TryBuildMesh(SemanticMeshInfo mesh, List<long> materialPathIds)
        {
            var vertexCount = mesh.VertexCount > 0 ? mesh.VertexCount : mesh.DecodedPositions.Count;
            if (mesh.VertexDataByteLength == 0 && mesh.DecodedPositions.Count == 0)
            {
                AddWarning($"GLB export skipped mesh '{mesh.Name}' ({mesh.PathId}): no vertex buffer payload in source mesh.");
                return -1;
            }

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
            var subMeshIndex = 0;
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

                // Get the material index for this submesh
                var materialIndex = GetMaterialIndexForSubmesh(subMeshIndex, materialPathIds);

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
                    ["material"] = materialIndex
                });
                
                subMeshIndex++;
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

            var childNodesByParent = _context.SemanticTransforms
                .Where(transform => transform.ParentPathId.HasValue && _transformNodeIndices.ContainsKey(transform.ParentPathId.Value))
                .GroupBy(transform => transform.ParentPathId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(child => _transformNodeIndices[child.PathId]).ToList());

            foreach (var transform in _context.SemanticTransforms)
            {
                if (!_transformNodeIndices.TryGetValue(transform.PathId, out var nodeIndex))
                {
                    continue;
                }

                var childNodeIndices = new HashSet<int>();
                foreach (var child in transform.ChildrenPathIds)
                {
                    if (_transformNodeIndices.TryGetValue(child, out var childIndex))
                    {
                        childNodeIndices.Add(childIndex);
                    }
                }

                if (childNodesByParent.TryGetValue(transform.PathId, out var parentLinkedChildren))
                {
                    foreach (var childIndex in parentLinkedChildren)
                    {
                        childNodeIndices.Add(childIndex);
                    }
                }

                if (childNodeIndices.Count > 0)
                {
                    _nodes[nodeIndex]["children"] = childNodeIndices.ToList();
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

        private List<Dictionary<string, object>> BuildMaterials()
        {
            var materials = new List<Dictionary<string, object>>();
            var materialPathIdToIndex = new Dictionary<long, int>();

            // Export all materials from the context, regardless of MeshRenderer references
            // This ensures .hhh files with embedded materials are included
            foreach (var material in _context.SemanticMaterials)
            {
                var materialObject = new Dictionary<string, object>
                {
                    ["name"] = material.Name,
                    ["pbrMetallicRoughness"] = new Dictionary<string, object>
                    {
                        ["baseColorFactor"] = material.BaseColorFactor,
                        ["metallicFactor"] = material.Metallic,
                        ["roughnessFactor"] = material.Roughness
                    },
                    ["doubleSided"] = true
                };

                if (!string.IsNullOrEmpty(material.AlphaMode) && material.AlphaMode != "OPAQUE")
                {
                    materialObject["alphaMode"] = material.AlphaMode;
                }

                materialPathIdToIndex[material.PathId] = materials.Count;
                materials.Add(materialObject);
            }

            // If no materials were found, add a default white material
            if (materials.Count == 0)
            {
                materials.Add(new Dictionary<string, object>
                {
                    ["name"] = "DefaultMaterial",
                    ["pbrMetallicRoughness"] = new Dictionary<string, object>
                    {
                        ["baseColorFactor"] = new[] { 1f, 1f, 1f, 1f },
                        ["metallicFactor"] = 0f,
                        ["roughnessFactor"] = 0.5f
                    },
                    ["doubleSided"] = true
                });
            }

            // Store the mapping for use in GetMaterialIndexForSubmesh
            _materialPathIdToIndex = materialPathIdToIndex;
            return materials;
        }

        private int GetMaterialIndexForSubmesh(int subMeshIndex, List<long> materialPathIds)
        {
            // If we have MeshRenderer material references, use them
            if (materialPathIds.Count > 0)
            {
                if (subMeshIndex < materialPathIds.Count)
                {
                    var materialPathId = materialPathIds[subMeshIndex];
                    if (_materialPathIdToIndex != null && _materialPathIdToIndex.TryGetValue(materialPathId, out var index))
                    {
                        return index;
                    }
                }
                return 0; // Fallback for mismatched indices
            }

            // If no MeshRenderer materials, assign materials sequentially by submesh
            // This handles standalone .hhh files with embedded materials
            if (_materialPathIdToIndex != null && _materialPathIdToIndex.Count > 0)
            {
                var materialIndex = subMeshIndex % _materialPathIdToIndex.Count;
                return materialIndex;
            }

            // Final fallback to first material
            return 0;
        }

        private void LogNodeHierarchy(List<int> rootNodeIndices)
        {
            try
            {
                System.Console.WriteLine("\n=== GLB Node Hierarchy ===");
                System.Console.WriteLine($"Root nodes: {string.Join(", ", rootNodeIndices)} (count: {rootNodeIndices.Count})");
                System.Console.WriteLine($"Total nodes exported: {_nodes.Count}");
                System.Console.WriteLine($"Total transforms in context: {_context.SemanticTransforms.Count}");
                System.Console.WriteLine($"Total GameObjects: {_gameObjectNameByPathId.Count}");
                System.Console.WriteLine($"Total transforms with meshes: {_meshPathByGameObjectPathId.Count}");
                System.Console.WriteLine();
                
                // Log skeleton nodes (likely named with common skeleton patterns)
                System.Console.WriteLine("Skeleton-like nodes (likely bones):");
                var skeletonNames = new[] { "armature", "skeleton", "rig", "root", "hips", "spine", "chest", "neck", "head", 
                                            "leftarm", "rightarm", "leftleg", "rightleg", "eyebone", "jawbone" };
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var node = _nodes[i];
                    if (node.TryGetValue("name", out var nameObj) && nameObj is string nodeName)
                    {
                        if (skeletonNames.Any(skel => nodeName.Contains(skel, StringComparison.OrdinalIgnoreCase)))
                        {
                            var hasChildren = node.ContainsKey("children");
                            System.Console.WriteLine($"  [{i}] {nodeName} {(hasChildren ? "[has children]" : "[no children]")} {(node.ContainsKey("mesh") ? "[MESH]" : "")}");
                        }
                    }
                }
                System.Console.WriteLine();
                
                System.Console.WriteLine("Node tree (hierarchy):");
                foreach (var rootIndex in rootNodeIndices)
                {
                    LogNodeRecursive(rootIndex, 0);
                }
                
                System.Console.WriteLine("\n=== End Hierarchy ===\n");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error logging hierarchy: {ex.Message}" );
            }
        }

        private void LogNodeRecursive(int nodeIndex, int depth)
        {
            if (nodeIndex >= _nodes.Count)
                return;
                
            var node = _nodes[nodeIndex];
            var indent = new string(' ', depth * 2);
            var nodeName = node.TryGetValue("name", out var nameObj) ? (string?)nameObj ?? $"[{nodeIndex}]unknown" : $"[{nodeIndex}]unnamed";
            var hasMesh = node.ContainsKey("mesh");
            var hasTransform = node.ContainsKey("translation") || node.ContainsKey("rotation") || node.ContainsKey("scale");
            var childCount = node.TryGetValue("children", out var childrenObj) && childrenObj is List<object> children ? children.Count : 0;
            
            System.Console.WriteLine($"{indent}[{nodeIndex}] {nodeName}{(hasMesh ? " [MESH]" : "")}{(hasTransform ? " [TXF]" : "")}{(childCount > 0 ? $" [{childCount} children]" : "")}");
            
            if (hasTransform)
            {
                var parts = new List<string>();
                if (node.TryGetValue("translation", out var translationObj) && translationObj is IEnumerable<object> translation)
                {
                    var vals = translation.Cast<double>().ToArray();
                    if (vals.Any(v => Math.Abs(v) > 0.001))
                        parts.Add($"pos:({vals[0]:F2},{vals[1]:F2},{vals[2]:F2})");
                }
                if (node.TryGetValue("scale", out var scaleObj) && scaleObj is IEnumerable<object> scale)
                {
                    var vals = scale.Cast<double>().ToArray();
                    if (Math.Abs(vals[0] - 1) > 0.001 || Math.Abs(vals[1] - 1) > 0.001 || Math.Abs(vals[2] - 1) > 0.001)
                        parts.Add($"scale:({vals[0]:F2},{vals[1]:F2},{vals[2]:F2})");
                }
                if (parts.Count > 0)
                    System.Console.WriteLine($"{indent}   {string.Join("; ", parts)}");
            }
            
            if (node.TryGetValue("children", out var childrenObj2) && childrenObj2 is List<object> childrenList)
            {
                foreach (var child in childrenList)
                {
                    if (child is int childIndex)
                    {
                        LogNodeRecursive(childIndex, depth + 1);
                    }
                }
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
