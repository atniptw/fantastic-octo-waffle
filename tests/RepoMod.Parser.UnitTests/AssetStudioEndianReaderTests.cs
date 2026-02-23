using AssetStudio;
using System.IO;

namespace RepoMod.Parser.UnitTests;

public class AssetStudioEndianReaderTests
{
    [Fact]
    public void EndianBinaryReader_ReadUInt32_BigEndian()
    {
        var bytes = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        using var stream = new MemoryStream(bytes);
        using var reader = new EndianBinaryReader(stream, EndianType.BigEndian);

        var value = reader.ReadUInt32();

        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void EndianBinaryReader_ReadUInt32_LittleEndian()
    {
        var bytes = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        using var stream = new MemoryStream(bytes);
        using var reader = new EndianBinaryReader(stream, EndianType.LittleEndian);

        var value = reader.ReadUInt32();

        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void EndianSpanReader_ReadStringToNull_ReturnsPrefix()
    {
        var bytes = new byte[] { 0x41, 0x42, 0x43, 0x00, 0x44 };

        var value = bytes.AsSpan().ReadStringToNull();

        Assert.Equal("ABC", value);
    }
}
