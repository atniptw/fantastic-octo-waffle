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

        // Version: 6 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 6);

        // Unity version: "2020.3.48f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);

        // Unity revision: "b805b124c6b7\0"
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);

        // Size: 300 bytes (int64, BIG-ENDIAN per UnityPy)
        WriteBigEndianInt64(writer, 300);

        // Compressed BlocksInfo size: 123 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 123);

        // Uncompressed BlocksInfo size: 123 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 123);

        // Flags: 0 (no compression, embedded, no padding)
        WriteBigEndianUInt32(writer, 0);

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

        // Version: 7 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 7);

        // Unity version: "2022.3.0f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2022.3.0f1"));
        writer.Write((byte)0x00);

        // Unity revision: "abc123def456\0"
        writer.Write(Encoding.UTF8.GetBytes("abc123def456"));
        writer.Write((byte)0x00);

        // Size: 5000 bytes (int64, BIG-ENDIAN per UnityPy)
        WriteBigEndianInt64(writer, 5000);

        // Compressed BlocksInfo size: 256 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 256);

        // Uncompressed BlocksInfo size: 512 (uint32, BIG-ENDIAN per UnityPy)
        WriteBigEndianUInt32(writer, 512);

        // Flags: 0x281 = LZMA (1) | BlocksInfoAtEnd (0x80) | NeedsPadding (0x200)
        WriteBigEndianUInt32(writer, 0x281);

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
        // Arrange: UnityPy doesn't validate version during header parsing
        // It will fail reading subsequent fields if stream is truncated
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);

        // Version: 5 (any version is accepted during header parse)
        WriteBigEndianUInt32(writer, 5);
        // Stream ends here - missing Unity version string will cause EOF error
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act & Assert: Fails on missing data, not version validation
        // UnityPy would throw during ReadUtf8NullTerminated for Unity version
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void Parse_InvalidCompressionType_ParsesSuccessfully()
    {
        // Arrange: UnityPy does NOT validate compression type during header parsing
        // It only throws when actually decompressing with an unsupported type
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        WriteBigEndianUInt32(writer, 6);
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        WriteBigEndianInt64(writer, 300);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 0x10); // Invalid compression type (16)
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act
        var header = parser.Parse(stream);

        // Assert: Should parse successfully, UnityPy doesn't validate at parse time
        Assert.NotNull(header);
        Assert.Equal(6u, header.Version);
        Assert.Equal((CompressionType)0x10, header.CompressionType);
    }

    [Fact]
    public void Parse_EncryptionFlagsSet_ParsesSuccessfully()
    {
        // Arrange: Test that encryption flags (0x100, 0x400, 0x1000) are accepted per UnityPy
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        WriteBigEndianUInt32(writer, 6);
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        WriteBigEndianInt64(writer, 300);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 0x1400); // Combined encryption flags (0x400 | 0x1000)
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act
        var header = parser.Parse(stream);

        // Assert: should parse without error
        Assert.NotNull(header);
        Assert.Equal(6u, header.Version);
    }

    [Fact]
    public void Parse_PaddingFlagOnV6_ParsesSuccessfully()
    {
        // Arrange: UnityPy does NOT validate padding flag restrictions per version
        // It accepts any flag value during parsing
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0x00);
        WriteBigEndianUInt32(writer, 6); // Version 6
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0x00);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0x00);
        WriteBigEndianInt64(writer, 300);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 123);
        WriteBigEndianUInt32(writer, 0x200); // Padding flag
        stream.Position = 0;

        var parser = new UnityFSHeaderParser();

        // Act
        var header = parser.Parse(stream);

        // Assert: Should parse successfully
        Assert.NotNull(header);
        Assert.Equal(6u, header.Version);
        Assert.True(header.NeedsPaddingAtStart);
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
        // AlignedHeaderPosition is the aligned position after header
        Assert.Equal(84L, location.AlignedHeaderPosition);
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
        // AlignedHeaderPosition is aligned header: align(95, 16) = 96
        Assert.Equal(96L, location.AlignedHeaderPosition);
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
        // AlignedHeaderPosition is just the header position when already aligned
        Assert.Equal(84L, location.AlignedHeaderPosition);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = new UnityFSHeaderParser();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));
    }

    [Fact]
    public void CalculateBlocksInfoLocation_NullHeader_ThrowsArgumentNullException()
    {
        // Arrange
        var parser = new UnityFSHeaderParser();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => parser.CalculateBlocksInfoLocation(null!, 1000));
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
            WriteBigEndianUInt32(writer, 6);
            writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
            writer.Write((byte)0x00);
            writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
            writer.Write((byte)0x00);
            WriteBigEndianInt64(writer, 300);
            WriteBigEndianUInt32(writer, 123);
            WriteBigEndianUInt32(writer, 123);
            WriteBigEndianUInt32(writer, compressionType); // Compression type in bits 0-5
            stream.Position = 0;

            var parser = new UnityFSHeaderParser();

            // Act
            var header = parser.Parse(stream);

            // Assert
            Assert.Equal((CompressionType)compressionType, header.CompressionType);
        }
    }

    /// <summary>
    /// Helper method to write a uint32 in big-endian format.
    /// </summary>
    private static void WriteBigEndianUInt32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    /// <summary>
    /// Helper method to write an int64 in big-endian format.
    /// </summary>
    private static void WriteBigEndianInt64(BinaryWriter writer, long value)
    {
        writer.Write((byte)((value >> 56) & 0xFF));
        writer.Write((byte)((value >> 48) & 0xFF));
        writer.Write((byte)((value >> 40) & 0xFF));
        writer.Write((byte)((value >> 32) & 0xFF));
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }
}
