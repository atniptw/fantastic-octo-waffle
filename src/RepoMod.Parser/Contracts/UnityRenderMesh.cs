namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderMesh(
    string ObjectId,
    string AssetId,
    string Name,
    int? VertexCount,
    int? SubMeshCount,
    int? BlendShapeCount);
