using System;
using System.Collections.Generic;
using System.IO;
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

            var serializedAssetIndex = 0;
            foreach (var node in blockInfo.Nodes)
            {
                var entry = new ContainerEntry(node.Path, node.Offset, node.Size, node.Flags);
                if (TrySlicePayload(decompressedData, node.Offset, node.Size, out var payload))
                {
                    entry.Payload = payload;
                    _ = TryParseSerializedPayload(payload, serializedAssetIndex, context);
                    serializedAssetIndex++;
                }
                else
                {
                    context.Warnings.Add($"UnityFS entry '{node.Path}' is out of range.");
                }
                container.Entries.Add(entry);
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

    private static void ParseSerializedFile(byte[] data, string sourceName, BaseAssetsContext context, string? serializedFileSourceName = null)
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

            var decoder = new Decoder();
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

    private static bool TryParseSerializedPayload(byte[] payload, int serializedAssetIndex, BaseAssetsContext context)
    {
        var serializedBefore = context.SerializedFiles.Count;
        var containersBefore = context.Containers.Count;

        try
        {
            var containerSourceName = $"serialized_asset_{serializedAssetIndex}";
            var serializedSourceName = $"asset_{serializedAssetIndex}";
            ParseSerializedFile(payload, containerSourceName, context, serializedSourceName);
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

            return false;
        }

        return true;
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
