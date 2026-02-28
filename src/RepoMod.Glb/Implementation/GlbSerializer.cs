using System.Buffers.Binary;
using System.Linq;
using System.Text.Json;
using RepoMod.Glb.Abstractions;
using RepoMod.Glb.Contracts;
using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Implementation;

public sealed class GlbSerializer : IGlbSerializer
{
    private const int GltfTrianglesMode = 4;
    private const uint GlbMagic = 0x46546C67;
    private const uint GlbVersion = 2;
    private const uint JsonChunkType = 0x4E4F534A;
    private const uint BinChunkType = 0x004E4942;
    public GlbBuildResult Build(GlbCompositionResult composition)
    {
        var diagnostics = new List<GlbBuildDiagnostic>();
        var binaryBuffer = new List<byte>();

        var materials = new List<object>();
        var materialIndexById = new Dictionary<string, int>(StringComparer.Ordinal);

        var images = new List<object>();
        var textures = new List<object>();
        var textureIndexById = new Dictionary<string, int>(StringComparer.Ordinal);

        var accessors = new List<object>();
        var bufferViews = new List<object>();
        var meshes = new List<object>();
        var nodes = new List<object>();

        var rootNodeChildren = new List<int>();

        BuildTextures(composition.RenderTextures, images, textures, textureIndexById);
        BuildMaterials(composition.RenderMaterials, materials, materialIndexById, textureIndexById);

        foreach (var anchor in composition.Anchors.OrderBy(item => item.AnchorPath, StringComparer.Ordinal))
        {
            var anchorNodeChildren = new List<int>();

            foreach (var composed in anchor.Primitives.OrderBy(item => item.Primitive.PrimitiveId, StringComparer.Ordinal))
            {
                var primitive = composed.Primitive;
                if (primitive.Topology != 0)
                {
                    diagnostics.Add(new GlbBuildDiagnostic(
                        "warning",
                        "UNSUPPORTED_TOPOLOGY",
                        $"Primitive '{primitive.PrimitiveId}' topology '{primitive.Topology}' is unsupported and was skipped.",
                        primitive.PrimitiveId));
                    continue;
                }

                if (primitive.Indices.Count == 0)
                {
                    diagnostics.Add(new GlbBuildDiagnostic(
                        "error",
                        "MISSING_INDICES",
                        $"Primitive '{primitive.PrimitiveId}' has no indices and was skipped.",
                        primitive.PrimitiveId));
                    continue;
                }

                if (primitive.Positions is not { Count: > 0 } || primitive.Positions.Count % 3 != 0)
                {
                    diagnostics.Add(new GlbBuildDiagnostic(
                        "error",
                        "INVALID_POSITIONS",
                        $"Primitive '{primitive.PrimitiveId}' has invalid position data and was skipped.",
                        primitive.PrimitiveId));
                    continue;
                }

                var vertexCount = primitive.Positions.Count / 3;
                if (primitive.Indices.Max() >= vertexCount)
                {
                    diagnostics.Add(new GlbBuildDiagnostic(
                        "error",
                        "INDEX_OUT_OF_RANGE",
                        $"Primitive '{primitive.PrimitiveId}' has index out of range and was skipped.",
                        primitive.PrimitiveId));
                    continue;
                }

                var attributes = new Dictionary<string, int>(StringComparer.Ordinal);

                var positionAccessor = AddFloatAccessor(
                    binaryBuffer,
                    bufferViews,
                    accessors,
                    primitive.Positions,
                    componentsPerVertex: 3,
                    accessorType: "VEC3",
                    includeMinMax: true);
                attributes["POSITION"] = positionAccessor;

                TryAddOptionalAttribute(primitive.PrimitiveId, "NORMAL", primitive.Normals, 3, "VEC3", diagnostics, binaryBuffer, bufferViews, accessors, attributes);
                TryAddOptionalAttribute(primitive.PrimitiveId, "TANGENT", primitive.Tangents, 4, "VEC4", diagnostics, binaryBuffer, bufferViews, accessors, attributes);
                TryAddOptionalAttribute(primitive.PrimitiveId, "COLOR_0", primitive.Colors, 4, "VEC4", diagnostics, binaryBuffer, bufferViews, accessors, attributes);
                TryAddOptionalAttribute(primitive.PrimitiveId, "TEXCOORD_0", primitive.Uv0, 2, "VEC2", diagnostics, binaryBuffer, bufferViews, accessors, attributes);
                TryAddOptionalAttribute(primitive.PrimitiveId, "TEXCOORD_1", primitive.Uv1, 2, "VEC2", diagnostics, binaryBuffer, bufferViews, accessors, attributes);

                var indicesAccessor = AddIndexAccessor(binaryBuffer, bufferViews, accessors, primitive.Indices);

                    var materialIndex = ResolveMaterialIndex(primitive.MaterialObjectId, materials, materialIndexById);
                var mesh = new
                {
                    primitives = new object[]
                    {
                        new
                        {
                            attributes,
                            indices = indicesAccessor,
                            material = materialIndex,
                            mode = GltfTrianglesMode
                        }
                    },
                    name = primitive.PrimitiveId
                };

                var meshIndex = meshes.Count;
                meshes.Add(mesh);

                var meshNodeIndex = nodes.Count;
                nodes.Add(new { mesh = meshIndex, name = primitive.PrimitiveId });
                anchorNodeChildren.Add(meshNodeIndex);
            }

            if (anchorNodeChildren.Count == 0)
            {
                continue;
            }

            var anchorNodeIndex = nodes.Count;
            nodes.Add(new { name = anchor.AnchorPath, children = anchorNodeChildren.ToArray() });
            rootNodeChildren.Add(anchorNodeIndex);
        }

        var rootNode = new { name = "ComposedAvatar", children = rootNodeChildren.ToArray() };
        var rootNodeIndex = nodes.Count;
        nodes.Add(rootNode);

        var gltf = new
        {
            asset = new { version = "2.0", generator = "RepoMod.Glb" },
            scenes = new object[] { new { nodes = new[] { rootNodeIndex } } },
            scene = 0,
            nodes,
            meshes,
            materials,
            images = images.Count > 0 ? images : null,
            textures = textures.Count > 0 ? textures : null,
            accessors,
            bufferViews,
            buffers = new object[] { new { byteLength = binaryBuffer.Count } }
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(gltf);
        var glbBytes = PackGlb(jsonBytes, binaryBuffer.ToArray());

        return new GlbBuildResult(glbBytes, diagnostics);
    }

    private static void TryAddOptionalAttribute(
        string primitiveId,
        string semantic,
        IReadOnlyList<float>? values,
        int componentsPerVertex,
        string accessorType,
        ICollection<GlbBuildDiagnostic> diagnostics,
        List<byte> binaryBuffer,
        List<object> bufferViews,
        List<object> accessors,
        IDictionary<string, int> attributes)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        if (values.Count % componentsPerVertex != 0)
        {
            diagnostics.Add(new GlbBuildDiagnostic(
                "warning",
                "INVALID_ATTRIBUTE_COMPONENTS",
                $"Primitive '{primitiveId}' attribute '{semantic}' has invalid component count and was skipped.",
                primitiveId));
            return;
        }

        var accessorIndex = AddFloatAccessor(binaryBuffer, bufferViews, accessors, values, componentsPerVertex, accessorType, includeMinMax: false);
        attributes[semantic] = accessorIndex;
    }

    private static int AddIndexAccessor(
        List<byte> binaryBuffer,
        List<object> bufferViews,
        List<object> accessors,
        IReadOnlyList<int> indices)
    {
        AlignBuffer(binaryBuffer, 4);
        var byteOffset = binaryBuffer.Count;
        foreach (var index in indices)
        {
            var bytes = BitConverter.GetBytes((uint)index);
            binaryBuffer.AddRange(bytes);
        }

        var byteLength = indices.Count * sizeof(uint);
        var bufferViewIndex = bufferViews.Count;
        bufferViews.Add(new
        {
            buffer = 0,
            byteOffset,
            byteLength,
            target = 34963
        });

        var accessorIndex = accessors.Count;
        accessors.Add(new
        {
            bufferView = bufferViewIndex,
            byteOffset = 0,
            componentType = 5125,
            count = indices.Count,
            type = "SCALAR",
            min = new[] { indices.Min() },
            max = new[] { indices.Max() }
        });

        return accessorIndex;
    }

    private static int AddFloatAccessor(
        List<byte> binaryBuffer,
        List<object> bufferViews,
        List<object> accessors,
        IReadOnlyList<float> values,
        int componentsPerVertex,
        string accessorType,
        bool includeMinMax)
    {
        AlignBuffer(binaryBuffer, 4);
        var byteOffset = binaryBuffer.Count;
        foreach (var value in values)
        {
            binaryBuffer.AddRange(BitConverter.GetBytes(value));
        }

        var byteLength = values.Count * sizeof(float);
        var bufferViewIndex = bufferViews.Count;
        bufferViews.Add(new
        {
            buffer = 0,
            byteOffset,
            byteLength,
            byteStride = componentsPerVertex * sizeof(float),
            target = 34962
        });

        var vertexCount = values.Count / componentsPerVertex;
        var accessorIndex = accessors.Count;

        if (includeMinMax)
        {
            var mins = new float[componentsPerVertex];
            var maxs = new float[componentsPerVertex];
            Array.Fill(mins, float.MaxValue);
            Array.Fill(maxs, float.MinValue);

            for (var i = 0; i < values.Count; i += componentsPerVertex)
            {
                for (var component = 0; component < componentsPerVertex; component++)
                {
                    var value = values[i + component];
                    if (value < mins[component])
                    {
                        mins[component] = value;
                    }

                    if (value > maxs[component])
                    {
                        maxs[component] = value;
                    }
                }
            }

            accessors.Add(new
            {
                bufferView = bufferViewIndex,
                byteOffset = 0,
                componentType = 5126,
                count = vertexCount,
                type = accessorType,
                min = mins,
                max = maxs
            });
        }
        else
        {
            accessors.Add(new
            {
                bufferView = bufferViewIndex,
                byteOffset = 0,
                componentType = 5126,
                count = vertexCount,
                type = accessorType
            });
        }

        return accessorIndex;
    }

    private static int ResolveMaterialIndex(
        string? materialObjectId,
        ICollection<object> materials,
        IDictionary<string, int> materialIndexById)
    {
        var key = string.IsNullOrWhiteSpace(materialObjectId) ? "default" : materialObjectId;
        if (materialIndexById.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var material = new
        {
            name = key,
            pbrMetallicRoughness = new
            {
                baseColorFactor = new[] { 1f, 1f, 1f, 1f },
                metallicFactor = 0f,
                roughnessFactor = 1f
            },
            alphaMode = "OPAQUE"
        };

        var index = materials.Count;
        materials.Add(material);
        materialIndexById[key] = index;
        return index;
    }

    private static void BuildTextures(
        IReadOnlyList<UnityRenderTexture> renderTextures,
        ICollection<object> images,
        ICollection<object> textures,
        IDictionary<string, int> textureIndexById)
    {
        foreach (var texture in renderTextures.OrderBy(item => item.ObjectId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(texture.ImageDataBase64))
            {
                continue;
            }

            if (textureIndexById.ContainsKey(texture.ObjectId))
            {
                continue;
            }

            var imageIndex = images.Count;
            images.Add(new
            {
                uri = $"data:image/png;base64,{texture.ImageDataBase64}"
            });

            var textureIndex = textures.Count;
            textures.Add(new
            {
                source = imageIndex
            });

            textureIndexById[texture.ObjectId] = textureIndex;
        }
    }

    private static void BuildMaterials(
        IReadOnlyList<UnityRenderMaterial> renderMaterials,
        ICollection<object> materials,
        IDictionary<string, int> materialIndexById,
        IReadOnlyDictionary<string, int> textureIndexById)
    {
        foreach (var material in renderMaterials.OrderBy(item => item.ObjectId, StringComparer.Ordinal))
        {
            if (materialIndexById.ContainsKey(material.ObjectId))
            {
                continue;
            }

            var baseColorFactor = ResolveBaseColorFactor(material);
            var baseColorTextureIndex = ResolveTextureIndex(material, textureIndexById, "_MainTex", "_BaseMap", "BaseColor", "Albedo", "albedo");
            var normalTextureIndex = ResolveTextureIndex(material, textureIndexById, "_BumpMap", "_NormalMap", "NormalMap", "normalMap");
            var emissiveTextureIndex = ResolveTextureIndex(material, textureIndexById, "_EmissionMap", "_EmissiveMap", "Emissive", "emission");

            var pbrMetallicRoughness = new Dictionary<string, object>
            {
                ["baseColorFactor"] = baseColorFactor,
                ["metallicFactor"] = ResolveFloatProperty(material, "_Metallic", 0f),
                ["roughnessFactor"] = ResolveRoughness(material)
            };

            if (baseColorTextureIndex is not null)
            {
                pbrMetallicRoughness["baseColorTexture"] = new { index = baseColorTextureIndex.Value };
            }

            var materialObject = new Dictionary<string, object>
            {
                ["name"] = material.ObjectId,
                ["pbrMetallicRoughness"] = pbrMetallicRoughness,
                ["alphaMode"] = "OPAQUE"
            };

            if (normalTextureIndex is not null)
            {
                materialObject["normalTexture"] = new { index = normalTextureIndex.Value };
            }

            if (emissiveTextureIndex is not null)
            {
                materialObject["emissiveTexture"] = new { index = emissiveTextureIndex.Value };
                materialObject["emissiveFactor"] = ResolveEmissiveFactor(material);
            }

            materialIndexById[material.ObjectId] = materials.Count;
            materials.Add(materialObject);
        }
    }

    private static int? ResolveTextureIndex(
        UnityRenderMaterial material,
        IReadOnlyDictionary<string, int> textureIndexById,
        params string[] slotNames)
    {
        foreach (var slotName in slotNames)
        {
            var binding = material.TextureBindings
                .FirstOrDefault(item => string.Equals(item.SlotName, slotName, StringComparison.OrdinalIgnoreCase));
            if (binding is null)
            {
                continue;
            }

            if (textureIndexById.TryGetValue(binding.TextureObjectId, out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static float[] ResolveBaseColorFactor(UnityRenderMaterial material)
    {
        var color = material.ColorProperties
            .FirstOrDefault(item => string.Equals(item.Name, "_Color", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(item.Name, "BaseColor", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(item.Name, "Color", StringComparison.OrdinalIgnoreCase));

        if (color is null)
        {
            return new[] { 1f, 1f, 1f, 1f };
        }

        return new[] { color.R, color.G, color.B, color.A };
    }

    private static float ResolveFloatProperty(UnityRenderMaterial material, string name, float fallback)
    {
        var property = material.FloatProperties
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return property is null ? fallback : property.Value;
    }

    private static float ResolveRoughness(UnityRenderMaterial material)
    {
        var glossiness = ResolveFloatProperty(material, "_Glossiness", 1f);
        return 1f - glossiness;
    }

    private static float[] ResolveEmissiveFactor(UnityRenderMaterial material)
    {
        var color = material.ColorProperties
            .FirstOrDefault(item => string.Equals(item.Name, "_EmissionColor", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(item.Name, "EmissiveColor", StringComparison.OrdinalIgnoreCase));

        if (color is null)
        {
            return new[] { 1f, 1f, 1f };
        }

        return new[] { color.R, color.G, color.B };
    }

    private static byte[] PackGlb(byte[] jsonBytes, byte[] binaryBytes)
    {
        var paddedJson = PadChunk(jsonBytes, 0x20);
        var paddedBin = PadChunk(binaryBytes, 0x00);

        var totalLength = 12 + 8 + paddedJson.Length + 8 + paddedBin.Length;
        var output = new byte[totalLength];
        var offset = 0;

        WriteUInt32(output, ref offset, GlbMagic);
        WriteUInt32(output, ref offset, GlbVersion);
        WriteUInt32(output, ref offset, (uint)totalLength);

        WriteUInt32(output, ref offset, (uint)paddedJson.Length);
        WriteUInt32(output, ref offset, JsonChunkType);
        Buffer.BlockCopy(paddedJson, 0, output, offset, paddedJson.Length);
        offset += paddedJson.Length;

        WriteUInt32(output, ref offset, (uint)paddedBin.Length);
        WriteUInt32(output, ref offset, BinChunkType);
        Buffer.BlockCopy(paddedBin, 0, output, offset, paddedBin.Length);

        return output;
    }

    private static byte[] PadChunk(byte[] bytes, byte padByte)
    {
        var remainder = bytes.Length % 4;
        if (remainder == 0)
        {
            return bytes;
        }

        var paddedLength = bytes.Length + (4 - remainder);
        var padded = new byte[paddedLength];
        Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
        for (var i = bytes.Length; i < paddedLength; i++)
        {
            padded[i] = padByte;
        }

        return padded;
    }

    private static void WriteUInt32(byte[] target, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset, 4), value);
        offset += 4;
    }

    private static void AlignBuffer(List<byte> buffer, int alignment)
    {
        while (buffer.Count % alignment != 0)
        {
            buffer.Add(0);
        }
    }
}
