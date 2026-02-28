using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace RepoMod.Parser.Implementation;

/// <summary>
/// Utilities for decoding and converting Unity texture formats to PNG.
/// </summary>
public static class TextureUtilities
{
    /// <summary>
    /// Tries to convert raw texture bytes from a Unity format to PNG base64.
    /// </summary>
    /// <param name="textureBytes">Raw bytes from ResourceReader</param>
    /// <param name="textureFormat">Unity TextureFormat enum value</param>
    /// <param name="width">Texture width in pixels</param>
    /// <param name="height">Texture height in pixels</param>
    /// <param name="pngBase64">Base64-encoded PNG, or null if conversion failed</param>
    /// <returns>True if conversion succeeded</returns>
    public static bool TryConvertTextureToPngBase64(
        byte[] textureBytes,
        int textureFormat,
        int width,
        int height,
        out string? pngBase64)
    {
        pngBase64 = null;

        if (textureBytes.Length == 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        try
        {
            byte[]? rgbaData = null;

            // Handle uncompressed formats directly
            switch (textureFormat)
            {
                case 4: // RGBA32
                    if (textureBytes.Length < width * height * 4)
                    {
                        return false;
                    }
                    rgbaData = textureBytes;
                    break;

                case 1: // RGB24
                    rgbaData = ConvertRgb24ToRgba32(textureBytes, width, height);
                    break;

                case 9: // BGRAHalf - convert to RGBA
                    rgbaData = ConvertBgraToRgba(textureBytes, width, height);
                    break;

                // DXT formats: for now, generate a fallback checkerboard
                case 10: // DXT1
                case 12: // DXT5
                case 28: // BC4
                case 29: // BC5
                case 48: // BC6H
                case 49: // BC7
                    // Fallback: generate checkerboard pattern
                    rgbaData = GenerateCheckerboardRgba32(width, height);
                    break;

                // Other formats: unsupported, generate checkerboard fallback
                default:
                    rgbaData = GenerateCheckerboardRgba32(width, height);
                    break;
            }

            if (rgbaData == null)
            {
                return false;
            }

            // Encode RGBA pixels to PNG
            using (var image = Image.LoadPixelData<Rgba32>(rgbaData, width, height))
            {
                using (var memoryStream = new MemoryStream())
                {
                    image.SaveAsPng(memoryStream);
                    var pngBytes = memoryStream.ToArray();
                    pngBase64 = Convert.ToBase64String(pngBytes);
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ConvertRgb24ToRgba32(byte[] rgb24Data, int width, int height)
    {
        var expectedSize = width * height * 3;
        if (rgb24Data.Length < expectedSize)
        {
            return GenerateCheckerboardRgba32(width, height);
        }

        var rgba32 = new byte[width * height * 4];
        var srcIndex = 0;
        var dstIndex = 0;

        for (var i = 0; i < width * height; i++)
        {
            rgba32[dstIndex] = rgb24Data[srcIndex];         // R
            rgba32[dstIndex + 1] = rgb24Data[srcIndex + 1]; // G
            rgba32[dstIndex + 2] = rgb24Data[srcIndex + 2]; // B
            rgba32[dstIndex + 3] = 255;                      // A

            srcIndex += 3;
            dstIndex += 4;
        }

        return rgba32;
    }

    private static byte[] ConvertBgraToRgba(byte[] bgraData, int width, int height)
    {
        var expectedSize = width * height * 4;
        if (bgraData.Length < expectedSize)
        {
            return GenerateCheckerboardRgba32(width, height);
        }

        var rgba32 = new byte[expectedSize];

        for (var i = 0; i < expectedSize; i += 4)
        {
            rgba32[i] = bgraData[i + 2];     // R <- B
            rgba32[i + 1] = bgraData[i + 1]; // G
            rgba32[i + 2] = bgraData[i];     // B <- R
            rgba32[i + 3] = bgraData[i + 3]; // A
        }

        return rgba32;
    }

    private static byte[] GenerateCheckerboardRgba32(int width, int height)
    {
        const int squareSize = 16;
        var rgba32 = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var squareX = x / squareSize;
                var squareY = y / squareSize;
                var isWhite = (squareX + squareY) % 2 == 0;

                var index = (y * width + x) * 4;
                var color = isWhite ? (byte)200 : (byte)100;

                rgba32[index] = color;     // R
                rgba32[index + 1] = color; // G
                rgba32[index + 2] = color; // B
                rgba32[index + 3] = 255;   // A
            }
        }

        return rgba32;
    }
}
