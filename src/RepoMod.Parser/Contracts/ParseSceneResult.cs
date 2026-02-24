namespace RepoMod.Parser.Contracts;

public sealed record ParseSceneResult(
    ParsedModScene? Scene,
    bool Success,
    string? Error)
{
    public static ParseSceneResult Succeeded(ParsedModScene scene)
        => new(scene, true, null);

    public static ParseSceneResult Failed(string error)
        => new(null, false, error);
}
