using System;
using System.Reflection;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests;

public sealed class PPtrDecodingTests
{
    [Fact]
    public void ReadPPtrPathId_Version5Plus_ReadsInt64PathId()
    {
        var readerType = GetParserType("UnityAssetParser.EndianBinaryReader");
        var method = GetReadPPtrPathIdMethod();

        var expectedPathId = 0x0102030405060708L;
        var buffer = new byte[12];

        WriteInt32LittleEndian(buffer, 0, 7);
        WriteInt64LittleEndian(buffer, 4, expectedPathId);

        var reader = Activator.CreateInstance(readerType, new object[] { buffer, false })!;
        var actualPathId = (long)method.Invoke(null, new object[] { reader, (uint)5 })!;
        var position = (int)readerType.GetProperty("Position")!.GetValue(reader)!;

        Assert.Equal(expectedPathId, actualPathId);
        Assert.Equal(12, position);
    }

    [Fact]
    public void ReadPPtrPathId_Version4_ReadsInt32PathId()
    {
        var readerType = GetParserType("UnityAssetParser.EndianBinaryReader");
        var method = GetReadPPtrPathIdMethod();

        const int expectedPathId = 0x12345678;
        var buffer = new byte[12];

        WriteInt32LittleEndian(buffer, 0, 9);
        WriteInt32LittleEndian(buffer, 4, expectedPathId);
        WriteInt32LittleEndian(buffer, 8, 0x7fffffff);

        var reader = Activator.CreateInstance(readerType, new object[] { buffer, false })!;
        var actualPathId = (long)method.Invoke(null, new object[] { reader, (uint)4 })!;
        var position = (int)readerType.GetProperty("Position")!.GetValue(reader)!;

        Assert.Equal(expectedPathId, actualPathId);
        Assert.Equal(8, position);
    }

    private static MethodInfo GetReadPPtrPathIdMethod()
    {
        var skeletonParserType = GetParserType("UnityAssetParser.SkeletonParser");
        var readerType = GetParserType("UnityAssetParser.EndianBinaryReader");

        return skeletonParserType.GetMethod(
            "ReadPPtrPathId",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { readerType, typeof(uint) },
            modifiers: null)
            ?? throw new InvalidOperationException("Could not locate ReadPPtrPathId method.");
    }

    private static Type GetParserType(string typeName)
    {
        return typeof(HhhParser).Assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Could not locate type '{typeName}'.");
    }

    private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = unchecked((byte)value);
        buffer[offset + 1] = unchecked((byte)(value >> 8));
        buffer[offset + 2] = unchecked((byte)(value >> 16));
        buffer[offset + 3] = unchecked((byte)(value >> 24));
    }

    private static void WriteInt64LittleEndian(byte[] buffer, int offset, long value)
    {
        WriteInt32LittleEndian(buffer, offset, unchecked((int)value));
        WriteInt32LittleEndian(buffer, offset + 4, unchecked((int)(value >> 32)));
    }
}
