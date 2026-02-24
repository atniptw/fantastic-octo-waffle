namespace RepoMod.Parser.Contracts;

public sealed record SceneEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    string EdgeKind,
    string Confidence);
