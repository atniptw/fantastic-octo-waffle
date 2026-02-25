namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderPrimitive(
    string PrimitiveId,
    string AssetId,
    string RenderObjectId,
    string? GameObjectId,
    string MeshObjectId,
    int SubMeshIndex,
    string? MaterialObjectId,
    int? FirstByte,
    int? IndexCount,
    int? Topology,
    int? BaseVertex,
    int? FirstVertex,
    int? VertexCount);
