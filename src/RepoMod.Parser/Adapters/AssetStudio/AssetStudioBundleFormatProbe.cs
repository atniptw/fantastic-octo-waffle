using RepoMod.Parser.Abstractions;
using System.Text;

namespace RepoMod.Parser.Adapters.AssetStudio;

public sealed class AssetStudioBundleFormatProbe : IBundleFormatProbe
{
    private static readonly string[] KnownSignatures =
    [
        "UnityFS",
        "UnityWeb",
        "UnityRaw",
        "UnityArchive"
    ];

    public bool IsLikelyUnityBundle(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.IsEmpty)
        {
            return false;
        }

        var length = Math.Min(headerBytes.Length, 32);
        var prefix = Encoding.ASCII.GetString(headerBytes[..length]);

        return KnownSignatures.Any(signature => prefix.StartsWith(signature, StringComparison.Ordinal));
    }
}
