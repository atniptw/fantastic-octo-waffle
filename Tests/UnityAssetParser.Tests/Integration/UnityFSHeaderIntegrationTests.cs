using System.Text;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// Integration tests for parsing UnityFS header using Binary I/O utilities.
/// Tests real-world usage of alignment and string reading helpers.
/// </summary>
public class UnityFSHeaderIntegrationTests
{
    /// <summary>
    /// Creates a minimal UnityFS header for testing.
    /// Based on UnityFS-BundleSpec.md § 15.3 (Header Parsing).
    /// </summary>
    private static byte[] CreateMinimalUnityFSHeader()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 6 (uint32, little-endian)
        writer.Write((uint)6);

        // UnityVersion: "2022.3.0f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2022.3.0f1"));
        writer.Write((byte)0x00);

        // UnityRevision: "abcdef123456\0"
        writer.Write(Encoding.UTF8.GetBytes("abcdef123456"));
        writer.Write((byte)0x00);

        // Size: 0x1000 (int64, little-endian)
        writer.Write((long)0x1000);

        // CompressedBlocksInfoSize: 0x100 (uint32, little-endian)
        writer.Write((uint)0x100);

        // UncompressedBlocksInfoSize: 0x100 (uint32, little-endian)
        writer.Write((uint)0x100);

        // Flags: 0x00 (uint32, little-endian - no compression, embedded BlocksInfo)
        writer.Write((uint)0x00);

        return stream.ToArray();
    }

    [Fact]
    public void ParseUnityFSHeader_ValidHeader_ReadsAllFields()
    {
        // Arrange
        var headerBytes = CreateMinimalUnityFSHeader();
        using var stream = new MemoryStream(headerBytes);
        using var reader = new BinaryReader(stream);

        // Act - Parse header (mimicking § 15.3 algorithm)
        var signature = reader.ReadUtf8NullTerminated();
        var version = reader.ReadUInt32();
        var unityVersion = reader.ReadUtf8NullTerminated();
        var unityRevision = reader.ReadUtf8NullTerminated();
        var size = reader.ReadInt64();
        var compressedBlocksInfoSize = reader.ReadUInt32();
        var uncompressedBlocksInfoSize = reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        var headerEnd = stream.Position;

        // Assert
        Assert.Equal("UnityFS", signature);
        Assert.Equal(6u, version);
        Assert.Equal("2022.3.0f1", unityVersion);
        Assert.Equal("abcdef123456", unityRevision);
        Assert.Equal(0x1000, size);
        Assert.Equal(0x100u, compressedBlocksInfoSize);
        Assert.Equal(0x100u, uncompressedBlocksInfoSize);
        Assert.Equal(0x00u, flags);
        Assert.True(headerEnd > 0, "Header end position should be after all fields");
    }

    [Fact]
    public void ParseUnityFSHeader_WithAlignment_CorrectlyAligns()
    {
        // Arrange
        var headerBytes = CreateMinimalUnityFSHeader();
        using var stream = new MemoryStream(headerBytes);
        using var reader = new BinaryReader(stream);

        // Act - Parse header and align
        reader.ReadUtf8NullTerminated(); // signature
        reader.ReadUInt32(); // version
        reader.ReadUtf8NullTerminated(); // unityVersion
        reader.ReadUtf8NullTerminated(); // unityRevision
        reader.ReadInt64(); // size
        reader.ReadUInt32(); // compressedBlocksInfoSize
        reader.ReadUInt32(); // uncompressedBlocksInfoSize
        reader.ReadUInt32(); // flags

        var headerEnd = stream.Position;

        // Apply 4-byte alignment (per § 15.4 for version < 7, no 0x200 flag)
        reader.Align(4);

        var alignedPosition = stream.Position;

        // Assert
        Assert.True(alignedPosition >= headerEnd, "Aligned position should be >= header end");
        Assert.Equal(0, alignedPosition % 4); // Should be 4-byte aligned
    }

    [Fact]
    public void ParseUnityFSHeader_16ByteAlignment_CorrectlyAligns()
    {
        // Arrange: Header with version 7 and flags 0x200 (requires 16-byte alignment)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 7 (triggers 16-byte alignment check)
        writer.Write((uint)7);

        // UnityVersion: "2023.1.0f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2023.1.0f1"));
        writer.Write((byte)0x00);

        // UnityRevision: "xyz789\0"
        writer.Write(Encoding.UTF8.GetBytes("xyz789"));
        writer.Write((byte)0x00);

        // Size: 0x2000
        writer.Write((long)0x2000);

        // CompressedBlocksInfoSize: 0x200
        writer.Write((uint)0x200);

        // UncompressedBlocksInfoSize: 0x200
        writer.Write((uint)0x200);

        // Flags: 0x200 (bit 9 set - BlockInfoNeedPaddingAtStart)
        writer.Write((uint)0x200);

        // Add padding bytes to allow alignment to 16 bytes
        var currentPos = writer.BaseStream.Position;
        var padding = BinaryReaderExtensions.CalculatePadding(currentPos, 16);
        for (int i = 0; i < padding; i++)
        {
            writer.Write((byte)0x00);
        }

        var headerBytes = stream.ToArray();

        // Act
        using var readStream = new MemoryStream(headerBytes);
        using var reader = new BinaryReader(readStream);

        reader.ReadUtf8NullTerminated(); // signature
        reader.ReadUInt32(); // version
        reader.ReadUtf8NullTerminated(); // unityVersion
        reader.ReadUtf8NullTerminated(); // unityRevision
        reader.ReadInt64(); // size
        reader.ReadUInt32(); // compressedBlocksInfoSize
        reader.ReadUInt32(); // uncompressedBlocksInfoSize
        var flags = reader.ReadUInt32();

        var headerEnd = readStream.Position;

        // Apply 16-byte alignment (version >= 7 and flags & 0x200)
        var requiresAlignment16 = (flags & 0x200) != 0;
        if (requiresAlignment16)
        {
            reader.Align(16);
        }

        var alignedPosition = readStream.Position;

        // Assert
        Assert.True(requiresAlignment16, "Should require 16-byte alignment");
        Assert.True(alignedPosition >= headerEnd, "Aligned position should be >= header end");
        Assert.Equal(0, alignedPosition % 16); // Should be 16-byte aligned
    }

    [Fact]
    public void ParseNodePath_ValidPath_ReadsCorrectly()
    {
        // Arrange: Simulate node path reading (§ 15.5)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Node fields (simplified)
        writer.Write((long)0x00); // offset
        writer.Write((long)0x900); // size
        writer.Write((int)0x00); // flags
        writer.Write(Encoding.UTF8.GetBytes("assets/mesh.serialized"));
        writer.Write((byte)0x00); // null terminator

        var nodeBytes = stream.ToArray();

        // Act
        using var readStream = new MemoryStream(nodeBytes);
        using var reader = new BinaryReader(readStream);

        var offset = reader.ReadInt64();
        var size = reader.ReadInt64();
        var flags = reader.ReadInt32();
        var path = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal(0x00, offset);
        Assert.Equal(0x900, size);
        Assert.Equal(0x00, flags);
        Assert.Equal("assets/mesh.serialized", path);
    }

    [Fact]
    public void ParseMultipleNodes_SequentialReads_AllCorrect()
    {
        // Arrange: Multiple node entries
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Node 1
        writer.Write((long)0x00);
        writer.Write((long)0x500);
        writer.Write((int)0x00);
        writer.Write(Encoding.UTF8.GetBytes("assets/texture.png"));
        writer.Write((byte)0x00);

        // Node 2
        writer.Write((long)0x500);
        writer.Write((long)0x300);
        writer.Write((int)0x01);
        writer.Write(Encoding.UTF8.GetBytes("assets/material.mat"));
        writer.Write((byte)0x00);

        // Node 3
        writer.Write((long)0x800);
        writer.Write((long)0x200);
        writer.Write((int)0x00);
        writer.Write(Encoding.UTF8.GetBytes("assets/shader.shader"));
        writer.Write((byte)0x00);

        var nodeBytes = stream.ToArray();

        // Act
        using var readStream = new MemoryStream(nodeBytes);
        using var reader = new BinaryReader(readStream);

        var nodes = new List<(long offset, long size, int flags, string path)>();
        for (int i = 0; i < 3; i++)
        {
            var offset = reader.ReadInt64();
            var size = reader.ReadInt64();
            var flags = reader.ReadInt32();
            var path = reader.ReadUtf8NullTerminated();
            nodes.Add((offset, size, flags, path));
        }

        // Assert
        Assert.Equal(3, nodes.Count);

        Assert.Equal(0x00, nodes[0].offset);
        Assert.Equal(0x500, nodes[0].size);
        Assert.Equal("assets/texture.png", nodes[0].path);

        Assert.Equal(0x500, nodes[1].offset);
        Assert.Equal(0x300, nodes[1].size);
        Assert.Equal("assets/material.mat", nodes[1].path);

        Assert.Equal(0x800, nodes[2].offset);
        Assert.Equal(0x200, nodes[2].size);
        Assert.Equal("assets/shader.shader", nodes[2].path);
    }

    [Fact]
    public void StorageBlockTable_WithAlignment_ParsesCorrectly()
    {
        // Arrange: Storage block table with alignment (§ 15.5)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Block count: 2
        writer.Write((int)2);

        // Block 1
        writer.Write((uint)0x500); // uncompressedSize
        writer.Write((uint)0x400); // compressedSize
        writer.Write((ushort)0x02); // flags (LZ4)

        // Block 2
        writer.Write((uint)0x600); // uncompressedSize
        writer.Write((uint)0x500); // compressedSize
        writer.Write((ushort)0x00); // flags (no compression)

        // Current position: 4 + (4+4+2)*2 = 24 bytes
        // Need 4-byte alignment before nodeCount
        var currentPos = writer.BaseStream.Position;
        var padding = BinaryReaderExtensions.CalculatePadding(currentPos, 4);
        for (int i = 0; i < padding; i++)
        {
            writer.Write((byte)0x00);
        }

        // Node count: 1
        writer.Write((int)1);

        var blockBytes = stream.ToArray();

        // Act
        using var readStream = new MemoryStream(blockBytes);
        using var reader = new BinaryReader(readStream);

        var blockCount = reader.ReadInt32();
        var blocks = new List<(uint uncompressedSize, uint compressedSize, ushort flags)>();
        for (int i = 0; i < blockCount; i++)
        {
            var uncompressedSize = reader.ReadUInt32();
            var compressedSize = reader.ReadUInt32();
            var flags = reader.ReadUInt16();
            blocks.Add((uncompressedSize, compressedSize, flags));
        }

        // Align to 4 bytes
        reader.Align(4);

        var nodeCount = reader.ReadInt32();

        // Assert
        Assert.Equal(2, blockCount);
        Assert.Equal(2, blocks.Count);

        Assert.Equal(0x500u, blocks[0].uncompressedSize);
        Assert.Equal(0x400u, blocks[0].compressedSize);
        Assert.Equal(0x02, blocks[0].flags);

        Assert.Equal(0x600u, blocks[1].uncompressedSize);
        Assert.Equal(0x500u, blocks[1].compressedSize);
        Assert.Equal(0x00, blocks[1].flags);

        Assert.Equal(1, nodeCount);
    }
}
