namespace RepoMod.Parser.Abstractions;

public interface IBundleFormatProbe
{
    bool IsLikelyUnityBundle(ReadOnlySpan<byte> headerBytes);
}
