namespace RepoMod.Parser.Contracts;

public sealed record ConverterScene(
    string SceneId,
    string ContainerId,
    string SourceType,
    IReadOnlyList<ConverterNode> Nodes,
    IReadOnlyList<ConverterPrimitive> Primitives,
    IReadOnlyList<string> Warnings);

public sealed record ConverterNode(
    string NodeId,
    string Name,
    string? ParentNodeId,
    IReadOnlyList<string> ChildNodeIds,
    IReadOnlyList<float>? LocalPosition,
    IReadOnlyList<float>? LocalRotation,
    IReadOnlyList<float>? LocalScale);

public sealed record ConverterPrimitive(
    string PrimitiveId,
    string RenderObjectId,
    string? NodeId,
    string MeshObjectId,
    string? MaterialObjectId,
    int SubMeshIndex,
    int Topology,
    IReadOnlyList<int> Indices,
    IReadOnlyList<float>? Positions,
    IReadOnlyList<float>? Normals,
    IReadOnlyList<float>? Tangents,
    IReadOnlyList<float>? Colors,
    IReadOnlyList<float>? Uv0,
    IReadOnlyList<float>? Uv1);

public sealed record ConverterDiagnostic(
    string Severity,
    string Code,
    string Message,
    string? PrimitiveId);

public sealed record ConverterSceneProjection(
    ConverterScene Scene,
    IReadOnlyList<ConverterDiagnostic> Diagnostics);