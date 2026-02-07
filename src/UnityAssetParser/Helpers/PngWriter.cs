using System.IO.Compression;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Minimal PNG writer for RGBA32 images.
/// </summary>
public static class PngWriter
{
    public static byte[] EncodeRgba32(int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive");
        }

        int expected = width * height * 4;
        if (rgba.Length < expected)
        {
            throw new InvalidDataException($"RGBA data too short: {rgba.Length} < {expected}");
        }

        using var ms = new MemoryStream();

        // PNG signature
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR
        using (var ihdr = new MemoryStream())
        {
            WriteBigEndian(ihdr, (uint)width);
            WriteBigEndian(ihdr, (uint)height);
            ihdr.WriteByte(8);  // bit depth
            ihdr.WriteByte(6);  // color type: RGBA
            ihdr.WriteByte(0);  // compression
            ihdr.WriteByte(0);  // filter
            ihdr.WriteByte(0);  // interlace
            WriteChunk(ms, "IHDR", ihdr.ToArray());
        }

        // IDAT
        var raw = BuildRawScanlines(width, height, rgba);
        byte[] compressed = Compress(raw);
        WriteChunk(ms, "IDAT", compressed);

        // IEND
        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    private static byte[] BuildRawScanlines(int width, int height, byte[] rgba)
    {
        int stride = width * 4;
        var raw = new byte[height * (stride + 1)];
        int src = 0;
        int dst = 0;
        for (int y = 0; y < height; y++)
        {
            raw[dst++] = 0; // filter type 0
            Buffer.BlockCopy(rgba, src, raw, dst, stride);
            src += stride;
            dst += stride;
        }
        return raw;
    }

    private static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        WriteBigEndian(stream, (uint)data.Length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        uint crc = Crc32(typeBytes, data);
        WriteBigEndian(stream, crc);
    }

    private static void WriteBigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)((value >> 24) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static uint Crc32(byte[] typeBytes, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc;
    }
}
