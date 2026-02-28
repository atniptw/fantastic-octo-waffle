namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderTexture(
    string ObjectId,
    string AssetId,
    string Name,
    int? Width,
    int? Height,
    int? TextureFormat,
    int? ImageDataByteCount,
    string? StreamPath,
    long? StreamOffset,
    int? StreamSize,
    string? ImageDataBase64);
