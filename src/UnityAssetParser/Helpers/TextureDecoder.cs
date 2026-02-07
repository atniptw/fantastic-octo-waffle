using UnityAssetParser.Export;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Decodes Unity Texture2D raw data into RGBA32 for rendering/export.
/// Supports a minimal set of formats commonly used by mods.
/// </summary>
public static class TextureDecoder
{
    public static byte[] DecodeToRgba32(TextureFormat format, byte[] data, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive");
        }

        return format switch
        {
            TextureFormat.RGBA32 => DecodeRgba32(data, width, height),
            TextureFormat.ARGB32 => DecodeArgb32(data, width, height),
            TextureFormat.BGRA32 => DecodeBgra32(data, width, height),
            TextureFormat.RGB24 => DecodeRgb24(data, width, height),
            TextureFormat.DXT1 => DecodeDxt1(data, width, height),
            TextureFormat.DXT5 => DecodeDxt5(data, width, height),
            _ => throw new NotSupportedException($"Unsupported TextureFormat: {format}")
        };
    }

    private static byte[] DecodeRgba32(byte[] data, int width, int height)
    {
        var expected = width * height * 4;
        if (data.Length < expected)
        {
            throw new InvalidDataException($"RGBA32 data too short: {data.Length} < {expected}");
        }

        if (data.Length == expected)
        {
            return data;
        }

        // Trim any trailing padding
        var result = new byte[expected];
        Array.Copy(data, result, expected);
        return result;
    }

    private static byte[] DecodeArgb32(byte[] data, int width, int height)
    {
        var expected = width * height * 4;
        if (data.Length < expected)
        {
            throw new InvalidDataException($"ARGB32 data too short: {data.Length} < {expected}");
        }

        var result = new byte[expected];
        for (int i = 0; i < expected; i += 4)
        {
            byte a = data[i];
            byte r = data[i + 1];
            byte g = data[i + 2];
            byte b = data[i + 3];
            result[i] = r;
            result[i + 1] = g;
            result[i + 2] = b;
            result[i + 3] = a;
        }

        return result;
    }

    private static byte[] DecodeBgra32(byte[] data, int width, int height)
    {
        var expected = width * height * 4;
        if (data.Length < expected)
        {
            throw new InvalidDataException($"BGRA32 data too short: {data.Length} < {expected}");
        }

        var result = new byte[expected];
        for (int i = 0; i < expected; i += 4)
        {
            byte b = data[i];
            byte g = data[i + 1];
            byte r = data[i + 2];
            byte a = data[i + 3];
            result[i] = r;
            result[i + 1] = g;
            result[i + 2] = b;
            result[i + 3] = a;
        }

        return result;
    }

    private static byte[] DecodeRgb24(byte[] data, int width, int height)
    {
        var expected = width * height * 3;
        if (data.Length < expected)
        {
            throw new InvalidDataException($"RGB24 data too short: {data.Length} < {expected}");
        }

        var result = new byte[width * height * 4];
        int src = 0;
        int dst = 0;
        for (int i = 0; i < width * height; i++)
        {
            result[dst++] = data[src++];
            result[dst++] = data[src++];
            result[dst++] = data[src++];
            result[dst++] = 255;
        }

        return result;
    }

    private static byte[] DecodeDxt1(byte[] data, int width, int height)
    {
        var result = new byte[width * height * 4];
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;

        int offset = 0;
        for (int y = 0; y < blockCountY; y++)
        {
            for (int x = 0; x < blockCountX; x++)
            {
                if (offset + 8 > data.Length)
                {
                    throw new InvalidDataException("DXT1 data truncated");
                }

                ushort c0 = BitConverter.ToUInt16(data, offset);
                ushort c1 = BitConverter.ToUInt16(data, offset + 2);
                uint bits = BitConverter.ToUInt32(data, offset + 4);
                offset += 8;

                var colors = DecodeDxtColors(c0, c1, c0 > c1);

                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        int pixelX = x * 4 + col;
                        int pixelY = y * 4 + row;
                        if (pixelX >= width || pixelY >= height)
                        {
                            bits >>= 2;
                            continue;
                        }

                        int code = (int)(bits & 0x3);
                        bits >>= 2;

                        int dst = (pixelY * width + pixelX) * 4;
                        result[dst] = colors[code][0];
                        result[dst + 1] = colors[code][1];
                        result[dst + 2] = colors[code][2];
                        result[dst + 3] = colors[code][3];
                    }
                }
            }
        }

        return result;
    }

    private static byte[] DecodeDxt5(byte[] data, int width, int height)
    {
        var result = new byte[width * height * 4];
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;

        int offset = 0;
        for (int y = 0; y < blockCountY; y++)
        {
            for (int x = 0; x < blockCountX; x++)
            {
                if (offset + 16 > data.Length)
                {
                    throw new InvalidDataException("DXT5 data truncated");
                }

                byte alpha0 = data[offset];
                byte alpha1 = data[offset + 1];
                ulong alphaBits = 0;
                for (int i = 0; i < 6; i++)
                {
                    alphaBits |= (ulong)data[offset + 2 + i] << (8 * i);
                }

                ushort c0 = BitConverter.ToUInt16(data, offset + 8);
                ushort c1 = BitConverter.ToUInt16(data, offset + 10);
                uint bits = BitConverter.ToUInt32(data, offset + 12);
                offset += 16;

                var colors = DecodeDxtColors(c0, c1, true);
                var alphas = DecodeDxt5AlphaTable(alpha0, alpha1);

                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        int pixelX = x * 4 + col;
                        int pixelY = y * 4 + row;
                        if (pixelX >= width || pixelY >= height)
                        {
                            bits >>= 2;
                            alphaBits >>= 3;
                            continue;
                        }

                        int colorCode = (int)(bits & 0x3);
                        bits >>= 2;

                        int alphaCode = (int)(alphaBits & 0x7);
                        alphaBits >>= 3;

                        int dst = (pixelY * width + pixelX) * 4;
                        result[dst] = colors[colorCode][0];
                        result[dst + 1] = colors[colorCode][1];
                        result[dst + 2] = colors[colorCode][2];
                        result[dst + 3] = alphas[alphaCode];
                    }
                }
            }
        }

        return result;
    }

    private static byte[][] DecodeDxtColors(ushort c0, ushort c1, bool fourColorMode)
    {
        var color0 = DecodeRgb565(c0);
        var color1 = DecodeRgb565(c1);

        var colors = new byte[4][];
        colors[0] = new[] { color0.r, color0.g, color0.b, (byte)255 };
        colors[1] = new[] { color1.r, color1.g, color1.b, (byte)255 };

        if (fourColorMode)
        {
            colors[2] = new[]
            {
                (byte)((2 * color0.r + color1.r) / 3),
                (byte)((2 * color0.g + color1.g) / 3),
                (byte)((2 * color0.b + color1.b) / 3),
                (byte)255
            };
            colors[3] = new[]
            {
                (byte)((color0.r + 2 * color1.r) / 3),
                (byte)((color0.g + 2 * color1.g) / 3),
                (byte)((color0.b + 2 * color1.b) / 3),
                (byte)255
            };
        }
        else
        {
            colors[2] = new[]
            {
                (byte)((color0.r + color1.r) / 2),
                (byte)((color0.g + color1.g) / 2),
                (byte)((color0.b + color1.b) / 2),
                (byte)255
            };
            colors[3] = new[] { (byte)0, (byte)0, (byte)0, (byte)0 };
        }

        return colors;
    }

    private static byte[] DecodeDxt5AlphaTable(byte alpha0, byte alpha1)
    {
        var table = new byte[8];
        table[0] = alpha0;
        table[1] = alpha1;

        if (alpha0 > alpha1)
        {
            table[2] = (byte)((6 * alpha0 + 1 * alpha1) / 7);
            table[3] = (byte)((5 * alpha0 + 2 * alpha1) / 7);
            table[4] = (byte)((4 * alpha0 + 3 * alpha1) / 7);
            table[5] = (byte)((3 * alpha0 + 4 * alpha1) / 7);
            table[6] = (byte)((2 * alpha0 + 5 * alpha1) / 7);
            table[7] = (byte)((1 * alpha0 + 6 * alpha1) / 7);
        }
        else
        {
            table[2] = (byte)((4 * alpha0 + 1 * alpha1) / 5);
            table[3] = (byte)((3 * alpha0 + 2 * alpha1) / 5);
            table[4] = (byte)((2 * alpha0 + 3 * alpha1) / 5);
            table[5] = (byte)((1 * alpha0 + 4 * alpha1) / 5);
            table[6] = 0;
            table[7] = 255;
        }

        return table;
    }

    private static (byte r, byte g, byte b) DecodeRgb565(ushort value)
    {
        byte r = (byte)(((value >> 11) & 0x1F) * 255 / 31);
        byte g = (byte)(((value >> 5) & 0x3F) * 255 / 63);
        byte b = (byte)((value & 0x1F) * 255 / 31);
        return (r, g, b);
    }
}
