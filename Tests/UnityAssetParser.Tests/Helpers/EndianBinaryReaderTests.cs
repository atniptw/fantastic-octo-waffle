using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Helpers;

/// <summary>
/// Unit tests for EndianBinaryReader.
/// Tests endianness conversion and basic reading operations.
/// </summary>
public class EndianBinaryReaderTests
{
    [Fact]
    public void Constructor_NullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EndianBinaryReader(null!, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsBigEndian_ReturnsCorrectValue(bool isBigEndian)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var reader = new EndianBinaryReader(stream, isBigEndian);

        // Act & Assert
        Assert.Equal(isBigEndian, reader.IsBigEndian);
    }

    [Fact]
    public void ReadByte_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0x12, 0x34, 0x56, 0x78];
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, false);

        // Act
        byte value1 = reader.ReadByte();
        byte value2 = reader.ReadByte();

        // Assert
        Assert.Equal(0x12, value1);
        Assert.Equal(0x34, value2);
    }

    [Fact]
    public void ReadInt16_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0x34, 0x12]; // Little-endian: 0x1234
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        short value = reader.ReadInt16();

        // Assert
        Assert.Equal(0x1234, value);
    }

    [Fact]
    public void ReadInt16_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        byte[] data = [0x12, 0x34]; // Big-endian: 0x1234
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        short value = reader.ReadInt16();

        // Assert
        Assert.Equal(0x1234, value);
    }

    [Fact]
    public void ReadUInt16_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0x34, 0x12]; // Little-endian: 0x1234
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        ushort value = reader.ReadUInt16();

        // Assert
        Assert.Equal((ushort)0x1234, value);
    }

    [Fact]
    public void ReadUInt16_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        byte[] data = [0x12, 0x34]; // Big-endian: 0x1234
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        ushort value = reader.ReadUInt16();

        // Assert
        Assert.Equal((ushort)0x1234, value);
    }

    [Fact]
    public void ReadInt32_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0x78, 0x56, 0x34, 0x12]; // Little-endian: 0x12345678
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        int value = reader.ReadInt32();

        // Assert
        Assert.Equal(0x12345678, value);
    }

    [Fact]
    public void ReadInt32_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        byte[] data = [0x12, 0x34, 0x56, 0x78]; // Big-endian: 0x12345678
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        int value = reader.ReadInt32();

        // Assert
        Assert.Equal(0x12345678, value);
    }

    [Fact]
    public void ReadUInt32_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0x78, 0x56, 0x34, 0x12]; // Little-endian: 0x12345678
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        uint value = reader.ReadUInt32();

        // Assert
        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void ReadUInt32_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        byte[] data = [0x12, 0x34, 0x56, 0x78]; // Big-endian: 0x12345678
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        uint value = reader.ReadUInt32();

        // Assert
        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void ReadInt64_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        byte[] data = [0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]; // Little-endian
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        long value = reader.ReadInt64();

        // Assert
        Assert.Equal(0x123456789ABCDEF0L, value);
    }

    [Fact]
    public void ReadInt64_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        byte[] data = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]; // Big-endian
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        long value = reader.ReadInt64();

        // Assert
        Assert.Equal(0x123456789ABCDEF0L, value);
    }

    [Fact]
    public void ReadSingle_LittleEndian_ReturnsCorrectValue()
    {
        // Arrange
        float expectedValue = 123.456f;
        byte[] data = BitConverter.GetBytes(expectedValue);
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        float value = reader.ReadSingle();

        // Assert
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void ReadSingle_BigEndian_SwapsBytesCorrectly()
    {
        // Arrange
        float expectedValue = 123.456f;
        byte[] littleEndianData = BitConverter.GetBytes(expectedValue);
        byte[] bigEndianData = [littleEndianData[3], littleEndianData[2], littleEndianData[1], littleEndianData[0]];
        using var stream = new MemoryStream(bigEndianData);
        using var reader = new EndianBinaryReader(stream, isBigEndian: true);

        // Act
        float value = reader.ReadSingle();

        // Assert
        Assert.Equal(expectedValue, value, precision: 5);
    }

    [Fact]
    public void ReadUtf8NullTerminated_ReturnsCorrectString()
    {
        // Arrange
        byte[] data = [0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x00]; // "Hello\0World\0"
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        string value1 = reader.ReadUtf8NullTerminated();
        string value2 = reader.ReadUtf8NullTerminated();

        // Assert
        Assert.Equal("Hello", value1);
        Assert.Equal("World", value2);
    }

    [Fact]
    public void Align_AlignsPositionCorrectly()
    {
        // Arrange
        byte[] data = new byte[20];
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        reader.Position = 1;
        reader.Align(4);

        // Assert
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void Align_AlreadyAligned_DoesNotMove()
    {
        // Arrange
        byte[] data = new byte[20];
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        reader.Position = 4;
        reader.Align(4);

        // Assert
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void Position_GetSet_WorksCorrectly()
    {
        // Arrange
        byte[] data = new byte[20];
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act
        reader.Position = 10;

        // Assert
        Assert.Equal(10, reader.Position);
    }

    [Fact]
    public void Length_ReturnsCorrectStreamLength()
    {
        // Arrange
        byte[] data = new byte[42];
        using var stream = new MemoryStream(data);
        using var reader = new EndianBinaryReader(stream, isBigEndian: false);

        // Act & Assert
        Assert.Equal(42, reader.Length);
    }
}
