namespace RepoMod.Parser.Contracts;

public sealed record UnityRenderSubMesh(
    int SubMeshIndex,
    int? FirstByte,
    int? IndexCount,
    int? Topology,
    int? BaseVertex,
    int? FirstVertex,
    int? VertexCount);
