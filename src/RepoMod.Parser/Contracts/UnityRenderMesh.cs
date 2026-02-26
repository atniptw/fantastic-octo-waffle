namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderMesh(
    string ObjectId,
    string AssetId,
    string Name,
    int? VertexCount,
    int? SubMeshCount,
    int? BlendShapeCount,
    int? VertexChannelCount,
    int? VertexDataByteCount,
    int? IndexBufferElementCount,
    int? IndexFormat,
    string? VertexDataBase64,
    IReadOnlyList<int>? IndexValues,
    IReadOnlyList<float>? Positions,
    IReadOnlyList<float>? Normals,
    IReadOnlyList<float>? Tangents,
    IReadOnlyList<float>? Colors,
    IReadOnlyList<float>? Uv0,
    IReadOnlyList<float>? Uv1,
    IReadOnlyList<UnityRenderSubMesh> SubMeshes);
