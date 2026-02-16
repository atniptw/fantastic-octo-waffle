using System;
using System.Collections.Generic;
using System.Text;

namespace UnityAssetParser;

internal static class TarReader
{
    private const int BlockSize = 512;

    public static IEnumerable<TarEntry> EnumerateEntries(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var offset = 0;
        while (offset + BlockSize <= data.Length)
        {
            var header = data.AsSpan(offset, BlockSize);
            if (IsZeroBlock(header))
            {
                yield break;
            }

            var name = ReadNullTerminatedString(header.Slice(0, 100));
            var size = ReadOctal(header.Slice(124, 12));
            var typeFlag = header[156];

            var dataOffset = offset + BlockSize;
            if (dataOffset + size > data.Length)
            {
                yield break;
            }

            if (typeFlag == 0 || typeFlag == (byte)'0')
            {
                yield return new TarEntry(name, dataOffset, size);
            }

            var paddedSize = AlignToBlock(size);
            offset = dataOffset + paddedSize;
        }
    }

    private static bool IsZeroBlock(ReadOnlySpan<byte> block)
    {
        for (var i = 0; i < block.Length; i++)
        {
            if (block[i] != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static int AlignToBlock(int size)
    {
        var remainder = size % BlockSize;
        return remainder == 0 ? size : size + (BlockSize - remainder);
    }

    private static string ReadNullTerminatedString(ReadOnlySpan<byte> data)
    {
        var length = data.IndexOf((byte)0);
        if (length < 0)
        {
            length = data.Length;
        }
        return Encoding.UTF8.GetString(data.Slice(0, length));
    }

    private static int ReadOctal(ReadOnlySpan<byte> data)
    {
        var length = data.IndexOf((byte)0);
        if (length < 0)
        {
            length = data.Length;
        }

        var value = 0;
        for (var i = 0; i < length; i++)
        {
            var ch = data[i];
            if (ch < (byte)'0' || ch > (byte)'7')
            {
                continue;
            }
            value = (value * 8) + (ch - (byte)'0');
        }
        return value;
    }
}

internal readonly struct TarEntry
{
    public TarEntry(string name, int offset, int size)
    {
        Name = name;
        Offset = offset;
        Size = size;
    }

    public string Name { get; }
    public int Offset { get; }
    public int Size { get; }
}
