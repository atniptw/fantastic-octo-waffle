using System.Text;

namespace RepoMod.Parser.Implementation;

internal static class UnityPackageTarReader
{
    private const int TarBlockSize = 512;

    public static IEnumerable<UnityPackageTarEntry> ReadEntries(Stream tarStream)
    {
        if (tarStream is null)
        {
            throw new ArgumentNullException(nameof(tarStream));
        }

        var header = new byte[TarBlockSize];
        while (true)
        {
            var headerRead = ReadExactlyOrEof(tarStream, header, TarBlockSize);
            if (headerRead == 0)
            {
                yield break;
            }

            if (headerRead != TarBlockSize)
            {
                throw new InvalidDataException("Unexpected end of tar stream while reading header.");
            }

            if (IsAllZero(header))
            {
                yield break;
            }

            var name = ReadString(header, 0, 100);
            var prefix = ReadString(header, 345, 155);
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                name = string.IsNullOrWhiteSpace(name)
                    ? prefix
                    : $"{prefix}/{name}";
            }

            var size = ParseOctal(ReadString(header, 124, 12));
            if (size < 0)
            {
                throw new InvalidDataException("Tar entry size cannot be negative.");
            }

            var typeFlag = header[156];
            var isDirectory = typeFlag == (byte)'5' || name.EndsWith('/');

            if (size > int.MaxValue)
            {
                throw new InvalidDataException($"Tar entry '{name}' exceeds supported size.");
            }

            var data = new byte[(int)size];
            if (data.Length > 0)
            {
                var dataRead = ReadExactlyOrEof(tarStream, data, data.Length);
                if (dataRead != data.Length)
                {
                    throw new InvalidDataException($"Unexpected end of tar stream while reading entry '{name}'.");
                }
            }

            var alignedSize = AlignToBlock(size);
            var paddingBytes = alignedSize - size;
            if (paddingBytes > 0)
            {
                SkipBytes(tarStream, paddingBytes);
            }

            yield return new UnityPackageTarEntry(name, data, isDirectory);
        }
    }

    private static long ParseOctal(string value)
    {
        var trimmed = value.Trim('\0', ' ');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return 0;
        }

        long result = 0;
        foreach (var ch in trimmed)
        {
            if (ch < '0' || ch > '7')
            {
                break;
            }

            result = (result * 8) + (ch - '0');
        }

        return result;
    }

    private static long AlignToBlock(long value)
    {
        var remainder = value % TarBlockSize;
        return remainder == 0 ? value : value + (TarBlockSize - remainder);
    }

    private static bool IsAllZero(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadString(byte[] source, int offset, int length)
    {
        var segment = source.AsSpan(offset, length);
        var end = segment.IndexOf((byte)0);
        if (end < 0)
        {
            end = segment.Length;
        }

        return Encoding.UTF8.GetString(segment[..end]).Trim();
    }

    private static int ReadExactlyOrEof(Stream stream, byte[] buffer, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static void SkipBytes(Stream stream, long bytesToSkip)
    {
        if (bytesToSkip <= 0)
        {
            return;
        }

        if (stream.CanSeek)
        {
            stream.Seek(bytesToSkip, SeekOrigin.Current);
            return;
        }

        var skipBuffer = new byte[Math.Min(TarBlockSize, (int)bytesToSkip)];
        var remaining = bytesToSkip;
        while (remaining > 0)
        {
            var read = stream.Read(skipBuffer, 0, (int)Math.Min(skipBuffer.Length, remaining));
            if (read == 0)
            {
                throw new InvalidDataException("Unexpected end of tar stream while skipping entry padding.");
            }

            remaining -= read;
        }
    }
}

internal sealed record UnityPackageTarEntry(string Name, byte[] Data, bool IsDirectory);