using System;

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
        var inputIndex = 0;
        var outputIndex = 0;

        while (inputIndex < input.Length)
        {
            var token = input[inputIndex++];
            var literalLength = token >> 4;
            if (literalLength == 15)
            {
                byte len;
                do
                {
                    len = input[inputIndex++];
                    literalLength += len;
                } while (len == 255 && inputIndex < input.Length);
            }

            if (literalLength > 0)
            {
                if (inputIndex + literalLength > input.Length || outputIndex + literalLength > output.Length)
                {
                    throw new InvalidOperationException("Invalid LZ4 literal length.");
                }
                input.Slice(inputIndex, literalLength).CopyTo(output.AsSpan(outputIndex));
                inputIndex += literalLength;
                outputIndex += literalLength;
            }

            if (inputIndex >= input.Length)
            {
                break;
            }

            if (inputIndex + 2 > input.Length)
            {
                throw new InvalidOperationException("Invalid LZ4 match offset.");
            }

            var offset = input[inputIndex] | (input[inputIndex + 1] << 8);
            inputIndex += 2;
            if (offset == 0 || offset > outputIndex)
            {
                throw new InvalidOperationException("Invalid LZ4 offset.");
            }

            var matchLength = token & 0x0F;
            if (matchLength == 15)
            {
                byte len;
                do
                {
                    len = input[inputIndex++];
                    matchLength += len;
                } while (len == 255 && inputIndex < input.Length);
            }
            matchLength += 4;

            if (outputIndex + matchLength > output.Length)
            {
                throw new InvalidOperationException("Invalid LZ4 match length.");
            }

            var matchStart = outputIndex - offset;
            for (var i = 0; i < matchLength; i++)
            {
                output[outputIndex++] = output[matchStart + i];
            }
        }

        return output;
    }
}
