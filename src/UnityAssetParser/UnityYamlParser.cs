using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;

namespace UnityAssetParser;

internal static class UnityYamlParser
{
    public static void Parse(byte[] data, string sourceName, BaseAssetsContext context)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Containers.Add(new ParsedContainer(sourceName, ContainerKind.Unknown, data.Length));

        var text = Encoding.UTF8.GetString(data);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var currentTypeId = -1;
        var currentFileId = 0L;
        var gameObjectName = string.Empty;
        var gameObjectActive = true;
        var gameObjectLayer = 0;

        var sourceGuid = TryGetGuidFromSourceName(sourceName);

        var meshName = string.Empty;
        var meshIndexFormat = 0;
        var meshVertexCount = 0;
        var meshVertexDataSize = 0;
        var meshVertexDataHex = string.Empty;
        var meshIndexBufferHex = string.Empty;
        var meshChannels = new List<SemanticVertexChannelInfo>();
        var meshSubMeshes = new List<MeshSubMeshDraft>();
        var inSubMeshes = false;
        var inVertexData = false;
        var inChannels = false;
        var currentChannel = (MeshChannelDraft?)null;
        var currentSubMesh = (MeshSubMeshDraft?)null;

        var meshFilterGameObjectId = 0L;
        var meshFilterMeshFileId = 0L;
        var meshFilterMeshGuid = string.Empty;

        var transformGameObjectId = 0L;
        var transformParentId = (long?)null;
        var transformChildren = new List<long>();
        var transformLocalPosition = new SemanticVector3(0, 0, 0);
        var transformLocalRotation = new SemanticQuaternion(1, 0, 0, 0);
        var transformLocalScale = new SemanticVector3(1, 1, 1);
        var inChildren = false;

        void Flush()
        {
            if (currentTypeId == 1 && currentFileId != 0)
            {
                var name = string.IsNullOrWhiteSpace(gameObjectName) ? sourceName : gameObjectName;
                context.SemanticGameObjects.Add(new SemanticGameObjectInfo(currentFileId, name, gameObjectActive, gameObjectLayer));

                var anchorTag = TryResolveAnchorTag(name);
                if (anchorTag is not null)
                {
                    context.SemanticAnchorPoints.Add(new SemanticAnchorPointInfo(currentFileId, anchorTag, name));
                }
            }
            else if (currentTypeId == 4 && currentFileId != 0)
            {
                context.SemanticTransforms.Add(new SemanticTransformInfo(
                    currentFileId,
                    transformGameObjectId,
                    transformParentId,
                    transformChildren.ToArray(),
                    transformLocalPosition,
                    transformLocalRotation,
                    transformLocalScale));
            }
            else if (currentTypeId == 43 && currentFileId != 0)
            {
                if (currentChannel is not null)
                {
                    meshChannels.Add(currentChannel.ToInfo(meshChannels.Count));
                    currentChannel = null;
                }

                if (currentSubMesh is not null)
                {
                    meshSubMeshes.Add(currentSubMesh);
                    currentSubMesh = null;
                }

                var meshPathId = BuildYamlPathId(sourceGuid, currentFileId);
                if (TryBuildMesh(
                    meshPathId,
                    meshName,
                    meshIndexFormat,
                    meshVertexCount,
                    meshVertexDataSize,
                    meshVertexDataHex,
                    meshIndexBufferHex,
                    meshChannels,
                    meshSubMeshes,
                    out var meshInfo))
                {
                    context.SemanticMeshes.Add(meshInfo);
                }
                else
                {
                    context.Warnings.Add($"Mesh parse failed for '{meshName}' ({meshPathId}). vtx={meshVertexCount} dataSize={meshVertexDataSize} dataHex={meshVertexDataHex.Length} indexHex={meshIndexBufferHex.Length}");
                }
            }
            else if (currentTypeId == 33 && currentFileId != 0)
            {
                if (meshFilterGameObjectId != 0 && meshFilterMeshFileId != 0 && !string.IsNullOrWhiteSpace(meshFilterMeshGuid))
                {
                    var meshPathId = BuildYamlPathId(meshFilterMeshGuid, meshFilterMeshFileId);
                    context.SemanticMeshFilters.Add(new SemanticMeshFilterInfo(currentFileId, meshFilterGameObjectId, meshPathId));
                }
            }
        }

        void ResetState(int typeId, long fileId)
        {
            currentTypeId = typeId;
            currentFileId = fileId;
            gameObjectName = string.Empty;
            gameObjectActive = true;
            gameObjectLayer = 0;
            meshName = string.Empty;
            meshIndexFormat = 0;
            meshVertexCount = 0;
            meshVertexDataSize = 0;
            meshVertexDataHex = string.Empty;
            meshIndexBufferHex = string.Empty;
            meshChannels.Clear();
            meshSubMeshes.Clear();
            inSubMeshes = false;
            inVertexData = false;
            inChannels = false;
            currentChannel = null;
            currentSubMesh = null;
            meshFilterGameObjectId = 0;
            meshFilterMeshFileId = 0;
            meshFilterMeshGuid = string.Empty;
            transformGameObjectId = 0;
            transformParentId = null;
            transformChildren = new List<long>();
            transformLocalPosition = new SemanticVector3(0, 0, 0);
            transformLocalRotation = new SemanticQuaternion(1, 0, 0, 0);
            transformLocalScale = new SemanticVector3(1, 1, 1);
            inChildren = false;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                Flush();
                var header = line.Substring("--- !u!".Length);
                var ampersandIndex = header.IndexOf('&');
                if (ampersandIndex > 0
                    && int.TryParse(header.Substring(0, ampersandIndex).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeId)
                    && long.TryParse(header.Substring(ampersandIndex + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId))
                {
                    ResetState(typeId, fileId);
                }
                else
                {
                    ResetState(-1, 0);
                }

                continue;
            }

            if (currentTypeId == -1)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (currentTypeId == 1)
            {
                if (TryReadSimpleValue(trimmed, "m_Name:", out var nameValue))
                {
                    gameObjectName = nameValue;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_IsActive:", out var activeValue)
                    && int.TryParse(activeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var activeInt))
                {
                    gameObjectActive = activeInt != 0;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_Layer:", out var layerValue)
                    && int.TryParse(layerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var layer))
                {
                    gameObjectLayer = layer;
                    continue;
                }
            }
            else if (currentTypeId == 33)
            {
                if (TryReadFileIdValue(trimmed, "m_GameObject:", out var gameObjectId))
                {
                    meshFilterGameObjectId = gameObjectId;
                    continue;
                }

                if (TryReadMeshReference(trimmed, out var meshFileId, out var meshGuid))
                {
                    meshFilterMeshFileId = meshFileId;
                    meshFilterMeshGuid = meshGuid;
                    continue;
                }
            }
            else if (currentTypeId == 43)
            {
                if (TryReadSimpleValue(trimmed, "m_Name:", out var meshNameValue))
                {
                    meshName = meshNameValue;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_IndexFormat:", out var indexFormatValue)
                    && int.TryParse(indexFormatValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndexFormat))
                {
                    meshIndexFormat = parsedIndexFormat;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_IndexBuffer:", out var indexBufferValue))
                {
                    meshIndexBufferHex = indexBufferValue;
                    continue;
                }

                if (trimmed == "m_SubMeshes:")
                {
                    inSubMeshes = true;
                    continue;
                }

                if (inSubMeshes)
                {
                    if (trimmed.StartsWith("- serializedVersion:", StringComparison.Ordinal))
                    {
                        if (currentSubMesh is not null)
                        {
                            meshSubMeshes.Add(currentSubMesh);
                        }

                        currentSubMesh = new MeshSubMeshDraft();
                        continue;
                    }

                    if (trimmed.StartsWith("m_Shapes:", StringComparison.Ordinal)
                        || trimmed.StartsWith("m_BindPose:", StringComparison.Ordinal))
                    {
                        if (currentSubMesh is not null)
                        {
                            meshSubMeshes.Add(currentSubMesh);
                            currentSubMesh = null;
                        }
                        inSubMeshes = false;
                        continue;
                    }

                    if (currentSubMesh is not null)
                    {
                        if (TryReadSimpleValue(trimmed, "firstByte:", out var firstByteValue)
                            && int.TryParse(firstByteValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstByte))
                        {
                            currentSubMesh.FirstByte = firstByte;
                            continue;
                        }

                        if (TryReadSimpleValue(trimmed, "indexCount:", out var indexCountValue)
                            && int.TryParse(indexCountValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indexCount))
                        {
                            currentSubMesh.IndexCount = indexCount;
                            continue;
                        }

                        if (TryReadSimpleValue(trimmed, "topology:", out var topologyValue)
                            && int.TryParse(topologyValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var topology))
                        {
                            currentSubMesh.Topology = topology;
                            continue;
                        }

                        if (TryReadSimpleValue(trimmed, "firstVertex:", out var firstVertexValue)
                            && int.TryParse(firstVertexValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstVertex))
                        {
                            currentSubMesh.FirstVertex = firstVertex;
                            continue;
                        }

                        if (TryReadSimpleValue(trimmed, "vertexCount:", out var vertexCountValue)
                            && int.TryParse(vertexCountValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vertexCount))
                        {
                            currentSubMesh.VertexCount = vertexCount;
                            continue;
                        }

                        if (TryReadVector3(trimmed, "m_Center:", out var center))
                        {
                            currentSubMesh.BoundsCenter = center;
                            continue;
                        }

                        if (TryReadVector3(trimmed, "m_Extent:", out var extent))
                        {
                            currentSubMesh.BoundsExtent = extent;
                            continue;
                        }
                    }
                }

                if (trimmed == "m_VertexData:")
                {
                    inVertexData = true;
                    continue;
                }

                if (inVertexData)
                {
                    if (TryReadSimpleValue(trimmed, "m_VertexCount:", out var vertexCountValue)
                        && int.TryParse(vertexCountValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedVertexCount))
                    {
                        meshVertexCount = parsedVertexCount;
                        continue;
                    }

                    if (trimmed == "m_Channels:")
                    {
                        inChannels = true;
                        continue;
                    }

                    if (inChannels)
                    {
                        if (trimmed.StartsWith("- stream:", StringComparison.Ordinal))
                        {
                            if (currentChannel is not null)
                            {
                                meshChannels.Add(currentChannel.ToInfo(meshChannels.Count));
                            }

                            var streamValue = trimmed.Substring("- stream:".Length).Trim();
                            var stream = int.TryParse(streamValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStream)
                                ? parsedStream
                                : 0;
                            currentChannel = new MeshChannelDraft(stream);
                            continue;
                        }

                        if (currentChannel is not null)
                        {
                            if (TryReadSimpleValue(trimmed, "offset:", out var offsetValue)
                                && int.TryParse(offsetValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
                            {
                                currentChannel.Offset = offset;
                                continue;
                            }

                            if (TryReadSimpleValue(trimmed, "format:", out var formatValue)
                                && int.TryParse(formatValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var format))
                            {
                                currentChannel.Format = format;
                                continue;
                            }

                            if (TryReadSimpleValue(trimmed, "dimension:", out var dimensionValue)
                                && int.TryParse(dimensionValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimension))
                            {
                                currentChannel.Dimension = dimension;
                                continue;
                            }
                        }
                    }

                    if (TryReadSimpleValue(trimmed, "m_DataSize:", out var dataSizeValue)
                        && int.TryParse(dataSizeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dataSize))
                    {
                        meshVertexDataSize = dataSize;
                        continue;
                    }

                    if (TryReadSimpleValue(trimmed, "_typelessdata:", out var typelessData))
                    {
                        meshVertexDataHex = typelessData;
                        continue;
                    }

                    if (trimmed.StartsWith("m_CompressedMesh:", StringComparison.Ordinal)
                        || trimmed.StartsWith("m_LocalAABB:", StringComparison.Ordinal))
                    {
                        if (currentChannel is not null)
                        {
                            meshChannels.Add(currentChannel.ToInfo(meshChannels.Count));
                            currentChannel = null;
                        }

                        inChannels = false;
                        inVertexData = false;
                    }
                }
            }
            else if (currentTypeId == 4)
            {
                if (trimmed.StartsWith("m_Children:", StringComparison.Ordinal))
                {
                    inChildren = true;
                    continue;
                }

                if (inChildren && trimmed.StartsWith("- fileID:", StringComparison.Ordinal))
                {
                    var idText = trimmed.Substring("- fileID:".Length).Trim();
                    if (long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var childId))
                    {
                        transformChildren.Add(childId);
                    }
                    continue;
                }

                if (!trimmed.StartsWith("- fileID:", StringComparison.Ordinal) && trimmed.Contains(":", StringComparison.Ordinal))
                {
                    inChildren = false;
                }

                if (TryReadFileIdValue(trimmed, "m_GameObject:", out var gameObjectId))
                {
                    transformGameObjectId = gameObjectId;
                    continue;
                }

                if (TryReadFileIdValue(trimmed, "m_Father:", out var fatherId))
                {
                    transformParentId = fatherId == 0 ? null : fatherId;
                    continue;
                }

                if (TryReadVector3(trimmed, "m_LocalPosition:", out var localPosition))
                {
                    transformLocalPosition = localPosition;
                    continue;
                }

                if (TryReadQuaternion(trimmed, "m_LocalRotation:", out var localRotation))
                {
                    transformLocalRotation = localRotation;
                    continue;
                }

                if (TryReadVector3(trimmed, "m_LocalScale:", out var localScale))
                {
                    transformLocalScale = localScale;
                    continue;
                }
            }
        }

        Flush();
    }

    public static bool LooksLikeYaml(byte[] data)
    {
        if (data is null || data.Length < 5)
        {
            return false;
        }

        var startIndex = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            startIndex = 3;
        }

        while (startIndex < data.Length && data[startIndex] <= 0x20)
        {
            startIndex++;
        }

        if (startIndex >= data.Length)
        {
            return false;
        }

        var prefixLength = Math.Min(64, data.Length - startIndex);
        var prefix = Encoding.ASCII.GetString(data, startIndex, prefixLength);
        return prefix.StartsWith("%YAML", StringComparison.Ordinal)
            || prefix.StartsWith("--- !u!", StringComparison.Ordinal);
    }

    private static bool TryReadSimpleValue(string line, string key, out string value)
    {
        if (line.StartsWith(key, StringComparison.Ordinal))
        {
            value = line.Substring(key.Length).Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadMeshReference(string line, out long fileId, out string guid)
    {
        fileId = 0;
        guid = string.Empty;

        if (!line.StartsWith("m_Mesh:", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryReadMapValue(line, "fileID", out var fileIdText)
            || !long.TryParse(fileIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId))
        {
            return false;
        }

        if (TryReadMapValue(line, "guid", out var guidValue))
        {
            guid = guidValue;
        }

        return fileId != 0 && !string.IsNullOrWhiteSpace(guid);
    }

    private static bool TryReadMapValue(string line, string key, out string value)
    {
        value = string.Empty;
        var keyIndex = line.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return false;
        }

        var colonIndex = line.IndexOf(':', keyIndex + key.Length);
        if (colonIndex < 0)
        {
            return false;
        }

        var endIndex = line.IndexOf(',', colonIndex + 1);
        if (endIndex < 0)
        {
            endIndex = line.IndexOf('}', colonIndex + 1);
        }

        if (endIndex < 0)
        {
            endIndex = line.Length;
        }

        value = line.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
        return value.Length > 0;
    }

    private static string? TryGetGuidFromSourceName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        var slashIndex = sourceName.IndexOf('/');
        if (slashIndex <= 0)
        {
            return null;
        }

        return sourceName.Substring(0, slashIndex);
    }

    private static long BuildYamlPathId(string? guid, long fileId)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return fileId;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(guid));
        var hash64 = BitConverter.ToInt64(hash, 0);
        return hash64 ^ fileId;
    }

    private static bool TryBuildMesh(
        long pathId,
        string name,
        int indexFormat,
        int vertexCount,
        int vertexDataSize,
        string vertexDataHex,
        string indexBufferHex,
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        IReadOnlyList<MeshSubMeshDraft> subMeshes,
        out SemanticMeshInfo meshInfo)
    {
        meshInfo = default!;

        if (vertexCount <= 0 || string.IsNullOrWhiteSpace(vertexDataHex) || string.IsNullOrWhiteSpace(indexBufferHex))
        {
            return false;
        }

        var vertexBytes = DecodeHexString(vertexDataHex);
        var indexBytes = DecodeHexString(indexBufferHex);
        if (vertexBytes.Length == 0 || indexBytes.Length == 0)
        {
            return false;
        }

        var indexElementSize = indexFormat == 1 ? 4 : 2;
        var indexElementCount = indexBytes.Length / indexElementSize;
        var decodedIndices = DecodeIndices(indexBytes, indexElementSize);

        var stride = vertexCount > 0 && vertexDataSize > 0 && vertexDataSize % vertexCount == 0
            ? vertexDataSize / vertexCount
            : CalculateStride(channels);

        var positions = DecodeVector3Channel(vertexBytes, vertexCount, stride, GetChannel(channels, 0));
        if (positions.Count == 0 && stride >= 12)
        {
            var fallbackChannel = new SemanticVertexChannelInfo(0, 0, 0, 0, 3);
            positions = DecodeVector3Channel(vertexBytes, vertexCount, stride, fallbackChannel);
        }
        var normals = DecodeVector3Channel(vertexBytes, vertexCount, stride, GetChannel(channels, 1));
        var uv0 = DecodeVector2Channel(vertexBytes, vertexCount, stride, GetChannel(channels, 4));

        if (positions.Count == 0 || decodedIndices.Count == 0)
        {
            return false;
        }

        var subMeshInfos = new List<SemanticSubMeshInfo>();
        var topologyList = new List<int>();
        SemanticVector3? boundsMinValue = null;
        SemanticVector3? boundsMaxValue = null;

        if (subMeshes.Count == 0)
        {
            subMeshInfos.Add(new SemanticSubMeshInfo(0, decodedIndices.Count, 0, 0, vertexCount));
            topologyList.Add(0);
        }
        else
        {
            foreach (var subMesh in subMeshes)
            {
                subMeshInfos.Add(new SemanticSubMeshInfo(
                    subMesh.FirstByte,
                    subMesh.IndexCount,
                    subMesh.Topology,
                    subMesh.FirstVertex,
                    subMesh.VertexCount));
                topologyList.Add(subMesh.Topology);

                if (subMesh.BoundsCenter.HasValue && subMesh.BoundsExtent.HasValue)
                {
                    var center = subMesh.BoundsCenter.Value;
                    var extent = subMesh.BoundsExtent.Value;
                    var min = new SemanticVector3(center.X - extent.X, center.Y - extent.Y, center.Z - extent.Z);
                    var max = new SemanticVector3(center.X + extent.X, center.Y + extent.Y, center.Z + extent.Z);
                    boundsMinValue = boundsMinValue.HasValue
                        ? new SemanticVector3(
                            MathF.Min(boundsMinValue.Value.X, min.X),
                            MathF.Min(boundsMinValue.Value.Y, min.Y),
                            MathF.Min(boundsMinValue.Value.Z, min.Z))
                        : min;
                    boundsMaxValue = boundsMaxValue.HasValue
                        ? new SemanticVector3(
                            MathF.Max(boundsMaxValue.Value.X, max.X),
                            MathF.Max(boundsMaxValue.Value.Y, max.Y),
                            MathF.Max(boundsMaxValue.Value.Z, max.Z))
                        : max;
                }
            }
        }

        if (!boundsMinValue.HasValue || !boundsMaxValue.HasValue)
        {
            boundsMinValue = new SemanticVector3(
                positions.Min(p => p.X),
                positions.Min(p => p.Y),
                positions.Min(p => p.Z));
            boundsMaxValue = new SemanticVector3(
                positions.Max(p => p.X),
                positions.Max(p => p.Y),
                positions.Max(p => p.Z));
        }

        var boundsCenter = new SemanticVector3(
            (boundsMinValue.Value.X + boundsMaxValue.Value.X) * 0.5f,
            (boundsMinValue.Value.Y + boundsMaxValue.Value.Y) * 0.5f,
            (boundsMinValue.Value.Z + boundsMaxValue.Value.Z) * 0.5f);
        var boundsExtent = new SemanticVector3(
            (boundsMaxValue.Value.X - boundsMinValue.Value.X) * 0.5f,
            (boundsMaxValue.Value.Y - boundsMinValue.Value.Y) * 0.5f,
            (boundsMaxValue.Value.Z - boundsMinValue.Value.Z) * 0.5f);

        var channelFlags = new SemanticMeshChannelFlags(
            positions.Count >= vertexCount,
            normals.Count >= vertexCount,
            false,
            false,
            uv0.Count >= vertexCount,
            false);

        var vertexStreams = new List<SemanticVertexStreamInfo>
        {
            new SemanticVertexStreamInfo(0, 0, stride, vertexBytes.Length)
        };

        meshInfo = new SemanticMeshInfo(
            pathId,
            string.IsNullOrWhiteSpace(name) ? $"mesh_{pathId}" : name,
            new SemanticBoundsInfo(boundsCenter, boundsExtent),
            channelFlags,
            indexFormat,
            decodedIndices,
            vertexBytes.Length,
            positions,
            normals,
            Array.Empty<SemanticVector4>(),
            Array.Empty<SemanticVector4>(),
            uv0,
            Array.Empty<SemanticVector2>(),
            channels,
            vertexStreams,
            indexElementSize,
            indexElementCount,
            indexBytes.Length,
            subMeshInfos.Count,
            subMeshInfos,
            topologyList,
            vertexCount);

        return true;
    }

    private static int CalculateStride(IReadOnlyList<SemanticVertexChannelInfo> channels)
    {
        var max = 0;
        foreach (var channel in channels)
        {
            if (channel.Dimension <= 0 || channel.Dimension > 4)
            {
                continue;
            }

            var componentSize = channel.Format == 1 ? 2 : 4;
            var size = channel.Offset + (channel.Dimension * componentSize);
            max = Math.Max(max, size);
        }

        return max;
    }

    private static SemanticVertexChannelInfo? GetChannel(IReadOnlyList<SemanticVertexChannelInfo> channels, int index)
    {
        return index >= 0 && index < channels.Count ? channels[index] : null;
    }

    private static IReadOnlyList<SemanticVector3> DecodeVector3Channel(
        byte[] vertexBytes,
        int vertexCount,
        int stride,
        SemanticVertexChannelInfo? channel)
    {
        if (channel is null || channel.Dimension < 3 || channel.Dimension > 4 || stride <= 0)
        {
            return Array.Empty<SemanticVector3>();
        }

        var componentSize = channel.Format == 1 ? 2 : 4;
        var offset = channel.Offset;
        var list = new List<SemanticVector3>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = (i * stride) + offset;
            if (baseOffset + (componentSize * 3) > vertexBytes.Length)
            {
                break;
            }

            var x = ReadFloat(vertexBytes, baseOffset, channel.Format);
            var y = ReadFloat(vertexBytes, baseOffset + componentSize, channel.Format);
            var z = ReadFloat(vertexBytes, baseOffset + (componentSize * 2), channel.Format);
            list.Add(new SemanticVector3(x, y, z));
        }

        return list;
    }

    private static IReadOnlyList<SemanticVector2> DecodeVector2Channel(
        byte[] vertexBytes,
        int vertexCount,
        int stride,
        SemanticVertexChannelInfo? channel)
    {
        if (channel is null || channel.Dimension < 2 || channel.Dimension > 4 || stride <= 0)
        {
            return Array.Empty<SemanticVector2>();
        }

        var componentSize = channel.Format == 1 ? 2 : 4;
        var offset = channel.Offset;
        var list = new List<SemanticVector2>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = (i * stride) + offset;
            if (baseOffset + (componentSize * 2) > vertexBytes.Length)
            {
                break;
            }

            var x = ReadFloat(vertexBytes, baseOffset, channel.Format);
            var y = ReadFloat(vertexBytes, baseOffset + componentSize, channel.Format);
            list.Add(new SemanticVector2(x, y));
        }

        return list;
    }

    private static float ReadFloat(byte[] bytes, int offset, int format)
    {
        if (format == 1)
        {
            var half = BitConverter.ToUInt16(bytes, offset);
            return HalfToSingle(half);
        }

        return BitConverter.ToSingle(bytes, offset);
    }

    private static float HalfToSingle(ushort value)
    {
        var sign = (value >> 15) & 0x0001;
        var exponent = (value >> 10) & 0x001F;
        var mantissa = value & 0x03FF;

        if (exponent == 0)
        {
            return (float)(Math.Pow(-1, sign) * Math.Pow(2, -14) * (mantissa / 1024.0));
        }

        if (exponent == 31)
        {
            return mantissa == 0 ? (sign == 1 ? float.NegativeInfinity : float.PositiveInfinity) : float.NaN;
        }

        return (float)(Math.Pow(-1, sign) * Math.Pow(2, exponent - 15) * (1 + mantissa / 1024.0));
    }

    private static IReadOnlyList<uint> DecodeIndices(byte[] indexBytes, int elementSize)
    {
        var list = new List<uint>(indexBytes.Length / elementSize);
        for (var i = 0; i + elementSize <= indexBytes.Length; i += elementSize)
        {
            list.Add(elementSize == 2
                ? BitConverter.ToUInt16(indexBytes, i)
                : BitConverter.ToUInt32(indexBytes, i));
        }

        return list;
    }

    private static byte[] DecodeHexString(string hex)
    {
        var builder = new StringBuilder(hex.Length);
        foreach (var ch in hex)
        {
            if ((ch >= '0' && ch <= '9')
                || (ch >= 'a' && ch <= 'f')
                || (ch >= 'A' && ch <= 'F'))
            {
                builder.Append(ch);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (cleaned.Length % 2 != 0)
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 1);
        }

        var bytes = new byte[cleaned.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private sealed class MeshChannelDraft
    {
        public MeshChannelDraft(int stream)
        {
            Stream = stream;
        }

        public int Stream { get; }
        public int Offset { get; set; }
        public int Format { get; set; }
        public int Dimension { get; set; }

        public SemanticVertexChannelInfo ToInfo(int channelIndex)
            => new SemanticVertexChannelInfo(channelIndex, Stream, Offset, Format, Dimension);
    }

    private sealed class MeshSubMeshDraft
    {
        public int FirstByte { get; set; }
        public int IndexCount { get; set; }
        public int Topology { get; set; }
        public int FirstVertex { get; set; }
        public int VertexCount { get; set; }
        public SemanticVector3? BoundsCenter { get; set; }
        public SemanticVector3? BoundsExtent { get; set; }
    }

    private static bool TryReadFileIdValue(string line, string key, out long value)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            value = 0;
            return false;
        }

        var braceIndex = line.IndexOf('{');
        if (braceIndex < 0)
        {
            value = 0;
            return false;
        }

        var fileIdIndex = line.IndexOf("fileID", StringComparison.Ordinal);
        if (fileIdIndex < 0)
        {
            value = 0;
            return false;
        }

        var colonIndex = line.IndexOf(':', fileIdIndex);
        if (colonIndex < 0)
        {
            value = 0;
            return false;
        }

        var endIndex = line.IndexOf('}', colonIndex);
        if (endIndex < 0)
        {
            endIndex = line.Length;
        }

        var valueText = line.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
        return long.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadVector3(string line, string key, out SemanticVector3 value)
    {
        if (!TryReadInlineMap(line, key, out var map))
        {
            value = default;
            return false;
        }

        value = new SemanticVector3(
            GetFloat(map, "x"),
            GetFloat(map, "y"),
            GetFloat(map, "z"));
        return true;
    }

    private static bool TryReadQuaternion(string line, string key, out SemanticQuaternion value)
    {
        if (!TryReadInlineMap(line, key, out var map))
        {
            value = default;
            return false;
        }

        value = new SemanticQuaternion(
            GetFloat(map, "w"),
            GetFloat(map, "x"),
            GetFloat(map, "y"),
            GetFloat(map, "z"));
        return true;
    }

    private static bool TryReadInlineMap(string line, string key, out Dictionary<string, float> map)
    {
        map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var braceIndex = line.IndexOf('{');
        var endIndex = line.IndexOf('}', braceIndex + 1);
        if (braceIndex < 0 || endIndex < 0)
        {
            return false;
        }

        var content = line.Substring(braceIndex + 1, endIndex - braceIndex - 1);
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var tokens = part.Split(':', 2);
            if (tokens.Length != 2)
            {
                continue;
            }

            var keyName = tokens[0].Trim();
            var valueText = tokens[1].Trim();
            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                map[keyName] = numeric;
            }
        }

        return map.Count > 0;
    }

    private static float GetFloat(Dictionary<string, float> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : 0f;
    }

    private static string? TryResolveAnchorTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = NormalizeName(name);
        if (normalized.Contains("head decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "head";
        }
        if (normalized.Contains("neck decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "neck";
        }
        if (normalized.Contains("body decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }
        if (normalized.Contains("hip decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "hip";
        }
        if (normalized.Contains("l-arm decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("leftarm decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "leftarm";
        }
        if (normalized.Contains("r-arm decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rightarm decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "rightarm";
        }
        if (normalized.Contains("l-leg decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("leftleg decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "leftleg";
        }
        if (normalized.Contains("r-leg decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rightleg decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "rightleg";
        }
        if (normalized.Contains("world decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "world";
        }

        return null;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        return trimmed;
    }
}
