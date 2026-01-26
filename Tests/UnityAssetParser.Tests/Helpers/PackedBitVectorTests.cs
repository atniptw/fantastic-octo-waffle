using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Helpers;

/// <summary>
/// Unit tests for PackedBitVector.
/// Tests bit unpacking, float reconstruction, and alignment behavior.
/// Based on UnityPy reference implementation.
/// </summary>
public class PackedBitVectorTests
{
    /// <summary>
    /// Helper method to create a binary stream with PackedBitVector data.
    /// </summary>
    private static MemoryStream CreatePackedBitVectorStream(
        uint numItems,
        float range,
        float start,
        uint bitSize,
        byte[] data)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        writer.Write(numItems);
        writer.Write(range);
        writer.Write(start);
        writer.Write(bitSize);
        writer.Write(data.Length);
        writer.Write(data);

        // Add padding to test alignment
        var padding = (4 - (data.Length % 4)) % 4;
        for (var i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Constructor_ReadsFieldsCorrectly()
    {
        // Arrange
        var data = new byte[] { 0xFF, 0x00, 0xAA };
        using var stream = CreatePackedBitVectorStream(
            numItems: 10,
            range: 2.0f,
            start: -1.0f,
            bitSize: 8,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        var packed = new PackedBitVector(reader);

        // Assert
        Assert.Equal(10u, packed.NumItems);
        Assert.Equal(2.0f, packed.Range);
        Assert.Equal(-1.0f, packed.Start);
        Assert.Equal((byte)8, packed.BitSize);
        Assert.Equal(data, packed.Data);
    }

    [Fact]
    public void Constructor_ZeroBitSize_SetsToNull()
    {
        // Arrange
        using var stream = CreatePackedBitVectorStream(
            numItems: 5,
            range: 1.0f,
            start: 0.0f,
            bitSize: 0,
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        var packed = new PackedBitVector(reader);

        // Assert
        Assert.Null(packed.BitSize);
    }

    [Fact]
    public void Constructor_AlignsTo4ByteBoundary()
    {
        // Arrange: 3 bytes of data requires 1 byte of padding to reach 4-byte boundary
        var data = new byte[] { 0x01, 0x02, 0x03 };
        using var stream = CreatePackedBitVectorStream(
            numItems: 3,
            range: 1.0f,
            start: 0.0f,
            bitSize: 8,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        var startPosition = reader.Position;

        // Act
        var packed = new PackedBitVector(reader);

        // Calculate expected position after reading
        // 4 (numItems) + 4 (range) + 4 (start) + 4 (bitSize) + 4 (data length) + 3 (data) + 1 (padding) = 24
        var expectedPosition = startPosition + 4 + 4 + 4 + 4 + 4 + 3 + 1;

        // Assert
        Assert.Equal(expectedPosition, reader.Position);
        Assert.Equal(0, reader.Position % 4); // Position should be 4-byte aligned
    }

    [Fact]
    public void UnpackInts_Uniform0And1Pattern_ReturnsCorrectValues()
    {
        // Arrange: Bit pattern 10101010 (0xAA) represents alternating 1 and 0 with 1-bit size
        // Reading 8 values with bit_size=1 from byte 0xAA should give [0, 1, 0, 1, 0, 1, 0, 1]
        var data = new byte[] { 0xAA }; // Binary: 10101010
        using var stream = CreatePackedBitVectorStream(
            numItems: 8,
            range: 1.0f,
            start: 0.0f,
            bitSize: 1,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackInts();

        // Assert: LSB first, so we read bits from right to left within each byte
        // Byte 0xAA = 10101010, reading LSB first: bit0=0, bit1=1, bit2=0, bit3=1, bit4=0, bit5=1, bit6=0, bit7=1
        Assert.Equal(new uint[] { 0, 1, 0, 1, 0, 1, 0, 1 }, result);
    }

    [Fact]
    public void UnpackInts_2BitValues_ReturnsCorrectValues()
    {
        // Arrange: Pack 4 values (0, 1, 2, 3) with 2 bits each into 1 byte
        // Binary: 11 10 01 00 (reading left to right) = 0xE4 (but LSB first)
        // LSB first: bits [00, 01, 10, 11] = byte value: 00 01 10 11 = 0x1B (binary: 00011011)
        // Actually in LSB: value0 uses bits 0-1, value1 uses bits 2-3, value2 uses bits 4-5, value3 uses bits 6-7
        var data = new byte[] { 0xE4 }; // Binary: 11100100
        // Reading LSB first: bits 0-1 = 00 (0), bits 2-3 = 01 (1), bits 4-5 = 10 (2), bits 6-7 = 11 (3)
        using var stream = CreatePackedBitVectorStream(
            numItems: 4,
            range: 3.0f,
            start: 0.0f,
            bitSize: 2,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackInts();

        // Assert
        Assert.Equal(new uint[] { 0, 1, 2, 3 }, result);
    }

    [Fact]
    public void UnpackInts_10BitValues_ReturnsCorrectMaxValue()
    {
        // Arrange: 10-bit max value is 1023 (0x3FF)
        // Pack value 1023 (binary: 11 11111111) in 10 bits
        // Bits 0-7: 11111111 (0xFF), bits 8-9: 11 (need to be in bit positions 0-1 of next byte)
        // Byte 0: 0xFF, Byte 1: 0x03 (binary: 00000011)
        var data = new byte[] { 0xFF, 0x03 };
        using var stream = CreatePackedBitVectorStream(
            numItems: 1,
            range: 1023.0f,
            start: 0.0f,
            bitSize: 10,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackInts();

        // Assert
        Assert.Single(result);
        Assert.Equal(1023u, result[0]);
    }

    [Fact]
    public void UnpackInts_16BitValues_ReturnsCorrectMaxValue()
    {
        // Arrange: 16-bit max value is 65535 (0xFFFF)
        var data = new byte[] { 0xFF, 0xFF };
        using var stream = CreatePackedBitVectorStream(
            numItems: 1,
            range: 65535.0f,
            start: 0.0f,
            bitSize: 16,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackInts();

        // Assert
        Assert.Single(result);
        Assert.Equal(65535u, result[0]);
    }

    [Fact]
    public void UnpackInts_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        using var stream = CreatePackedBitVectorStream(
            numItems: 0,
            range: 1.0f,
            start: 0.0f,
            bitSize: 8,
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackInts();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void UnpackInts_NullBitSize_ThrowsInvalidOperationException()
    {
        // Arrange
        using var stream = CreatePackedBitVectorStream(
            numItems: 5,
            range: 1.0f,
            start: 0.0f,
            bitSize: 0, // Will be set to null
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => packed.UnpackInts());
        Assert.Contains("BitSize must be set", exception.Message);
    }

    [Fact]
    public void UnpackFloats_Uniform0And1Pattern_ReturnsCorrectFloats()
    {
        // Arrange: Same as UnpackInts test but with float reconstruction
        // Values [0, 1, 0, 1, 0, 1, 0, 1] with range=1.0, start=0.0, bitSize=1
        // Formula: value = int * (1.0 / (2^1 - 1)) + 0.0 = int * 1.0 = int
        var data = new byte[] { 0xAA };
        using var stream = CreatePackedBitVectorStream(
            numItems: 8,
            range: 1.0f,
            start: 0.0f,
            bitSize: 1,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackFloats();

        // Assert
        Assert.Equal(8, result.Length);
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(i % 2 == 0 ? 0.0f : 1.0f, result[i]);
        }
    }

    [Fact]
    public void UnpackFloats_NonZeroStartAndRange_ReturnsCorrectValues()
    {
        // Arrange: Values [0, 1, 2, 3] with range=10.0, start=-5.0, bitSize=2
        // Scale = 10.0 / (2^2 - 1) = 10.0 / 3 ≈ 3.333...
        // value0 = 0 * 3.333... + (-5.0) = -5.0
        // value1 = 1 * 3.333... + (-5.0) ≈ -1.667
        // value2 = 2 * 3.333... + (-5.0) ≈ 1.667
        // value3 = 3 * 3.333... + (-5.0) = 5.0
        var data = new byte[] { 0xE4 }; // Contains [0, 1, 2, 3] with 2-bit encoding
        using var stream = CreatePackedBitVectorStream(
            numItems: 4,
            range: 10.0f,
            start: -5.0f,
            bitSize: 2,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackFloats();

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(-5.0f, result[0], precision: 5);
        Assert.Equal(-5.0f + 10.0f / 3.0f, result[1], precision: 5);
        Assert.Equal(-5.0f + 2 * 10.0f / 3.0f, result[2], precision: 5);
        Assert.Equal(5.0f, result[3], precision: 5);
    }

    [Fact]
    public void UnpackFloats_ZeroBitSize_ReturnsConstantArray()
    {
        // Arrange: When bitSize is 0, all values should be Start
        using var stream = CreatePackedBitVectorStream(
            numItems: 5,
            range: 100.0f,
            start: 42.0f,
            bitSize: 0,
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackFloats();

        // Assert
        Assert.Equal(5, result.Length);
        Assert.All(result, value => Assert.Equal(42.0f, value));
    }

    [Fact]
    public void UnpackFloats_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        using var stream = CreatePackedBitVectorStream(
            numItems: 0,
            range: 1.0f,
            start: 0.0f,
            bitSize: 8,
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act
        var result = packed.UnpackFloats();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void UnpackFloats_NullBitSize_HandlesAsConstantArray()
    {
        // Arrange: When BitSize is 0 (stored as null), UnpackFloats should return constant array
        using var stream = CreatePackedBitVectorStream(
            numItems: 3,
            range: 1.0f,
            start: 2.5f,
            bitSize: 0,
            data: Array.Empty<byte>());
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act - should return constant array, not throw
        var result = packed.UnpackFloats();

        // Assert
        Assert.Equal(3, result.Length);
        Assert.All(result, value => Assert.Equal(2.5f, value));
    }

    [Fact]
    public void UnpackInts_WithStartAndCount_ReturnsSubset()
    {
        // Arrange: Pack 8 values [0,1,0,1,0,1,0,1]
        var data = new byte[] { 0xAA };
        using var stream = CreatePackedBitVectorStream(
            numItems: 8,
            range: 1.0f,
            start: 0.0f,
            bitSize: 1,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act: Read 4 values starting at index 2
        var result = packed.UnpackInts(start: 2, count: 4);

        // Assert: Should get values at indices 2,3,4,5 which are [0,1,0,1]
        Assert.Equal(new uint[] { 0, 1, 0, 1 }, result);
    }

    [Fact]
    public void UnpackFloats_WithStartAndCount_ReturnsSubset()
    {
        // Arrange: Pack 8 values [0,1,0,1,0,1,0,1]
        var data = new byte[] { 0xAA };
        using var stream = CreatePackedBitVectorStream(
            numItems: 8,
            range: 1.0f,
            start: 0.0f,
            bitSize: 1,
            data: data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);
        var packed = new PackedBitVector(reader);

        // Act: Read 4 values starting at index 2
        var result = packed.UnpackFloats(start: 2, count: 4);

        // Assert: Should get values at indices 2,3,4,5 which are [0,1,0,1]
        Assert.Equal(new float[] { 0.0f, 1.0f, 0.0f, 1.0f }, result);
    }

    [Fact]
    public void Constructor_NullReader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PackedBitVector(null!));
    }
}
