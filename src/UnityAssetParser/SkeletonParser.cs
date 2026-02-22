using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SevenZip.Compression.LZMA;

namespace UnityAssetParser;

internal static class SkeletonParser
{
    private const uint BlocksInfoAtEndFlag = 0x80;
    private const uint BlockInfoNeedsAlignmentFlag = 0x200;
    private const int BundleSignatureMaxLength = 20;

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

        var signature = PeekSignature(data);
        if (IsUnityBundleSignature(signature, out var kind))
        {
            ParseUnityBundle(data, sourceName, kind, context);
            return;
        }

        if (IsSerializedFile(data))
        {
            ParseSerializedFile(data, sourceName, context);
            return;
        }

        context.Containers.Add(new ParsedContainer(sourceName, ContainerKind.Unknown, data.Length));
    }

    private static string PeekSignature(byte[] data)
    {
        var reader = new EndianBinaryReader(data);
        return reader.ReadStringToNull(BundleSignatureMaxLength);
    }

    private static bool IsUnityBundleSignature(string signature, out ContainerKind kind)
    {
        switch (signature)
        {
            case "UnityFS":
                kind = ContainerKind.UnityFs;
                return true;
            case "UnityWeb":
                kind = ContainerKind.UnityWeb;
                return true;
            case "UnityRaw":
            case "UnityArchive":
                kind = ContainerKind.UnityRaw;
                return true;
            default:
                kind = ContainerKind.Unknown;
                return false;
        }
    }

    private static void ParseUnityBundle(byte[] data, string sourceName, ContainerKind kind, BaseAssetsContext context)
    {
        var reader = new EndianBinaryReader(data);
        var signature = reader.ReadStringToNull(BundleSignatureMaxLength);
        reader.IsBigEndian = true;
        var version = reader.ReadUInt32();
        var unityVersion = reader.ReadStringToNull();
        var unityRevision = reader.ReadStringToNull();

        var container = new ParsedContainer(sourceName, kind, data.Length)
        {
            Version = version,
            UnityVersion = unityVersion,
            UnityRevision = unityRevision
        };

        context.Containers.Add(container);

        if (kind == ContainerKind.UnityFs)
        {
            var size = reader.ReadInt64();
            var compressedBlocksInfoSize = reader.ReadUInt32();
            var uncompressedBlocksInfoSize = reader.ReadUInt32();
            var flags = reader.ReadUInt32();
            _ = size;
            _ = uncompressedBlocksInfoSize;

            var blockInfoCompression = (int)(flags & 0x3F);
            var blocksInfoAtEnd = (flags & BlocksInfoAtEndFlag) != 0;
            var blocksInfoOffset = blocksInfoAtEnd
                ? checked((int)(data.Length - compressedBlocksInfoSize))
                : AlignPosition(reader.Position, 16);

            if (blocksInfoOffset < 0 || blocksInfoOffset + compressedBlocksInfoSize > data.Length)
            {
                context.Warnings.Add("UnityFS block info offsets are out of range.");
                return;
            }

            var blockInfoBytes = ReadBlockInfoBytes(
                data,
                blocksInfoOffset,
                (int)compressedBlocksInfoSize,
                (int)uncompressedBlocksInfoSize,
                blockInfoCompression,
                context
            );

            if (blockInfoBytes is null)
            {
                return;
            }

            var dataOffset = blocksInfoAtEnd
                ? AlignPosition(reader.Position, 16)
                : blocksInfoOffset + (int)compressedBlocksInfoSize;

            if (!blocksInfoAtEnd && (flags & BlockInfoNeedsAlignmentFlag) != 0)
            {
                dataOffset = AlignPosition(dataOffset, 16);
            }

            var dataLength = blocksInfoAtEnd
                ? blocksInfoOffset - dataOffset
                : data.Length - dataOffset;

            if (dataLength < 0)
            {
                context.Warnings.Add("UnityFS data segment is out of range.");
                return;
            }

            var blockInfo = ParseBlockInfo(blockInfoBytes);
            var decompressedData = DecompressBlocks(
                data,
                dataOffset,
                dataLength,
                blockInfo.Blocks,
                context
            );

            if (decompressedData is null)
            {
                return;
            }

            var serializedPayloads = new List<byte[]>();
            foreach (var node in blockInfo.Nodes)
            {
                var entry = new ContainerEntry(node.Path, node.Offset, node.Size, node.Flags);
                if (TrySlicePayload(decompressedData, node.Offset, node.Size, out var payload))
                {
                    entry.Payload = payload;
                    serializedPayloads.Add(payload);
                }
                else
                {
                    context.Warnings.Add($"UnityFS entry '{node.Path}' is out of range.");
                }
                container.Entries.Add(entry);
            }

            var externalResourceLookup = BuildExternalResourceLookup(container.Entries);
            var serializedAssetIndex = 0;
            foreach (var serializedPayload in serializedPayloads)
            {
                _ = TryParseSerializedPayload(serializedPayload, serializedAssetIndex, context, externalResourceLookup);
                serializedAssetIndex++;
            }
        }
        else
        {
            context.Warnings.Add($"Bundle signature '{signature}' parsing is not implemented yet.");
        }
    }

    private static bool IsSerializedFile(byte[] data)
    {
        return TryReadSerializedHeader(data, isBigEndian: false, out _)
            || TryReadSerializedHeader(data, isBigEndian: true, out _);
    }

    private static void ParseSerializedFile(
        byte[] data,
        string sourceName,
        BaseAssetsContext context,
        string? serializedFileSourceName = null,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup = null)
    {
        var hasLittleHeader = TryReadSerializedHeader(data, isBigEndian: false, out var littleHeader);
        var hasBigHeader = TryReadSerializedHeader(data, isBigEndian: true, out var bigHeader);

        if (!hasLittleHeader && !hasBigHeader)
        {
            throw new InvalidDataException("Invalid serialized file header.");
        }

        var header = hasLittleHeader ? littleHeader : bigHeader;

        var reader = new EndianBinaryReader(data)
        {
            IsBigEndian = header.EndianFlag != 0,
            Position = header.HeaderSize
        };

        var metadataSize = header.MetadataSize;
        var fileSize = header.FileSize;
        var version = header.Version;
        var dataOffset = header.DataOffset;

        var info = new SerializedFileInfo(serializedFileSourceName ?? sourceName)
        {
            Version = version,
            FileSize = fileSize,
            MetadataSize = metadataSize,
            DataOffset = dataOffset,
            BigEndian = header.EndianFlag != 0
        };

        if (version >= 7)
        {
            reader.ReadStringToNull();
        }
        if (version >= 8)
        {
            reader.ReadInt32();
        }
        var enableTypeTree = true;
        if (version >= 13)
        {
            enableTypeTree = reader.ReadByte() != 0;
        }

        var typeCount = reader.ReadInt32();
        var typeClassIds = new List<int>(typeCount);
        for (var i = 0; i < typeCount; i++)
        {
            typeClassIds.Add(ReadSerializedTypeClassId(reader, version, enableTypeTree, isRefType: false));
        }

        var bigIdEnabled = 0;
        if (version >= 7 && version < 14)
        {
            bigIdEnabled = reader.ReadInt32();
        }

        var objectCount = reader.ReadInt32();
        for (var i = 0; i < objectCount; i++)
        {
            var pathId = ReadPathId(reader, version, bigIdEnabled);
            var byteStart = version >= 22 ? reader.ReadInt64() : reader.ReadUInt32();
            byteStart += dataOffset;
            var byteSize = reader.ReadUInt32();
            var typeId = reader.ReadInt32();
            int? classId = null;
            if (version < 16)
            {
                classId = reader.ReadUInt16();
            }
            else if (typeId >= 0 && typeId < typeClassIds.Count)
            {
                classId = typeClassIds[typeId];
            }
            if (version < 11)
            {
                reader.ReadUInt16();
            }
            if (version >= 11 && version < 17)
            {
                reader.ReadInt16();
            }
            if (version == 15 || version == 16)
            {
                reader.ReadByte();
            }

            info.Objects.Add(new SerializedObjectInfo(pathId, byteStart, byteSize, typeId, classId));
            context.SemanticObjects.Add(new SemanticObjectInfo(pathId, classId, ResolveTypeName(classId)));
        }

        if (version >= 11)
        {
            var scriptCount = reader.ReadInt32();
            for (var i = 0; i < scriptCount; i++)
            {
                reader.ReadInt32();
                if (version < 14)
                {
                    reader.ReadInt32();
                }
                else
                {
                    reader.Align(4);
                    reader.ReadInt64();
                }
            }
        }

        var externalsCount = reader.ReadInt32();
        for (var i = 0; i < externalsCount; i++)
        {
            if (version >= 6)
            {
                reader.ReadStringToNull();
            }
            if (version >= 5)
            {
                reader.ReadBytes(16);
                reader.ReadInt32();
            }
            reader.ReadStringToNull();
        }

        if (version >= 20)
        {
            var refTypeCount = reader.ReadInt32();
            for (var i = 0; i < refTypeCount; i++)
            {
                SkipSerializedType(reader, version, enableTypeTree, isRefType: true);
            }
        }

        if (version >= 5)
        {
            reader.ReadStringToNull();
        }

        PopulateHierarchySemantics(data, info, context);
        PopulateRenderLinkSemantics(data, info, context, externalResourceLookup);

        context.SerializedFiles.Add(info);
        context.Containers.Add(new ParsedContainer(sourceName, ContainerKind.SerializedFile, data.Length)
        {
            Version = version
        });
    }

    private static bool TryReadSerializedHeader(byte[] data, bool isBigEndian, out SerializedHeader header)
    {
        header = default;
        if (data.Length < 20)
        {
            return false;
        }

        var reader = new EndianBinaryReader(data)
        {
            IsBigEndian = isBigEndian
        };

        var metadataSize = reader.ReadUInt32();
        var fileSize = (long)reader.ReadUInt32();
        var version = reader.ReadUInt32();
        var dataOffset = (long)reader.ReadUInt32();
        var endianFlag = (byte)0;

        if (version >= 9)
        {
            if (reader.Position + 4 > data.Length)
            {
                return false;
            }

            endianFlag = reader.ReadByte();
            reader.ReadBytes(3);
        }

        if (version >= 22)
        {
            if (reader.Position + 24 > data.Length)
            {
                return false;
            }

            metadataSize = reader.ReadUInt32();
            fileSize = reader.ReadInt64();
            dataOffset = reader.ReadInt64();
            reader.ReadInt64();
        }

        if (version == 0 || version > 1000)
        {
            return false;
        }

        if (fileSize <= 0 || fileSize > data.Length)
        {
            return false;
        }

        if (dataOffset < 0 || dataOffset > fileSize)
        {
            return false;
        }

        if (metadataSize > fileSize)
        {
            return false;
        }

        header = new SerializedHeader(metadataSize, fileSize, version, dataOffset, isBigEndian, endianFlag, reader.Position);
        return true;
    }

    private static long ReadPathId(EndianBinaryReader reader, uint version, int bigIdEnabled)
    {
        if (bigIdEnabled != 0)
        {
            return reader.ReadInt64();
        }
        if (version < 14)
        {
            return reader.ReadInt32();
        }
        reader.Align(4);
        return reader.ReadInt64();
    }

    private static void SkipSerializedType(EndianBinaryReader reader, uint version, bool enableTypeTree, bool isRefType)
    {
        _ = ReadSerializedTypeClassId(reader, version, enableTypeTree, isRefType);
    }

    private static int ReadSerializedTypeClassId(EndianBinaryReader reader, uint version, bool enableTypeTree, bool isRefType)
    {
        var classId = reader.ReadInt32();

        if (version >= 16)
        {
            reader.ReadByte();
        }
        var scriptTypeIndex = -1;
        if (version >= 17)
        {
            scriptTypeIndex = reader.ReadInt16();
        }

        if (version >= 13)
        {
            var needsScriptId = isRefType
                ? scriptTypeIndex >= 0
                : (version < 16 && classId < 0) || (version >= 16 && classId == 114);

            if (needsScriptId)
            {
                reader.ReadBytes(16);
            }
            reader.ReadBytes(16);
        }

        if (enableTypeTree)
        {
            if (version >= 12 || version == 10)
            {
                SkipTypeTreeBlob(reader, version);
            }
        }

        if (version >= 21 && !isRefType)
        {
            var dependencyCount = reader.ReadInt32();
            for (var i = 0; i < dependencyCount; i++)
            {
                reader.ReadInt32();
            }
        }

        if (version >= 21 && isRefType)
        {
            reader.ReadStringToNull();
            reader.ReadStringToNull();
            reader.ReadStringToNull();
        }

        return classId;
    }

    private static void SkipTypeTreeBlob(EndianBinaryReader reader, uint version)
    {
        var nodeCount = reader.ReadInt32();
        var stringBufferSize = reader.ReadInt32();
        for (var i = 0; i < nodeCount; i++)
        {
            reader.ReadUInt16();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            if (version >= 19)
            {
                reader.ReadInt64();
            }
        }
        reader.ReadBytes(stringBufferSize);
    }

    private static int AlignPosition(int value, int alignment)
    {
        if (alignment <= 1)
        {
            return value;
        }
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    private static byte[]? ReadBlockInfoBytes(
        byte[] data,
        int offset,
        int compressedSize,
        int uncompressedSize,
        int compression,
        BaseAssetsContext context)
    {
        if (compressedSize < 0 || uncompressedSize < 0)
        {
            context.Warnings.Add("UnityFS block info sizes are invalid.");
            return null;
        }

        if (offset < 0 || offset + compressedSize > data.Length)
        {
            context.Warnings.Add("UnityFS block info segment is out of range.");
            return null;
        }

        var span = data.AsSpan(offset, compressedSize);
        switch (compression)
        {
            case 0:
                return span.ToArray();
            case 2:
            case 3:
                return Lz4Decoder.DecodeBlock(span, uncompressedSize);
            case 1:
                return TryDecompressLzma(span, uncompressedSize, context);
            default:
                context.Warnings.Add($"UnityFS block info compression {compression} is unsupported.");
                return null;
        }
    }

    private static byte[]? TryDecompressLzma(ReadOnlySpan<byte> compressed, int uncompressedSize, BaseAssetsContext context)
    {
        if (uncompressedSize < 0)
        {
            context.Warnings.Add("UnityFS LZMA uncompressed size is invalid.");
            return null;
        }

        if (compressed.Length < 5)
        {
            context.Warnings.Add("UnityFS LZMA payload is too short.");
            return null;
        }

        try
        {
            using var input = new MemoryStream(compressed.ToArray(), writable: false);
            using var output = new MemoryStream(uncompressedSize);

            var properties = new byte[5];
            if (input.Read(properties, 0, properties.Length) != properties.Length)
            {
                context.Warnings.Add("UnityFS LZMA properties are invalid.");
                return null;
            }

            var decoder = new SevenZip.Compression.LZMA.Decoder();
            decoder.SetDecoderProperties(properties);
            decoder.Code(input, output, input.Length - input.Position, uncompressedSize, null);

            var decoded = output.ToArray();
            if (decoded.Length != uncompressedSize)
            {
                context.Warnings.Add("UnityFS LZMA decompressed size mismatch.");
                return null;
            }

            return decoded;
        }
        catch (Exception)
        {
            context.Warnings.Add("UnityFS LZMA decompression failed.");
            return null;
        }
    }

    private static (List<BlockInfo> Blocks, List<NodeInfo> Nodes) ParseBlockInfo(byte[] blockInfoBytes)
    {
        var reader = new EndianBinaryReader(blockInfoBytes);
        reader.IsBigEndian = true;
        reader.ReadBytes(16);
        var blockCount = reader.ReadInt32();
        var blocks = new List<BlockInfo>(blockCount);
        for (var i = 0; i < blockCount; i++)
        {
            var uncompressedSize = reader.ReadUInt32();
            var compressedSize = reader.ReadUInt32();
            var flags = reader.ReadUInt16();
            blocks.Add(new BlockInfo(uncompressedSize, compressedSize, flags));
        }

        var nodeCount = reader.ReadInt32();
        var nodes = new List<NodeInfo>(nodeCount);
        for (var i = 0; i < nodeCount; i++)
        {
            var offset = reader.ReadInt64();
            var size = reader.ReadInt64();
            var flags = reader.ReadUInt32();
            var path = reader.ReadStringToNull();
            nodes.Add(new NodeInfo(path, offset, size, flags));
        }

        return (blocks, nodes);
    }

    private static byte[]? DecompressBlocks(
        byte[] data,
        int dataOffset,
        int dataLength,
        List<BlockInfo> blocks,
        BaseAssetsContext context)
    {
        long totalUncompressed = 0;
        long totalCompressed = 0;
        foreach (var block in blocks)
        {
            totalUncompressed += block.UncompressedSize;
            totalCompressed += block.CompressedSize;
        }

        if (totalUncompressed > int.MaxValue)
        {
            context.Warnings.Add("UnityFS uncompressed data size exceeds supported limits.");
            return null;
        }

        if (totalCompressed > dataLength)
        {
            context.Warnings.Add("UnityFS compressed data length exceeds available data.");
            return null;
        }

        var output = new byte[(int)totalUncompressed];
        var inputIndex = dataOffset;
        var outputIndex = 0;

        foreach (var block in blocks)
        {
            if (inputIndex + block.CompressedSize > dataOffset + dataLength)
            {
                context.Warnings.Add("UnityFS block data is out of range.");
                return null;
            }

            var compressedSpan = data.AsSpan(inputIndex, (int)block.CompressedSize);
            var compression = block.Flags & 0x3F;
            byte[]? decompressed;

            switch (compression)
            {
                case 0:
                    decompressed = compressedSpan.ToArray();
                    break;
                case 2:
                case 3:
                    decompressed = Lz4Decoder.DecodeBlock(compressedSpan, (int)block.UncompressedSize);
                    break;
                case 1:
                    decompressed = TryDecompressLzma(compressedSpan, (int)block.UncompressedSize, context);
                    if (decompressed is null)
                    {
                        return null;
                    }
                    break;
                default:
                    context.Warnings.Add($"UnityFS block compression {compression} is unsupported.");
                    return null;
            }

            if (decompressed.Length != block.UncompressedSize)
            {
                context.Warnings.Add("UnityFS block decompressed size mismatch.");
                return null;
            }

            Buffer.BlockCopy(decompressed, 0, output, outputIndex, decompressed.Length);
            inputIndex += (int)block.CompressedSize;
            outputIndex += decompressed.Length;
        }

        return output;
    }

    private static bool TrySlicePayload(byte[] data, long offset, long size, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (offset < 0 || size < 0 || offset > int.MaxValue || size > int.MaxValue)
        {
            return false;
        }

        var end = offset + size;
        if (end > data.Length)
        {
            return false;
        }

        payload = new byte[size];
        Buffer.BlockCopy(data, (int)offset, payload, 0, (int)size);
        return true;
    }

    private static bool TryParseSerializedPayload(
        byte[] payload,
        int serializedAssetIndex,
        BaseAssetsContext context,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup = null)
    {
        var serializedBefore = context.SerializedFiles.Count;
        var containersBefore = context.Containers.Count;
        var semanticObjectsBefore = context.SemanticObjects.Count;
        var semanticGameObjectsBefore = context.SemanticGameObjects.Count;
        var semanticTransformsBefore = context.SemanticTransforms.Count;
        var semanticMeshFiltersBefore = context.SemanticMeshFilters.Count;
        var semanticMeshRenderersBefore = context.SemanticMeshRenderers.Count;
        var semanticMeshesBefore = context.SemanticMeshes.Count;
        var semanticMaterialsBefore = context.SemanticMaterials.Count;
        var semanticTexturesBefore = context.SemanticTextures.Count;

        try
        {
            var containerSourceName = $"serialized_asset_{serializedAssetIndex}";
            var serializedSourceName = $"asset_{serializedAssetIndex}";
            ParseSerializedFile(payload, containerSourceName, context, serializedSourceName, externalResourceLookup);
        }
        catch
        {
            while (context.SerializedFiles.Count > serializedBefore)
            {
                context.SerializedFiles.RemoveAt(context.SerializedFiles.Count - 1);
            }

            while (context.Containers.Count > containersBefore)
            {
                context.Containers.RemoveAt(context.Containers.Count - 1);
            }

            while (context.SemanticObjects.Count > semanticObjectsBefore)
            {
                context.SemanticObjects.RemoveAt(context.SemanticObjects.Count - 1);
            }

            while (context.SemanticGameObjects.Count > semanticGameObjectsBefore)
            {
                context.SemanticGameObjects.RemoveAt(context.SemanticGameObjects.Count - 1);
            }

            while (context.SemanticTransforms.Count > semanticTransformsBefore)
            {
                context.SemanticTransforms.RemoveAt(context.SemanticTransforms.Count - 1);
            }

            while (context.SemanticMeshFilters.Count > semanticMeshFiltersBefore)
            {
                context.SemanticMeshFilters.RemoveAt(context.SemanticMeshFilters.Count - 1);
            }

            while (context.SemanticMeshRenderers.Count > semanticMeshRenderersBefore)
            {
                context.SemanticMeshRenderers.RemoveAt(context.SemanticMeshRenderers.Count - 1);
            }

            while (context.SemanticMeshes.Count > semanticMeshesBefore)
            {
                context.SemanticMeshes.RemoveAt(context.SemanticMeshes.Count - 1);
            }

            while (context.SemanticMaterials.Count > semanticMaterialsBefore)
            {
                context.SemanticMaterials.RemoveAt(context.SemanticMaterials.Count - 1);
            }

            while (context.SemanticTextures.Count > semanticTexturesBefore)
            {
                context.SemanticTextures.RemoveAt(context.SemanticTextures.Count - 1);
            }

            return false;
        }

        if (context.SerializedFiles.Count == serializedBefore)
        {
            return false;
        }

        if (context.SerializedFiles[^1].Objects.Count == 0)
        {
            context.SerializedFiles.RemoveAt(context.SerializedFiles.Count - 1);

            while (context.Containers.Count > containersBefore)
            {
                context.Containers.RemoveAt(context.Containers.Count - 1);
            }

            while (context.SemanticObjects.Count > semanticObjectsBefore)
            {
                context.SemanticObjects.RemoveAt(context.SemanticObjects.Count - 1);
            }

            while (context.SemanticGameObjects.Count > semanticGameObjectsBefore)
            {
                context.SemanticGameObjects.RemoveAt(context.SemanticGameObjects.Count - 1);
            }

            while (context.SemanticTransforms.Count > semanticTransformsBefore)
            {
                context.SemanticTransforms.RemoveAt(context.SemanticTransforms.Count - 1);
            }

            while (context.SemanticMeshFilters.Count > semanticMeshFiltersBefore)
            {
                context.SemanticMeshFilters.RemoveAt(context.SemanticMeshFilters.Count - 1);
            }

            while (context.SemanticMeshRenderers.Count > semanticMeshRenderersBefore)
            {
                context.SemanticMeshRenderers.RemoveAt(context.SemanticMeshRenderers.Count - 1);
            }

            while (context.SemanticMeshes.Count > semanticMeshesBefore)
            {
                context.SemanticMeshes.RemoveAt(context.SemanticMeshes.Count - 1);
            }

            while (context.SemanticMaterials.Count > semanticMaterialsBefore)
            {
                context.SemanticMaterials.RemoveAt(context.SemanticMaterials.Count - 1);
            }

            while (context.SemanticTextures.Count > semanticTexturesBefore)
            {
                context.SemanticTextures.RemoveAt(context.SemanticTextures.Count - 1);
            }

            return false;
        }

        return true;
    }

    private static string ResolveTypeName(int? classId)
    {
        if (!classId.HasValue)
        {
            return "Unknown";
        }

        return classId.Value switch
        {
            1 => "GameObject",
            4 => "Transform",
            21 => "Material",
            23 => "MeshRenderer",
            28 => "Texture2D",
            33 => "MeshFilter",
            43 => "Mesh",
            48 => "Shader",
            74 => "AnimationClip",
            91 => "AnimatorController",
            95 => "Animator",
            114 => "MonoBehaviour",
            142 => "AssetBundle",
            198 => "ParticleSystem",
            199 => "ParticleSystemRenderer",
            _ => $"Class{classId.Value}"
        };
    }

    private static void PopulateHierarchySemantics(byte[] data, SerializedFileInfo info, BaseAssetsContext context)
    {
        var gameObjectPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 1)
                .ConvertAll(item => item.PathId));

        var transformPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 4)
                .ConvertAll(item => item.PathId));

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 1 && obj.ClassId != 4)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            try
            {
                if (obj.ClassId == 1)
                {
                    var gameObject = TryReadGameObject(payload, obj.PathId, info.Version, info.BigEndian);
                    if (gameObject is not null)
                    {
                        context.SemanticGameObjects.Add(gameObject);
                    }
                }
                else if (obj.ClassId == 4)
                {
                    var transform = TryReadTransform(payload, obj.PathId, info.Version, info.BigEndian, gameObjectPathIds, transformPathIds);
                    if (transform is not null)
                    {
                        context.SemanticTransforms.Add(transform);
                    }
                }
            }
            catch
            {
            }
        }
    }

    private static Dictionary<string, byte[]> BuildExternalResourceLookup(IReadOnlyList<ContainerEntry> entries)
    {
        var lookup = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.Payload is null || entry.Payload.Length == 0)
            {
                continue;
            }

            AddExternalResourceLookupKey(lookup, entry.Path, entry.Payload);

            var normalizedPath = entry.Path.Replace('\\', '/').Trim();
            var fileName = Path.GetFileName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                AddExternalResourceLookupKey(lookup, fileName, entry.Payload);
            }
        }

        return lookup;
    }

    private static void AddExternalResourceLookupKey(Dictionary<string, byte[]> lookup, string rawKey, byte[] payload)
    {
        var normalized = NormalizeResourceLookupKey(rawKey);
        if (!string.IsNullOrWhiteSpace(normalized) && !lookup.ContainsKey(normalized))
        {
            lookup[normalized] = payload;
        }
    }

    private static string NormalizeResourceLookupKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim();
        const string archivePrefix = "archive:/";
        if (normalized.StartsWith(archivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(archivePrefix.Length);
        }

        return normalized.TrimStart('/');
    }

    private static bool TryResolveExternalResource(
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup,
        string resourcePath,
        out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (externalResourceLookup is null || externalResourceLookup.Count == 0 || string.IsNullOrWhiteSpace(resourcePath))
        {
            return false;
        }

        var normalized = NormalizeResourceLookupKey(resourcePath);
        if (externalResourceLookup.TryGetValue(normalized, out payload))
        {
            return true;
        }

        var fileName = Path.GetFileName(normalized);
        if (!string.IsNullOrWhiteSpace(fileName)
            && externalResourceLookup.TryGetValue(NormalizeResourceLookupKey(fileName), out payload))
        {
            return true;
        }

        foreach (var pair in externalResourceLookup)
        {
            if (normalized.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(fileName)
                    && pair.Key.EndsWith('/' + fileName, StringComparison.OrdinalIgnoreCase)))
            {
                payload = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static void PopulateRenderLinkSemantics(
        byte[] data,
        SerializedFileInfo info,
        BaseAssetsContext context,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup)
    {
        var gameObjectPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 1)
                .ConvertAll(item => item.PathId));

        var meshPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 43)
                .ConvertAll(item => item.PathId));

        var materialPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 21)
                .ConvertAll(item => item.PathId));

        var shaderPathIds = new HashSet<long>(
            info.Objects
                .FindAll(item => item.ClassId == 48)
                .ConvertAll(item => item.PathId));

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 33)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            var meshFilter = TryReadMeshFilter(payload, obj.PathId, info.Version, info.BigEndian, gameObjectPathIds, meshPathIds);
            if (meshFilter is not null)
            {
                context.SemanticMeshFilters.Add(meshFilter);
            }

            continue;
        }

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 23)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            var meshRenderer = TryReadMeshRenderer(payload, obj.PathId, info.Version, info.BigEndian, gameObjectPathIds, materialPathIds);
            if (meshRenderer is not null)
            {
                context.SemanticMeshRenderers.Add(meshRenderer);
            }
        }

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 43)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            var mesh = TryReadMesh(payload, obj.PathId, info.Version, info.BigEndian, externalResourceLookup);
            if (mesh is not null)
            {
                context.SemanticMeshes.Add(mesh);
            }
        }

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 21)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            var material = TryReadMaterial(payload, obj.PathId, info.Version, info.BigEndian, shaderPathIds);
            if (material is not null)
            {
                context.SemanticMaterials.Add(material);
            }
        }

        foreach (var obj in info.Objects)
        {
            if (obj.ClassId != 28)
            {
                continue;
            }

            if (!TrySlicePayload(data, obj.ByteStart, obj.ByteSize, out var payload))
            {
                continue;
            }

            var texture = TryReadTexture(payload, obj.PathId, info.Version, info.BigEndian);
            if (texture is not null)
            {
                context.SemanticTextures.Add(texture);
            }
        }
    }

    private static SemanticMeshFilterInfo? TryReadMeshFilter(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        HashSet<long> gameObjectPathIds,
        HashSet<long> meshPathIds)
    {
        if (TryReadMeshFilterPayload(payload, pathId, version, isBigEndian, 0, gameObjectPathIds, meshPathIds, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadMeshFilterPayload(payload, pathId, version, isBigEndian, objectPrefixSize, gameObjectPathIds, meshPathIds, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadMeshFilterPayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        HashSet<long> gameObjectPathIds,
        HashSet<long> meshPathIds,
        out SemanticMeshFilterInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var gameObjectPathId = ReadPPtrPathId(reader, version);
            var meshPathId = ReadPPtrPathId(reader, version);

            if (!gameObjectPathIds.Contains(gameObjectPathId) || !meshPathIds.Contains(meshPathId))
            {
                return false;
            }

            candidate = new SemanticMeshFilterInfo(pathId, gameObjectPathId, meshPathId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SemanticMeshRendererInfo? TryReadMeshRenderer(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        HashSet<long> gameObjectPathIds,
        HashSet<long> materialPathIds)
    {
        if (TryReadMeshRendererPayload(payload, pathId, version, isBigEndian, 0, gameObjectPathIds, materialPathIds, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadMeshRendererPayload(payload, pathId, version, isBigEndian, objectPrefixSize, gameObjectPathIds, materialPathIds, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadMeshRendererPayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        HashSet<long> gameObjectPathIds,
        HashSet<long> materialPathIds,
        out SemanticMeshRendererInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var gameObjectPathId = ReadPPtrPathId(reader, version);
            if (!gameObjectPathIds.Contains(gameObjectPathId))
            {
                return false;
            }

            if (!TryReadRendererMaterials(payload, version, isBigEndian, startOffset, materialPathIds, out var materials))
            {
                return false;
            }

            candidate = new SemanticMeshRendererInfo(pathId, gameObjectPathId, materials);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadRendererMaterials(
        byte[] payload,
        uint version,
        bool isBigEndian,
        int startOffset,
        HashSet<long> materialPathIds,
        out List<long> materials)
    {
        materials = new List<long>();

        var pptrSize = version >= 5 ? 12 : 8;
        var searchStart = AlignPosition(startOffset + pptrSize, 4);
        var searchEnd = Math.Min(payload.Length - 4, searchStart + 256);
        if (searchStart > searchEnd)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        for (var offset = searchStart; offset <= searchEnd; offset += 4)
        {
            reader.Position = offset;
            var materialCount = reader.ReadInt32();
            if (materialCount <= 0 || materialCount > 64)
            {
                continue;
            }

            var bytesRequired = 4 + (materialCount * pptrSize);
            if (offset + bytesRequired > payload.Length)
            {
                continue;
            }

            var candidateMaterials = new List<long>(materialCount);
            var isValid = true;

            reader.Position = offset + 4;
            for (var i = 0; i < materialCount; i++)
            {
                var materialPathId = ReadPPtrPathId(reader, version);
                if (!materialPathIds.Contains(materialPathId))
                {
                    isValid = false;
                    break;
                }

                candidateMaterials.Add(materialPathId);
            }

            if (!isValid)
            {
                continue;
            }

            materials = candidateMaterials;
            return true;
        }

        return false;
    }

    private static SemanticMaterialInfo? TryReadMaterial(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        HashSet<long> shaderPathIds)
    {
        if (TryReadMaterialPayload(payload, pathId, version, isBigEndian, 0, shaderPathIds, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadMaterialPayload(payload, pathId, version, isBigEndian, objectPrefixSize, shaderPathIds, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadMaterialPayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        HashSet<long> shaderPathIds,
        out SemanticMaterialInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var name = ReadAlignedString(reader);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var shaderPathIdRaw = ReadPPtrPathId(reader, version);
            long? shaderPathId = shaderPathIdRaw == 0 ? null : shaderPathIdRaw;

            if (shaderPathId.HasValue && !shaderPathIds.Contains(shaderPathId.Value))
            {
                return false;
            }

            // Try to extract color and PBR properties from the binary payload
            var baseColorFactor = new[] { 1f, 1f, 1f, 1f };
            var metallic = 0f;
            var roughness = 0.5f;

            try
            {
                // Skip past the parent reference (PPtr)
                _ = ReadPPtrPathId(reader, version);
                
                // Try to read color properties if available
                // The format varies, but commonly: m_SavedProperties contains shader properties
                // We'll attempt to find _Color, _Metallic, _Smoothness if they exist
                var startPos = reader.Position;
                var maxBytes = Math.Min(payload.Length - (int)startPos, 512); // Read next 512 bytes for properties
                
                // Look for color data pattern (4 floats for RGBA)
                if (maxBytes >= 16)
                {
                    // Try reading a color field (common pattern: r, g, b, a as floats)
                    var testR = reader.ReadSingle();
                    var testG = reader.ReadSingle();
                    var testB = reader.ReadSingle();
                    var testA = reader.ReadSingle();
                    
                    // Validate it looks like a color (values between 0-1 typically)
                    if (testR >= 0 && testR <= 2f && testG >= 0 && testG <= 2f && 
                        testB >= 0 && testB <= 2f && testA >= 0 && testA <= 2f)
                    {
                        baseColorFactor = new[] { testR, testG, testB, testA };
                        System.Console.WriteLine($"[SkeletonParser] Material {name} (pathId={pathId}): Extracted baseColor=[{testR},{testG},{testB},{testA}]");
                    }
                    else
                    {
                        System.Console.WriteLine($"[SkeletonParser] Material {name} (pathId={pathId}): Color validation failed [{testR},{testG},{testB},{testA}], using default white");
                    }
                }
            }
            catch (Exception ex)
            {
                // If color extraction fails, fall back to defaults
                System.Console.WriteLine($"[SkeletonParser] Material {name} (pathId={pathId}): Failed to extract color - {ex.Message}");
            }

            candidate = new SemanticMaterialInfo(pathId, name, shaderPathId, baseColorFactor, metallic, roughness);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SemanticMeshInfo? TryReadMesh(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup)
    {
        if (TryReadMeshPayload(payload, pathId, version, isBigEndian, 0, externalResourceLookup, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadMeshPayload(payload, pathId, version, isBigEndian, objectPrefixSize, externalResourceLookup, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadMeshPayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup,
        out SemanticMeshInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var name = ReadAlignedString(reader);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var subMeshCount = reader.ReadInt32();
            if (subMeshCount <= 0 || subMeshCount > 1024)
            {
                return false;
            }

            var topology = new List<int>(subMeshCount);
            var subMeshes = new List<SemanticSubMeshInfo>(subMeshCount);
            long indexElementCountTotal = 0;
            long vertexCountMax = 0;

            float minX = 0;
            float minY = 0;
            float minZ = 0;
            float maxX = 0;
            float maxY = 0;
            float maxZ = 0;

            for (var i = 0; i < subMeshCount; i++)
            {
                var firstByte = reader.ReadUInt32();
                var indexCount = reader.ReadUInt32();
                var topologyValue = reader.ReadInt32();
                _ = reader.ReadUInt32();
                var firstVertex = reader.ReadUInt32();
                var vertexCount = reader.ReadUInt32();

                var centerX = reader.ReadSingle();
                var centerY = reader.ReadSingle();
                var centerZ = reader.ReadSingle();
                var extentX = reader.ReadSingle();
                var extentY = reader.ReadSingle();
                var extentZ = reader.ReadSingle();

                if (indexCount > int.MaxValue || firstVertex > int.MaxValue || vertexCount > int.MaxValue)
                {
                    return false;
                }

                if (firstByte > int.MaxValue)
                {
                    return false;
                }

                if (topologyValue < 0 || topologyValue > 10)
                {
                    return false;
                }

                topology.Add(topologyValue);
                indexElementCountTotal += indexCount;
                vertexCountMax = Math.Max(vertexCountMax, firstVertex + vertexCount);
                subMeshes.Add(new SemanticSubMeshInfo(
                    (int)firstByte,
                    (int)indexCount,
                    topologyValue,
                    (int)firstVertex,
                    (int)vertexCount));

                var aabbMinX = centerX - extentX;
                var aabbMinY = centerY - extentY;
                var aabbMinZ = centerZ - extentZ;
                var aabbMaxX = centerX + extentX;
                var aabbMaxY = centerY + extentY;
                var aabbMaxZ = centerZ + extentZ;

                if (i == 0)
                {
                    minX = aabbMinX;
                    minY = aabbMinY;
                    minZ = aabbMinZ;
                    maxX = aabbMaxX;
                    maxY = aabbMaxY;
                    maxZ = aabbMaxZ;
                }
                else
                {
                    minX = Math.Min(minX, aabbMinX);
                    minY = Math.Min(minY, aabbMinY);
                    minZ = Math.Min(minZ, aabbMinZ);
                    maxX = Math.Max(maxX, aabbMaxX);
                    maxY = Math.Max(maxY, aabbMaxY);
                    maxZ = Math.Max(maxZ, aabbMaxZ);
                }
            }

            var indexElementSize = vertexCountMax > ushort.MaxValue ? 4 : 2;
            var indexCountBytes = indexElementCountTotal * indexElementSize;

            if (indexCountBytes > int.MaxValue || vertexCountMax > int.MaxValue)
            {
                return false;
            }

            int? indexFormat = null;
            IReadOnlyList<uint> decodedIndices = Array.Empty<uint>();
            var indexBufferEndOffset = -1;
            if (TryReadMeshIndexBuffer(
                payload,
                isBigEndian,
                reader.Position,
                (int)indexCountBytes,
                (int)indexElementCountTotal,
                out var parsedIndexFormat,
                out var parsedIndices,
                out var parsedIndexBufferEndOffset))
            {
                indexFormat = parsedIndexFormat;
                decodedIndices = parsedIndices;
                indexBufferEndOffset = parsedIndexBufferEndOffset;
            }

            if (decodedIndices.Count > 0)
            {
                if (decodedIndices.Count != indexElementCountTotal)
                {
                    return false;
                }

                if (vertexCountMax > 0)
                {
                    var maxIndex = decodedIndices.Max();
                    if (maxIndex >= vertexCountMax)
                    {
                        return false;
                    }
                }

                foreach (var subMesh in subMeshes)
                {
                    var firstIndex = subMesh.FirstByte / indexElementSize;
                    if (firstIndex < 0 || firstIndex + subMesh.IndexCount > decodedIndices.Count)
                    {
                        return false;
                    }
                }
            }

            var vertexDataByteLength = 0;
            IReadOnlyList<SemanticVector3> decodedPositions = Array.Empty<SemanticVector3>();
            IReadOnlyList<SemanticVector3> decodedNormals = Array.Empty<SemanticVector3>();
            IReadOnlyList<SemanticVector4> decodedTangents = Array.Empty<SemanticVector4>();
            IReadOnlyList<SemanticVector4> decodedColors = Array.Empty<SemanticVector4>();
            IReadOnlyList<SemanticVector2> decodedUv0 = Array.Empty<SemanticVector2>();
            IReadOnlyList<SemanticVector2> decodedUv1 = Array.Empty<SemanticVector2>();
            IReadOnlyList<SemanticVertexChannelInfo> vertexChannels = Array.Empty<SemanticVertexChannelInfo>();
            IReadOnlyList<SemanticVertexStreamInfo> vertexStreams = Array.Empty<SemanticVertexStreamInfo>();
            if (TryReadMeshVertexData(
                payload,
                isBigEndian,
                indexBufferEndOffset,
                (int)vertexCountMax,
                externalResourceLookup,
                out var parsedVertexDataByteLength,
                out var parsedPositions,
                out var parsedNormals,
                out var parsedTangents,
                out var parsedColors,
                out var parsedUv0,
                out var parsedUv1,
                out var parsedVertexChannels,
                out var parsedVertexStreams))
            {
                vertexDataByteLength = parsedVertexDataByteLength;
                decodedPositions = parsedPositions;
                decodedNormals = parsedNormals;
                decodedTangents = parsedTangents;
                decodedColors = parsedColors;
                decodedUv0 = parsedUv0;
                decodedUv1 = parsedUv1;
                vertexChannels = parsedVertexChannels;
                vertexStreams = parsedVertexStreams;
            }

            foreach (var subMesh in subMeshes)
            {
                if (subMesh.FirstVertex < 0 || subMesh.VertexCount < 0 || subMesh.FirstVertex + subMesh.VertexCount > vertexCountMax)
                {
                    return false;
                }
            }

            var channelFlags = BuildMeshChannelFlags(vertexChannels, (int)vertexCountMax);

            var boundsCenter = new SemanticVector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                (minZ + maxZ) * 0.5f);

            var boundsExtent = new SemanticVector3(
                (maxX - minX) * 0.5f,
                (maxY - minY) * 0.5f,
                (maxZ - minZ) * 0.5f);

            candidate = new SemanticMeshInfo(
                pathId,
                name,
                new SemanticBoundsInfo(boundsCenter, boundsExtent),
                channelFlags,
                indexFormat,
                decodedIndices,
                vertexDataByteLength,
                decodedPositions,
                decodedNormals,
                decodedTangents,
                decodedColors,
                decodedUv0,
                decodedUv1,
                vertexChannels,
                vertexStreams,
                indexElementSize,
                (int)indexElementCountTotal,
                (int)indexCountBytes,
                subMeshCount,
                subMeshes,
                topology,
                (int)vertexCountMax);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadMeshIndexBuffer(
        byte[] payload,
        bool isBigEndian,
        int searchStartOffset,
        int expectedIndexBytes,
        int expectedIndexElements,
        out int indexFormat,
        out IReadOnlyList<uint> decodedIndices,
        out int indexBufferEndOffset)
    {
        indexFormat = default;
        decodedIndices = Array.Empty<uint>();
        indexBufferEndOffset = -1;

        if (expectedIndexBytes <= 0 || expectedIndexElements <= 0)
        {
            return false;
        }

        var searchStart = AlignPosition(searchStartOffset, 4);
        var searchEnd = Math.Min(payload.Length - 8, searchStart + 4096);
        if (searchStart > searchEnd)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        for (var offset = searchStart; offset <= searchEnd; offset += 4)
        {
            reader.Position = offset;
            var candidateFormat = reader.ReadInt32();
            if (candidateFormat is not 0 and not 1)
            {
                continue;
            }

            var candidateLength = reader.ReadInt32();
            if (candidateLength != expectedIndexBytes)
            {
                continue;
            }

            if (offset + 8 + candidateLength > payload.Length)
            {
                continue;
            }

            var elementSize = candidateFormat == 0 ? 2 : 4;
            if (candidateLength % elementSize != 0)
            {
                continue;
            }

            if ((candidateLength / elementSize) != expectedIndexElements)
            {
                continue;
            }

            reader.Position = offset + 8;
            var indices = new List<uint>(expectedIndexElements);
            for (var i = 0; i < expectedIndexElements; i++)
            {
                indices.Add(candidateFormat == 0 ? reader.ReadUInt16() : reader.ReadUInt32());
            }

            indexFormat = candidateFormat;
            decodedIndices = indices;
            indexBufferEndOffset = offset + 8 + candidateLength;
            return true;
        }

        return false;
    }

    private static bool TryReadMeshVertexData(
        byte[] payload,
        bool isBigEndian,
        int indexBufferEndOffset,
        int vertexCount,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup,
        out int vertexDataByteLength,
        out IReadOnlyList<SemanticVector3> decodedPositions,
        out IReadOnlyList<SemanticVector3> decodedNormals,
        out IReadOnlyList<SemanticVector4> decodedTangents,
        out IReadOnlyList<SemanticVector4> decodedColors,
        out IReadOnlyList<SemanticVector2> decodedUv0,
        out IReadOnlyList<SemanticVector2> decodedUv1,
        out IReadOnlyList<SemanticVertexChannelInfo> vertexChannels,
        out IReadOnlyList<SemanticVertexStreamInfo> vertexStreams)
    {
        vertexDataByteLength = 0;
        decodedPositions = Array.Empty<SemanticVector3>();
        decodedNormals = Array.Empty<SemanticVector3>();
        decodedTangents = Array.Empty<SemanticVector4>();
        decodedColors = Array.Empty<SemanticVector4>();
        decodedUv0 = Array.Empty<SemanticVector2>();
        decodedUv1 = Array.Empty<SemanticVector2>();
        vertexChannels = Array.Empty<SemanticVertexChannelInfo>();
        vertexStreams = Array.Empty<SemanticVertexStreamInfo>();

        if (indexBufferEndOffset < 0 || vertexCount < 0)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        var scanStart = AlignPosition(indexBufferEndOffset, 4);
        var scanEnd = Math.Min(payload.Length - 4, scanStart + 512);
        if (scanStart > scanEnd)
        {
            return false;
        }

        for (var offset = scanStart; offset <= scanEnd; offset += 4)
        {
            reader.Position = offset;
            var candidateLength = reader.ReadInt32();
            if (candidateLength < 0)
            {
                continue;
            }

            if (offset + 4 + candidateLength > payload.Length)
            {
                continue;
            }

            var candidateVertexDataBuffer = payload;
            var candidateVertexDataStart = offset + 4;
            var candidateVertexDataLength = candidateLength;

            if (candidateVertexDataLength == 0
                && vertexCount > 0
                && TryResolveMeshStreamVertexData(payload, isBigEndian, externalResourceLookup, out var streamedVertexData))
            {
                candidateVertexDataBuffer = streamedVertexData;
                candidateVertexDataStart = 0;
                candidateVertexDataLength = streamedVertexData.Length;
            }

            vertexDataByteLength = candidateVertexDataLength;

            if (TryReadMeshVertexChannels(payload, isBigEndian, scanStart, offset, out var parsedChannels))
            {
                vertexChannels = parsedChannels;

                if (TryBuildStreamLayout(
                    vertexChannels,
                    vertexCount,
                    candidateVertexDataLength,
                    out var streamLayout))
                {
                    vertexStreams = streamLayout
                        .OrderBy(item => item.Key)
                        .Select(item => new SemanticVertexStreamInfo(
                            item.Key,
                            item.Value.Offset,
                            item.Value.Stride,
                            item.Value.Stride * vertexCount))
                        .ToArray();
                }
            }

            if (vertexCount > 0
                && vertexChannels.Count > 0
                && TryDecodePositionsFromChannels(candidateVertexDataBuffer, isBigEndian, candidateVertexDataStart, candidateVertexDataLength, vertexCount, vertexChannels, out var channelPositions))
            {
                decodedPositions = channelPositions;

                if (TryDecodeVector3ChannelFromLayout(
                    candidateVertexDataBuffer,
                    isBigEndian,
                    candidateVertexDataStart,
                    candidateVertexDataLength,
                    vertexCount,
                    vertexChannels,
                    channelIndex: 1,
                    out var channelNormals))
                {
                    decodedNormals = channelNormals;
                }

                if (TryDecodeVector4ChannelFromLayout(
                    candidateVertexDataBuffer,
                    isBigEndian,
                    candidateVertexDataStart,
                    candidateVertexDataLength,
                    vertexCount,
                    vertexChannels,
                    channelIndex: 2,
                    out var channelTangents))
                {
                    decodedTangents = channelTangents;
                }

                if (TryDecodeVector4ChannelFromLayout(
                    candidateVertexDataBuffer,
                    isBigEndian,
                    candidateVertexDataStart,
                    candidateVertexDataLength,
                    vertexCount,
                    vertexChannels,
                    channelIndex: 3,
                    out var channelColors))
                {
                    decodedColors = channelColors;
                }

                if (TryDecodeVector2ChannelFromLayout(
                    candidateVertexDataBuffer,
                    isBigEndian,
                    candidateVertexDataStart,
                    candidateVertexDataLength,
                    vertexCount,
                    vertexChannels,
                    channelIndex: 4,
                    out var channelUv0))
                {
                    decodedUv0 = channelUv0;
                }

                if (TryDecodeVector2ChannelFromLayout(
                    candidateVertexDataBuffer,
                    isBigEndian,
                    candidateVertexDataStart,
                    candidateVertexDataLength,
                    vertexCount,
                    vertexChannels,
                    channelIndex: 5,
                    out var channelUv1))
                {
                    decodedUv1 = channelUv1;
                }
            }
            else if (vertexCount > 0 && candidateVertexDataLength == vertexCount * 12)
            {
                var fallbackReader = new EndianBinaryReader(candidateVertexDataBuffer)
                {
                    IsBigEndian = isBigEndian,
                    Position = candidateVertexDataStart
                };

                var positions = new List<SemanticVector3>(vertexCount);
                for (var i = 0; i < vertexCount; i++)
                {
                    positions.Add(new SemanticVector3(fallbackReader.ReadSingle(), fallbackReader.ReadSingle(), fallbackReader.ReadSingle()));
                }

                decodedPositions = positions;
            }

            if (vertexCount > 0
                && decodedPositions.Count == 0
                && candidateVertexDataLength > vertexCount * 12
                && candidateVertexDataLength % vertexCount == 0)
            {
                var inferredStride = candidateVertexDataLength / vertexCount;
                if (inferredStride >= 12)
                {
                    var inferredReader = new EndianBinaryReader(candidateVertexDataBuffer)
                    {
                        IsBigEndian = isBigEndian
                    };

                    var inferred = new List<SemanticVector3>(vertexCount);
                    var allFinite = true;
                    for (var i = 0; i < vertexCount; i++)
                    {
                        inferredReader.Position = candidateVertexDataStart + (i * inferredStride);
                        var x = inferredReader.ReadSingle();
                        var y = inferredReader.ReadSingle();
                        var z = inferredReader.ReadSingle();

                        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)
                            || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
                        {
                            allFinite = false;
                            break;
                        }

                        inferred.Add(new SemanticVector3(x, y, z));
                    }

                    if (allFinite && inferred.Count == vertexCount)
                    {
                        decodedPositions = inferred;
                    }
                }
            }

            if (vertexCount == 0 || decodedPositions.Count > 0)
            {
                return true;
            }

            vertexDataByteLength = 0;
            decodedPositions = Array.Empty<SemanticVector3>();
            decodedNormals = Array.Empty<SemanticVector3>();
            decodedTangents = Array.Empty<SemanticVector4>();
            decodedColors = Array.Empty<SemanticVector4>();
            decodedUv0 = Array.Empty<SemanticVector2>();
            decodedUv1 = Array.Empty<SemanticVector2>();
            vertexChannels = Array.Empty<SemanticVertexChannelInfo>();
            vertexStreams = Array.Empty<SemanticVertexStreamInfo>();
        }

        return false;
    }

    private static bool TryResolveMeshStreamVertexData(
        byte[] meshPayload,
        bool isBigEndian,
        IReadOnlyDictionary<string, byte[]>? externalResourceLookup,
        out byte[] vertexData)
    {
        vertexData = Array.Empty<byte>();

        if (!TryExtractMeshStreamDescriptor(meshPayload, isBigEndian, out var streamPath, out var streamOffset, out var streamSize))
        {
            return false;
        }

        if (!TryResolveExternalResource(externalResourceLookup, streamPath, out var externalPayload))
        {
            return false;
        }

        if (streamOffset < 0 || streamSize <= 0 || streamOffset > externalPayload.Length || streamOffset + streamSize > externalPayload.Length)
        {
            return false;
        }

        vertexData = new byte[streamSize];
        Buffer.BlockCopy(externalPayload, (int)streamOffset, vertexData, 0, streamSize);
        return true;
    }

    private static bool TryExtractMeshStreamDescriptor(
        byte[] meshPayload,
        bool isBigEndian,
        out string streamPath,
        out long streamOffset,
        out int streamSize)
    {
        streamPath = string.Empty;
        streamOffset = 0;
        streamSize = 0;

        var marker = Encoding.ASCII.GetBytes(".resS");
        if (meshPayload.Length < marker.Length + 16)
        {
            return false;
        }

        for (var i = 0; i <= meshPayload.Length - marker.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < marker.Length; j++)
            {
                if (meshPayload[i + j] != marker[j])
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            var stringStart = i;
            while (stringStart > 0 && IsLikelyPathByte(meshPayload[stringStart - 1]))
            {
                stringStart--;
            }

            var stringEnd = i + marker.Length;
            while (stringEnd < meshPayload.Length && IsLikelyPathByte(meshPayload[stringEnd]))
            {
                stringEnd++;
            }

            var stringLength = stringEnd - stringStart;
            if (stringLength <= marker.Length || stringStart < 4)
            {
                continue;
            }

            var encodedLength = isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(meshPayload.AsSpan(stringStart - 4, 4))
                : BinaryPrimitives.ReadInt32LittleEndian(meshPayload.AsSpan(stringStart - 4, 4));

            if (encodedLength != stringLength)
            {
                continue;
            }

            var candidatePath = Encoding.UTF8.GetString(meshPayload, stringStart, stringLength);
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                continue;
            }

            if (TryReadInt64At(meshPayload, stringStart - 16, isBigEndian, out var offset64)
                && TryReadInt32At(meshPayload, stringStart - 8, isBigEndian, out var size32)
                && size32 > 0)
            {
                streamPath = candidatePath;
                streamOffset = offset64;
                streamSize = size32;
                return true;
            }

            if (TryReadInt32At(meshPayload, stringStart - 12, isBigEndian, out var offset32)
                && TryReadInt32At(meshPayload, stringStart - 8, isBigEndian, out size32)
                && offset32 >= 0
                && size32 > 0)
            {
                streamPath = candidatePath;
                streamOffset = offset32;
                streamSize = size32;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadInt32At(byte[] data, int offset, bool isBigEndian, out int value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > data.Length)
        {
            return false;
        }

        value = isBigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4))
            : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        return true;
    }

    private static bool TryReadInt64At(byte[] data, int offset, bool isBigEndian, out long value)
    {
        value = 0;
        if (offset < 0 || offset + 8 > data.Length)
        {
            return false;
        }

        value = isBigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset, 8))
            : BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset, 8));
        return true;
    }

    private static bool IsLikelyPathByte(byte value)
    {
        return value is (>= (byte)'a' and <= (byte)'z')
            or (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'0' and <= (byte)'9')
            or (byte)'/'
            or (byte)'\\'
            or (byte)'-'
            or (byte)'_'
            or (byte)'.'
            or (byte)':';
    }

    private static bool TryDecodeVector4ChannelFromLayout(
        byte[] payload,
        bool isBigEndian,
        int vertexDataStart,
        int vertexDataByteLength,
        int vertexCount,
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int channelIndex,
        out IReadOnlyList<SemanticVector4> decoded)
    {
        decoded = Array.Empty<SemanticVector4>();

        var channel = channels.FirstOrDefault(candidate => candidate.ChannelIndex == channelIndex);
        if (channel is null)
        {
            return false;
        }

        if (!CanDecodeChannelFormat(channel.Format) || channel.Dimension < 3)
        {
            return false;
        }

        if (!TryBuildStreamLayout(channels, vertexCount, vertexDataByteLength, out var streamLayout)
            || !streamLayout.TryGetValue(channel.Stream, out var streamInfo))
        {
            return false;
        }

        if (vertexDataStart < 0 || vertexDataStart + vertexDataByteLength > payload.Length)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        var values = new List<SemanticVector4>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = vertexDataStart + streamInfo.Offset + (i * streamInfo.Stride) + channel.Offset;
            reader.Position = baseOffset;

            if (!TryReadChannelComponent(reader, channel.Format, out var x)
                || !TryReadChannelComponent(reader, channel.Format, out var y)
                || !TryReadChannelComponent(reader, channel.Format, out var z))
            {
                return false;
            }

            var w = 1f;
            if (channel.Dimension >= 4)
            {
                if (!TryReadChannelComponent(reader, channel.Format, out w))
                {
                    return false;
                }
            }

            values.Add(new SemanticVector4(x, y, z, w));
        }

        decoded = values;
        return true;
    }

    private static bool TryDecodeVector3ChannelFromLayout(
        byte[] payload,
        bool isBigEndian,
        int vertexDataStart,
        int vertexDataByteLength,
        int vertexCount,
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int channelIndex,
        out IReadOnlyList<SemanticVector3> decoded)
    {
        decoded = Array.Empty<SemanticVector3>();

        var channel = channels.FirstOrDefault(candidate => candidate.ChannelIndex == channelIndex);
        if (channel is null)
        {
            return false;
        }

        if (!CanDecodeChannelFormat(channel.Format) || channel.Dimension < 3)
        {
            return false;
        }

        if (!TryBuildStreamLayout(channels, vertexCount, vertexDataByteLength, out var streamLayout)
            || !streamLayout.TryGetValue(channel.Stream, out var streamInfo))
        {
            return false;
        }

        if (vertexDataStart < 0 || vertexDataStart + vertexDataByteLength > payload.Length)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        var values = new List<SemanticVector3>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = vertexDataStart + streamInfo.Offset + (i * streamInfo.Stride) + channel.Offset;
            reader.Position = baseOffset;

            if (!TryReadChannelComponent(reader, channel.Format, out var x)
                || !TryReadChannelComponent(reader, channel.Format, out var y)
                || !TryReadChannelComponent(reader, channel.Format, out var z))
            {
                return false;
            }

            values.Add(new SemanticVector3(x, y, z));
        }

        decoded = values;
        return true;
    }

    private static bool TryDecodeVector2ChannelFromLayout(
        byte[] payload,
        bool isBigEndian,
        int vertexDataStart,
        int vertexDataByteLength,
        int vertexCount,
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int channelIndex,
        out IReadOnlyList<SemanticVector2> decoded)
    {
        decoded = Array.Empty<SemanticVector2>();

        var channel = channels.FirstOrDefault(candidate => candidate.ChannelIndex == channelIndex);
        if (channel is null)
        {
            return false;
        }

        if (!CanDecodeChannelFormat(channel.Format) || channel.Dimension < 2)
        {
            return false;
        }

        if (!TryBuildStreamLayout(channels, vertexCount, vertexDataByteLength, out var streamLayout)
            || !streamLayout.TryGetValue(channel.Stream, out var streamInfo))
        {
            return false;
        }

        if (vertexDataStart < 0 || vertexDataStart + vertexDataByteLength > payload.Length)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        var values = new List<SemanticVector2>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = vertexDataStart + streamInfo.Offset + (i * streamInfo.Stride) + channel.Offset;
            reader.Position = baseOffset;

            if (!TryReadChannelComponent(reader, channel.Format, out var x)
                || !TryReadChannelComponent(reader, channel.Format, out var y))
            {
                return false;
            }

            values.Add(new SemanticVector2(x, y));
        }

        decoded = values;
        return true;
    }

    private static bool TryGetStreamStride(
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int stream,
        out int stride)
    {
        stride = 0;

        foreach (var channel in channels.Where(channel => channel.Stream == stream))
        {
            var formatSize = GetVertexFormatElementSize(channel.Format);
            if (formatSize <= 0)
            {
                return false;
            }

            var channelEnd = channel.Offset + (formatSize * channel.Dimension);
            stride = Math.Max(stride, channelEnd);
        }

        return stride > 0;
    }

    private static bool TryBuildStreamLayout(
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int vertexCount,
        int vertexDataByteLength,
        out Dictionary<int, (int Offset, int Stride)> streamLayout)
    {
        streamLayout = new Dictionary<int, (int Offset, int Stride)>();
        if (vertexCount <= 0)
        {
            return false;
        }

        var streamIds = channels
            .Select(channel => channel.Stream)
            .Distinct()
            .OrderBy(stream => stream)
            .ToArray();

        if (streamIds.Length == 0)
        {
            return false;
        }

        if (TryBuildStreamLayoutInternal(channels, streamIds, vertexCount, vertexDataByteLength, alignStreams: true, out streamLayout))
        {
            return true;
        }

        return TryBuildStreamLayoutInternal(channels, streamIds, vertexCount, vertexDataByteLength, alignStreams: false, out streamLayout);
    }

    private static bool TryBuildStreamLayoutInternal(
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        IReadOnlyList<int> streamIds,
        int vertexCount,
        int vertexDataByteLength,
        bool alignStreams,
        out Dictionary<int, (int Offset, int Stride)> streamLayout)
    {
        streamLayout = new Dictionary<int, (int Offset, int Stride)>();
        var runningOffset = 0;

        foreach (var streamId in streamIds)
        {
            if (!TryGetStreamStride(channels, streamId, out var stride))
            {
                return false;
            }

            if (alignStreams)
            {
                runningOffset = AlignPosition(runningOffset, 16);
            }

            var streamByteLength = stride * vertexCount;
            if (streamByteLength < 0)
            {
                return false;
            }

            if (runningOffset + streamByteLength > vertexDataByteLength)
            {
                return false;
            }

            streamLayout[streamId] = (runningOffset, stride);
            runningOffset += streamByteLength;
        }

        return true;
    }

    private static bool TryDecodePositionsFromChannels(
        byte[] payload,
        bool isBigEndian,
        int vertexDataStart,
        int vertexDataByteLength,
        int vertexCount,
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        out IReadOnlyList<SemanticVector3> positions)
    {
        positions = Array.Empty<SemanticVector3>();

        var positionChannel = channels.FirstOrDefault(channel => channel.ChannelIndex == 0 || channel.Offset == 0);
        if (positionChannel is null)
        {
            return false;
        }

        if (positionChannel.Stream != 0 || !CanDecodeChannelFormat(positionChannel.Format) || positionChannel.Dimension < 3)
        {
            return false;
        }

        if (!TryBuildStreamLayout(channels, vertexCount, vertexDataByteLength, out var streamLayout)
            || !streamLayout.TryGetValue(positionChannel.Stream, out var streamInfo))
        {
            return false;
        }

        if (vertexDataStart < 0 || vertexDataStart + vertexDataByteLength > payload.Length)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        var parsed = new List<SemanticVector3>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = vertexDataStart + streamInfo.Offset + (i * streamInfo.Stride) + positionChannel.Offset;
            reader.Position = baseOffset;

            if (!TryReadChannelComponent(reader, positionChannel.Format, out var x)
                || !TryReadChannelComponent(reader, positionChannel.Format, out var y)
                || !TryReadChannelComponent(reader, positionChannel.Format, out var z))
            {
                return false;
            }

            parsed.Add(new SemanticVector3(x, y, z));
        }

        positions = parsed;
        return true;
    }

    private static bool CanDecodeChannelFormat(int format)
    {
        return format is >= 0 and <= 5;
    }

    private static bool TryReadChannelComponent(EndianBinaryReader reader, int format, out float value)
    {
        value = 0;

        switch (format)
        {
            case 0:
                value = reader.ReadSingle();
                return true;
            case 1:
                value = (float)BitConverter.UInt16BitsToHalf(reader.ReadUInt16());
                return true;
            case 2:
                value = reader.ReadByte() / 255f;
                return true;
            case 3:
            {
                var signed = unchecked((sbyte)reader.ReadByte());
                value = Math.Max(signed / 127f, -1f);
                return true;
            }
            case 4:
                value = reader.ReadUInt16() / 65535f;
                return true;
            case 5:
            {
                var signed = reader.ReadInt16();
                value = Math.Max(signed / 32767f, -1f);
                return true;
            }
            default:
                return false;
        }
    }

    private static int GetVertexFormatElementSize(int format)
    {
        return format switch
        {
            0 => 4,
            1 => 2,
            2 => 1,
            3 => 1,
            4 => 2,
            5 => 2,
            6 => 1,
            7 => 1,
            8 => 2,
            9 => 2,
            10 => 4,
            11 => 4,
            _ => -1
        };
    }

    private static SemanticMeshChannelFlags BuildMeshChannelFlags(
        IReadOnlyList<SemanticVertexChannelInfo> channels,
        int vertexCount)
    {
        var channelIds = channels
            .Select(channel => channel.ChannelIndex)
            .ToHashSet();

        var positions = vertexCount > 0 || channelIds.Contains(0);
        var normals = channelIds.Contains(1);
        var tangents = channelIds.Contains(2);
        var colors = channelIds.Contains(3);
        var uv0 = channelIds.Contains(4);
        var uv1 = channelIds.Contains(5);

        return new SemanticMeshChannelFlags(positions, normals, tangents, colors, uv0, uv1);
    }

    private static bool TryReadMeshVertexChannels(
        byte[] payload,
        bool isBigEndian,
        int minOffset,
        int maxOffset,
        out IReadOnlyList<SemanticVertexChannelInfo> channels)
    {
        channels = Array.Empty<SemanticVertexChannelInfo>();
        if (minOffset > maxOffset)
        {
            return false;
        }

        var searchStart = AlignPosition(Math.Max(0, minOffset - 256), 4);
        var searchEnd = Math.Min(payload.Length - 4, maxOffset);
        if (searchStart > searchEnd)
        {
            return false;
        }

        var reader = new EndianBinaryReader(payload)
        {
            IsBigEndian = isBigEndian
        };

        for (var offset = searchStart; offset <= searchEnd; offset += 4)
        {
            reader.Position = offset;
            var count = reader.ReadInt32();
            if (count <= 0 || count > 16)
            {
                continue;
            }

            var bytesRequired = 4 + (count * 4);
            if (offset + bytesRequired > payload.Length)
            {
                continue;
            }

            var parsed = new List<SemanticVertexChannelInfo>(count);
            var hasLikelyPositionChannel = false;
            var valid = true;

            for (var i = 0; i < count; i++)
            {
                var baseOffset = offset + 4 + (i * 4);
                var stream = payload[baseOffset];
                var channelOffset = payload[baseOffset + 1];
                var format = payload[baseOffset + 2];
                var rawDimension = payload[baseOffset + 3];
                var dimension = rawDimension & 0x0F;

                if (stream > 8 || format > 20 || dimension == 0 || dimension > 4)
                {
                    valid = false;
                    break;
                }

                if (i == 0 && stream == 0 && channelOffset == 0 && dimension >= 3)
                {
                    hasLikelyPositionChannel = true;
                }

                parsed.Add(new SemanticVertexChannelInfo(i, stream, channelOffset, format, dimension));
            }

            if (!valid || !hasLikelyPositionChannel)
            {
                continue;
            }

            channels = parsed;
            return true;
        }

        return false;
    }

    private static SemanticTextureInfo? TryReadTexture(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian)
    {
        if (TryReadTexturePayload(payload, pathId, version, isBigEndian, 0, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadTexturePayload(payload, pathId, version, isBigEndian, objectPrefixSize, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadTexturePayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        out SemanticTextureInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var name = ReadAlignedString(reader);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            _ = reader.ReadInt32();

            _ = reader.ReadByte();
            _ = reader.ReadByte();
            reader.Align(4);

            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            var format = reader.ReadInt32();
            var mipCount = reader.ReadInt32();

            if (width < 0 || height < 0 || format < 0 || mipCount < 0)
            {
                return false;
            }

            candidate = new SemanticTextureInfo(pathId, name, width, height, format, mipCount);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SemanticGameObjectInfo? TryReadGameObject(byte[] payload, long pathId, uint version, bool isBigEndian)
    {
        if (TryReadGameObjectPayload(payload, pathId, version, isBigEndian, 0, preferInt64PathId: true, out var candidate)
            || TryReadGameObjectPayload(payload, pathId, version, isBigEndian, 0, preferInt64PathId: false, out candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && (TryReadGameObjectPayload(payload, pathId, version, isBigEndian, objectPrefixSize, preferInt64PathId: true, out candidate)
                || TryReadGameObjectPayload(payload, pathId, version, isBigEndian, objectPrefixSize, preferInt64PathId: false, out candidate)))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadGameObjectPayload(byte[] payload, long pathId, uint version, bool isBigEndian, int startOffset, bool preferInt64PathId, out SemanticGameObjectInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var componentCount = reader.ReadInt32();
            if (componentCount < 0 || componentCount > 1024)
            {
                return false;
            }

            for (var i = 0; i < componentCount; i++)
            {
                reader.ReadInt32();
                _ = ReadPPtrPathId(reader, version, forceInt32PathId: !preferInt64PathId);
            }

            var layer = reader.ReadInt32();
            var name = ReadAlignedString(reader);

            if (layer is < 0 or > 31)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (reader.Position + 3 > reader.Length)
            {
                return false;
            }

            _ = reader.ReadUInt16();
            var isActive = reader.ReadByte() != 0;

            if (reader.Position > reader.Length)
            {
                return false;
            }

            candidate = new SemanticGameObjectInfo(pathId, name, isActive, layer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SemanticTransformInfo? TryReadTransform(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        HashSet<long> gameObjectPathIds,
        HashSet<long> transformPathIds)
    {
        if (TryReadTransformPayload(payload, pathId, version, isBigEndian, 0, gameObjectPathIds, transformPathIds, out var candidate))
        {
            return candidate;
        }

        var objectPrefixSize = GetObjectPrefixSize(version);
        if (objectPrefixSize > 0
            && objectPrefixSize < payload.Length
            && TryReadTransformPayload(payload, pathId, version, isBigEndian, objectPrefixSize, gameObjectPathIds, transformPathIds, out candidate))
        {
            return candidate;
        }

        return null;
    }

    private static bool TryReadTransformPayload(
        byte[] payload,
        long pathId,
        uint version,
        bool isBigEndian,
        int startOffset,
        HashSet<long> gameObjectPathIds,
        HashSet<long> transformPathIds,
        out SemanticTransformInfo candidate)
    {
        candidate = default!;

        try
        {
            var reader = new EndianBinaryReader(payload)
            {
                IsBigEndian = isBigEndian,
                Position = startOffset
            };

            var gameObjectPathId = ReadPPtrPathId(reader, version);

            var rotationX = reader.ReadSingle();
            var rotationY = reader.ReadSingle();
            var rotationZ = reader.ReadSingle();
            var rotationW = reader.ReadSingle();

            var positionX = reader.ReadSingle();
            var positionY = reader.ReadSingle();
            var positionZ = reader.ReadSingle();

            var scaleX = reader.ReadSingle();
            var scaleY = reader.ReadSingle();
            var scaleZ = reader.ReadSingle();

            var childCount = reader.ReadInt32();
            if (childCount < 0 || childCount > 1024)
            {
                return false;
            }

            var children = new List<long>(childCount);
            for (var i = 0; i < childCount; i++)
            {
                children.Add(ReadPPtrPathId(reader, version));
            }

            var parentPathId = ReadPPtrPathId(reader, version);

            if (!gameObjectPathIds.Contains(gameObjectPathId))
            {
                return false;
            }

            if (parentPathId != 0 && !transformPathIds.Contains(parentPathId))
            {
                return false;
            }

            if (!children.TrueForAll(child => transformPathIds.Contains(child)))
            {
                return false;
            }

            candidate = new SemanticTransformInfo(
                pathId,
                gameObjectPathId,
                parentPathId == 0 ? null : parentPathId,
                children,
                new SemanticVector3(positionX, positionY, positionZ),
                new SemanticQuaternion(rotationW, rotationX, rotationY, rotationZ),
                new SemanticVector3(scaleX, scaleY, scaleZ));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetObjectPrefixSize(uint version)
    {
        return 4 + (3 * (version >= 14 ? 12 : 8));
    }

    private static long ReadPPtrPathId(EndianBinaryReader reader, uint version)
    {
        return ReadPPtrPathId(reader, version, forceInt32PathId: false);
    }

    private static long ReadPPtrPathId(EndianBinaryReader reader, uint version, bool forceInt32PathId)
    {
        _ = reader.ReadInt32();
        if (forceInt32PathId)
        {
            return reader.ReadInt32();
        }

        return version >= 5 ? reader.ReadInt64() : reader.ReadInt32();
    }

    private static string ReadAlignedString(EndianBinaryReader reader)
    {
        var byteCount = reader.ReadInt32();
        if (byteCount <= 0)
        {
            reader.Align(4);
            return string.Empty;
        }

        var bytes = reader.ReadBytes(byteCount);
        reader.Align(4);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private sealed record BlockInfo(uint UncompressedSize, uint CompressedSize, ushort Flags);
    private sealed record NodeInfo(string Path, long Offset, long Size, uint Flags);
    private readonly record struct SerializedHeader(
        uint MetadataSize,
        long FileSize,
        uint Version,
        long DataOffset,
        bool BigEndian,
        byte EndianFlag,
        int HeaderSize
    );
}
