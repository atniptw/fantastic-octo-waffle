using System;
using K4os.Compression.LZ4;

namespace UnityAssetParser;

internal static class Lz4Decoder
{
    public static byte[] DecodeBlock(ReadOnlySpan<byte> input, int uncompressedSize)
    {
        if (uncompressedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uncompressedSize));
        }

        var output = new byte[uncompressedSize];
        var decodedLength = LZ4Codec.Decode(
            input.ToArray(),
            0,
            input.Length,
            output,
            0,
            output.Length);

        if (decodedLength < 0)
        {
            throw new InvalidOperationException("Invalid LZ4 block.");
        }

        if (decodedLength != uncompressedSize)
        {
            throw new InvalidOperationException("Invalid LZ4 decompressed size.");
        }

        return output;
    }
}
