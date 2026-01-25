using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Unit tests for UnityFSHeaderParser.
/// Tests header parsing, flag validation, and BlocksInfo location calculation.
/// </summary>
public class UnityFSHeaderParserTests
{
    /// <summary>
    /// Creates a minimal V6 UnityFS header with embedded, uncompressed BlocksInfo.
    /// </summary>
    private static byte[] CreateV6EmbeddedUncompressedHeader()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 6 (uint32, little-endian)
        writer.Write((uint)6);

        // Unity version: "2020.3.48f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);

        // Unity revision: "b805b124c6b7\0"
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);

        // Size: 300 bytes (int64, little-endian)
        writer.Write((long)300);

        // Compressed BlocksInfo size: 123 (uint32, little-endian)
        writer.Write((uint)123);

        // Uncompressed BlocksInfo size: 123 (uint32, little-endian)
        writer.Write((uint)123);

        // Flags: 0 (no compression, embedded, no padding)
        writer.Write((uint)0);

        return stream.ToArray();
    }

    /// <summary>
    /// Creates a V7 header with LZMA, streamed layout, and padding flag.
    /// </summary>
    private static byte[] CreateV7LzmaStreamedWithPaddingHeader()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 7 (uint32, little-endian)
        writer.Write((uint)7);

        // Unity version: "2022.3.0f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2022.3.0f1"));
        writer.Write((byte)0x00);

        // Unity revision: "abc123def456\0"
        writer.Write(Encoding.UTF8.GetBytes("abc123def456"));
        writer.Write((byte)0x00);

        // Size: 5000 bytes (int64, little-endian)
        writer.Write((long)5000);

        // Compressed BlocksInfo size: 256 (uint32, little-endian)
        writer.Write((uint)256);

        // Uncompressed BlocksInfo size: 512 (uint32, little-endian)
        writer.Write((uint)512);

        // Flags: 0x281 = LZMA (1) | BlocksInfoAtEnd (0x80) | NeedsPadding (0x200)
        writer.Write((uint)0x281);

        return stream.ToArray();
    }

    [Fact]
    public void Parse_ValidV6EmbeddedUncompressed_ParsesCorrectly()
    {
        // Arrange
        var headerBytes = CreateV6EmbeddedUncompressedHeader();
        using var stream = new MemoryStream(headerBytes);
        var parser = new UnityFSHeaderParser();

        // Act
        var header = parser.Parse(stream);

        // Assert
        Assert.Equal("UnityFS", header.Signature);
        Assert.Equal(6u, header.Version);
        Assert.Equal("2020.3.48f1", header.UnityVersion);
        Assert.Equal("b805b124c6b7", header.UnityRevision);
        Assert.Equal(300L, header.Size);
        Assert.Equal(123u, header.CompressedBlocksInfoSize);
        Assert.Equal(123u, header.UncompressedBlocksInfoSize);
        Assert.Equal(0u, header.Flags);
        Assert.Equal(CompressionType.None, header.CompressionType);
        Assert.False(header.BlocksInfoAtEnd);
        Assert.False(header.NeedsPaddingAtStart);
        Assert.Equal(4, header.AlignmentSize);
    }

    [Fact]
    public void Parse_ValidV7LzmaStreamedWithPadding_ParsesCorrectly()
    {
        // Arrange
        var headerBytes = CreateV7LzmaStreamedWithPaddingHeader();
        using var stream = new MemoryStream(headerBytes);
        var parser = new UnityFSHeaderParser();

        // Act
        var header = parser.Parse(stream);

        // Assert
        Assert.Equal("UnityFS", header.Signature);
        Assert.Equal(7u, header.Version);
        Assert.Equal("2022.3.0f1", header.UnityVersion);
        Assert.Equal("abc123def456", header.UnityRevision);
        Assert.Equal(5000L, header.Size);
        Assert.Equal(256u, header.CompressedBlocksInfoSize);
        Assert.Equal(512u, header.UncompressedBlocksInfoSize);
        Assert.Equal(0x281u, header.Flags);
        Assert.Equal(CompressionType.LZMA, header.CompressionType);
        Assert.True(header.BlocksInfoAtEnd);
        Assert.True(header.NeedsPaddingAtStart);
        Assert.Equal(16, header.AlignmentSize);
    }

    [Fact]
    public void Parse_InvalidSignature_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.UTF8.GetBytes("UnityWeb"));
        writer.Write((byte)0x00);
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        var ex = Assert.Throws<InvalidBundleSignatureException>(() => parser.Parse(stream));
        Assert.Equal("UnityWeb", ex.ActualSignature);
        Assert.Contains("UnityWeb", ex.Message);
    }

    [Fact]
    public void Parse_UnsupportedVersion_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 5 (unsupported)
        writer.Write((uint)5);
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        var ex = Assert.Throws<UnsupportedVersionException>(() => parser.Parse(stream));
        Assert.Equal(5u, ex.Version);
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public void Parse_InvalidCompressionType_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        writer.Write((uint)6);
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        writer.Write((long)300);
        writer.Write((uint)123);
        writer.Write((uint)123);
        writer.Write((uint)0x10); // Invalid compression type (16)
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        var ex = Assert.Throws<HeaderParseException>(() => parser.Parse(stream));
        Assert.Contains("compression type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ReservedBitsSet_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        writer.Write((uint)6);
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        writer.Write((long)300);
        writer.Write((uint)123);
        writer.Write((uint)123);
        writer.Write((uint)0x1000); // Reserved bit set
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        var ex = Assert.Throws<HeaderParseException>(() => parser.Parse(stream));
        Assert.Contains("Reserved", ex.Message);
    }

    [Fact]
    public void Parse_PaddingFlagOnV6_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        writer.Write((uint)6); // Version 6
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        writer.Write((long)300);
        writer.Write((uint)123);
        writer.Write((uint)123);
        writer.Write((uint)0x200); // Padding flag (only valid for v7+)
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        var ex = Assert.Throws<HeaderParseException>(() => parser.Parse(stream));
        Assert.Contains("Padding flag", ex.Message);
        Assert.Contains("v7+", ex.Message);
    }

    [Fact]
    public void CalculateBlocksInfoLocation_EmbeddedLayout_CalculatesCorrectly()
    {
        // Arrange
        var header = new UnityFSHeader
        {
            Signature = "UnityFS",
            Version = 6,
            UnityVersion = "2020.3.48f1",
            UnityRevision = "b805b124c6b7",
            Size = 1000,
            Flags = 0,  // Embedded, no padding
            CompressedBlocksInfoSize = 100,
            UncompressedBlocksInfoSize = 100,
            HeaderEndPosition = 83  // Example position after Flags
        };

        var parser = new UnityFSHeaderParser();

        // Act
        var location = parser.CalculateBlocksInfoLocation(header, fileLength: 1000);

        // Assert
        // Aligned to 4 bytes: 83 → 84
        Assert.Equal(84L, location.BlocksInfoPosition);
        Assert.Equal(1, location.AlignmentPadding);
        // Data starts after BlocksInfo: 84 + 100 = 184
        Assert.Equal(184L, location.DataOffset);
    }

    [Fact]
    public void CalculateBlocksInfoLocation_StreamedLayout_CalculatesCorrectly()
    {
        // Arrange
        var header = new UnityFSHeader
        {
            Signature = "UnityFS",
            Version = 7,
            UnityVersion = "2022.3.0f1",
            UnityRevision = "abc123def456",
            Size = 2000,
            Flags = 0x280,  // Streamed (bit 7) + padding (bit 9)
            CompressedBlocksInfoSize = 150,
            UncompressedBlocksInfoSize = 300,
            HeaderEndPosition = 95
        };

        var parser = new UnityFSHeaderParser();

        // Act
        var location = parser.CalculateBlocksInfoLocation(header, fileLength: 2000);

        // Assert
        // BlocksInfo at EOF: 2000 - 150 = 1850
        Assert.Equal(1850L, location.BlocksInfoPosition);
        // Data starts after aligned header: align(95, 16) = 96
        Assert.Equal(96L, location.DataOffset);
        Assert.Equal(1, location.AlignmentPadding);
    }

    [Fact]
    public void CalculateBlocksInfoLocation_AlreadyAligned_NoPadding()
    {
        // Arrange
        var header = new UnityFSHeader
        {
            Signature = "UnityFS",
            Version = 6,
            UnityVersion = "2020.3.48f1",
            UnityRevision = "b805b124c6b7",
            Size = 1000,
            Flags = 0,
            CompressedBlocksInfoSize = 100,
            UncompressedBlocksInfoSize = 100,
            HeaderEndPosition = 84  // Already 4-byte aligned
        };

        var parser = new UnityFSHeaderParser();

        // Act
        var location = parser.CalculateBlocksInfoLocation(header, fileLength: 1000);

        // Assert
        Assert.Equal(84L, location.BlocksInfoPosition);
        Assert.Equal(0, location.AlignmentPadding);
        Assert.Equal(184L, location.DataOffset);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = new UnityFSHeaderParser();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.Throws<ArgumentNullException>(() => parser.Parse(null));
#pragma warning restore CS8625
    }

    [Fact]
    public void CalculateBlocksInfoLocation_NullHeader_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = new UnityFSHeaderParser();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.Throws<ArgumentNullException>(() => parser.CalculateBlocksInfoLocation(null, 1000));
#pragma warning restore CS8625
    }

    [Fact]
    public void CalculateBlocksInfoLocation_NegativeFileLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var header = new UnityFSHeader
        {
            Signature = "UnityFS",
            Version = 6,
            UnityVersion = "2020.3.48f1",
            UnityRevision = "b805b124c6b7",
            Size = 1000,
            Flags = 0,
            CompressedBlocksInfoSize = 100,
            UncompressedBlocksInfoSize = 100,
            HeaderEndPosition = 84
        };

        var parser = new UnityFSHeaderParser();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            parser.CalculateBlocksInfoLocation(header, fileLength: -1));
    }

    [Fact]
    public void Parse_AllCompressionTypes_ParsesCorrectly()
    {
        // Test all valid compression types (0-4)
        for (uint compressionType = 0; compressionType <= 4; compressionType++)
        {
            // Arrange
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
            writer.Write((byte)0x00);
            writer.Write((uint)6);
            writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
            writer.Write((byte)0x00);
            writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
            writer.Write((byte)0x00);
            writer.Write((long)300);
            writer.Write((uint)123);
            writer.Write((uint)123);
            writer.Write(compressionType); // Compression type in bits 0-5
            stream.Position = 0;

            var parser = new UnityFSHeaderParser();

            // Act
            var header = parser.Parse(stream);

            // Assert
            Assert.Equal((CompressionType)compressionType, header.CompressionType);
        }
    }
}
