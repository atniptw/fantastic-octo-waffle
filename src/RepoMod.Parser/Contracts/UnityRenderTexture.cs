namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderTexture(
    string ObjectId,
    string AssetId,
    string Name,
    int? Width,
    int? Height,
    int? TextureFormat);
