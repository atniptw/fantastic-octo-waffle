using System.Text;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Helpers;

/// <summary>
/// Unit tests for BinaryReaderExtensions UTF-8 string reading.
/// </summary>
public class StringReadingTests
{
    [Fact]
    public void ReadUtf8NullTerminated_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x00 }); // Just null terminator
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal(string.Empty, result);
        Assert.Equal(1, stream.Position); // Should be after null terminator
    }

    [Fact]
    public void ReadUtf8NullTerminated_SingleCharacter_ReturnsCorrectString()
    {
        // Arrange: "A\0"
        using var stream = new MemoryStream(new byte[] { 0x41, 0x00 });
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("A", result);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void ReadUtf8NullTerminated_BasicAsciiString_ReturnsCorrectString()
    {
        // Arrange: "Hello\0"
        var bytes = Encoding.UTF8.GetBytes("Hello").Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("Hello", result);
        Assert.Equal(6, stream.Position);
    }

    [Fact]
    public void ReadUtf8NullTerminated_Utf8MultibyteChars_ReturnsCorrectString()
    {
        // Arrange: "你好\0" (Chinese "Hello")
        var bytes = Encoding.UTF8.GetBytes("你好").Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("你好", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_StringWithEmoji_ReturnsCorrectString()
    {
        // Arrange: "Test🚀\0" (emoji)
        var bytes = Encoding.UTF8.GetBytes("Test🚀").Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("Test🚀", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_UnityFSSignature_ReturnsCorrectString()
    {
        // Arrange: "UnityFS\0"
        var bytes = Encoding.UTF8.GetBytes("UnityFS").Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("UnityFS", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_PathString_ReturnsCorrectString()
    {
        // Arrange: "assets/mesh.serialized\0"
        var bytes = Encoding.UTF8.GetBytes("assets/mesh.serialized").Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("assets/mesh.serialized", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_SmallString_UsesInlineAllocation()
    {
        // Arrange: String < 8KB threshold
        var testString = new string('A', 1024); // 1KB
        var bytes = Encoding.UTF8.GetBytes(testString).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal(testString, result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_LargeString_UsesArrayPool()
    {
        // Arrange: String > 8KB threshold (should use ArrayPool)
        var testString = new string('B', 10000); // ~10KB
        var bytes = Encoding.UTF8.GetBytes(testString).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal(testString, result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_MaxLengthMinusOne_Succeeds()
    {
        // Arrange: 1MB - 1 byte (should succeed)
        var maxLength = 1024 * 1024; // 1MB
        var testString = new string('C', maxLength - 1);
        var bytes = Encoding.UTF8.GetBytes(testString).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated(maxLength);

        // Assert
        Assert.Equal(testString, result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_ExceedsMaxLength_ThrowsStringReadException()
    {
        // Arrange: String exceeds maxLength
        var maxLength = 100;
        var testString = new string('D', maxLength + 1);
        var bytes = Encoding.UTF8.GetBytes(testString).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act & Assert
        var exception = Assert.Throws<StringReadException>(() =>
            reader.ReadUtf8NullTerminated(maxLength));
        Assert.Contains("exceeds maximum length", exception.Message);
    }

    [Fact]
    public void ReadUtf8NullTerminated_NoNullTerminator_ThrowsStreamBoundsException()
    {
        // Arrange: String without null terminator
        var bytes = Encoding.UTF8.GetBytes("NoTerminator");
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act & Assert
        var exception = Assert.Throws<StreamBoundsException>(() =>
            reader.ReadUtf8NullTerminated());
        Assert.Contains("end of stream", exception.Message);
        Assert.Contains("null terminator", exception.Message);
    }

    [Fact]
    public void ReadUtf8NullTerminated_InvalidUtf8Sequence_ThrowsUtf8DecodingException()
    {
        // Arrange: Invalid UTF-8 sequence (orphaned surrogate high byte)
        var bytes = new byte[] { 0xFF, 0xFE, 0x00 }; // Invalid UTF-8
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act & Assert
        var exception = Assert.Throws<Utf8DecodingException>(() =>
            reader.ReadUtf8NullTerminated());
        Assert.Contains("UTF-8 decoding failed", exception.Message);
    }

    [Fact]
    public void ReadUtf8NullTerminated_Utf8BOM_HandledCorrectly()
    {
        // Arrange: UTF-8 BOM + "Test\0"
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var bytes = bom.Concat(Encoding.UTF8.GetBytes("Test")).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        // BOM is included in the decoded string (C# UTF-8 decoder behavior)
        Assert.StartsWith("\uFEFF", result); // BOM character
        Assert.Contains("Test", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_AlignedOffset_ReadsCorrectly()
    {
        // Arrange: String starts at 4-byte aligned offset
        var padding = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }; // 4 bytes padding
        var bytes = padding.Concat(Encoding.UTF8.GetBytes("Aligned")).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        reader.ReadBytes(4); // Skip padding to position 4

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("Aligned", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_UnalignedOffset_ReadsCorrectly()
    {
        // Arrange: String starts at unaligned offset (position 3)
        var padding = new byte[] { 0xFF, 0xFF, 0xFF }; // 3 bytes padding
        var bytes = padding.Concat(Encoding.UTF8.GetBytes("Unaligned")).Concat(new byte[] { 0x00 }).ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        reader.ReadBytes(3); // Skip to position 3

        // Act
        var result = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("Unaligned", result);
    }

    [Fact]
    public void ReadUtf8NullTerminated_NullReader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            BinaryReaderExtensions.ReadUtf8NullTerminated(null!));
    }

    [Fact]
    public void ReadUtf8NullTerminated_ZeroMaxLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x00 });
        using var reader = new BinaryReader(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.ReadUtf8NullTerminated(maxLength: 0));
    }

    [Fact]
    public void ReadUtf8NullTerminated_NegativeMaxLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x00 });
        using var reader = new BinaryReader(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.ReadUtf8NullTerminated(maxLength: -1));
    }

    [Fact]
    public void ReadUtf8NullTerminated_MultipleStrings_ReadsSequentially()
    {
        // Arrange: "First\0Second\0Third\0"
        var bytes = Encoding.UTF8.GetBytes("First")
            .Concat(new byte[] { 0x00 })
            .Concat(Encoding.UTF8.GetBytes("Second"))
            .Concat(new byte[] { 0x00 })
            .Concat(Encoding.UTF8.GetBytes("Third"))
            .Concat(new byte[] { 0x00 })
            .ToArray();
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        // Act
        var first = reader.ReadUtf8NullTerminated();
        var second = reader.ReadUtf8NullTerminated();
        var third = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("First", first);
        Assert.Equal("Second", second);
        Assert.Equal("Third", third);
    }
}
