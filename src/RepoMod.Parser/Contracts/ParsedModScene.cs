namespace RepoMod.Parser.Contracts;

public sealed record ParsedModScene(
    string SceneId,
    ContainerDescriptor Container,
    IReadOnlyList<ParsedAssetRecord> Assets,
    IReadOnlyList<UnityObjectRef> ObjectRefs,
    IReadOnlyList<UnityRenderObject> RenderObjects,
    IReadOnlyList<AttachmentHint> AttachmentHints,
    IReadOnlyList<string> AvatarAssetIds,
    ParsedSceneGraph Graph,
    IReadOnlyList<string> Warnings);
