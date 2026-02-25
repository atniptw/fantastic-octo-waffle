namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderMaterial(
    string ObjectId,
    string AssetId,
    string Name,
    string? ShaderObjectId,
    IReadOnlyList<UnityRenderTextureBinding> TextureBindings);
