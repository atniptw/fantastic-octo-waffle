namespace RepoMod.Parser.Contracts;

public sealed record ParsedSceneGraph(
    IReadOnlyList<SceneNode> Nodes,
    IReadOnlyList<SceneEdge> Edges,
    IReadOnlyList<RefLink> RefLinks);
