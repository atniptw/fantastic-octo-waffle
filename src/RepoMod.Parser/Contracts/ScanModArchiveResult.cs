namespace RepoMod.Parser.Contracts;

public sealed record ScanModArchiveResult(
    IReadOnlyList<DiscoveredBundle> Bundles,
    IReadOnlyList<string> Warnings,
    bool Success,
    string? Error)
{
    public static ScanModArchiveResult Succeeded(IReadOnlyList<DiscoveredBundle> bundles, IReadOnlyList<string>? warnings = null)
        => new(bundles, warnings ?? [], true, null);

    public static ScanModArchiveResult Failed(string error)
        => new([], [], false, error);
}
