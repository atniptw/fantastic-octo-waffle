namespace RepoMod.Parser.Contracts;

public sealed record ParsedModScene(
    string SceneId,
    ContainerDescriptor Container,
    IReadOnlyList<ParsedAssetRecord> Assets,
    IReadOnlyList<UnityObjectRef> ObjectRefs,
    IReadOnlyList<AttachmentHint> AttachmentHints,
    IReadOnlyList<string> AvatarAssetIds,
    IReadOnlyList<string> Warnings);
