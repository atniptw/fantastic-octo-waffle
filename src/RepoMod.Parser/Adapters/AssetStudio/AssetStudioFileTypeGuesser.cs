using AssetStudio;
using System.Text;

namespace RepoMod.Parser.Adapters.AssetStudio;

public static class AssetStudioFileTypeGuesser
{
    public static FileType? GuessFromHeader(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.IsEmpty)
        {
            return null;
        }

        var length = Math.Min(headerBytes.Length, 64);
        var header = Encoding.ASCII.GetString(headerBytes[..length]);

        if (header.StartsWith("UnityWebData1.0", StringComparison.Ordinal))
        {
            return FileType.WebFile;
        }

        if (header.StartsWith("UnityFS", StringComparison.Ordinal)
            || header.StartsWith("UnityWeb", StringComparison.Ordinal)
            || header.StartsWith("UnityRaw", StringComparison.Ordinal)
            || header.StartsWith("UnityArchive", StringComparison.Ordinal))
        {
            return FileType.BundleFile;
        }

        if (headerBytes.Length >= 2 && headerBytes[0] == 0x1F && headerBytes[1] == 0x8B)
        {
            return FileType.GZipFile;
        }

        if (headerBytes.Length >= 4 && headerBytes[0] == 0x50 && headerBytes[1] == 0x4B && headerBytes[2] == 0x03 && headerBytes[3] == 0x04)
        {
            return FileType.ZipFile;
        }

        return null;
    }
}
