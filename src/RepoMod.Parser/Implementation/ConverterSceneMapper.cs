using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public static class ConverterSceneMapper
{
    public static ConverterSceneProjection MapWithDiagnostics(ParsedModScene scene)
    {
        var mappedScene = Map(scene);
        var diagnostics = ConverterSceneValidator.Validate(mappedScene);
        return new ConverterSceneProjection(mappedScene, diagnostics);
    }

    public static ConverterScene Map(ParsedModScene scene)
    {
        var nodes = scene.RenderObjects
            .Where(item => item.Kind is "transform" or "gameobject")
            .OrderBy(item => item.ObjectId, StringComparer.Ordinal)
            .Select(item => new ConverterNode(
                item.ObjectId,
                item.Name,
                item.ParentObjectId,
                item.ChildObjectIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                item.LocalPosition,
                item.LocalRotation,
                item.LocalScale))
            .ToArray();

        var primitives = scene.RenderPrimitives
            .OrderBy(item => item.PrimitiveId, StringComparer.Ordinal)
            .Select(item => new ConverterPrimitive(
                item.PrimitiveId,
                item.RenderObjectId,
                item.GameObjectId,
                item.MeshObjectId,
                item.MaterialObjectId,
                item.SubMeshIndex,
                item.Topology ?? 0,
                item.IndexValues?.ToArray() ?? [],
                item.Positions,
                item.Normals,
                item.Tangents,
                item.Colors,
                item.Uv0,
                item.Uv1))
            .ToArray();

        return new ConverterScene(
            scene.SceneId,
            scene.Container.ContainerId,
            scene.Container.SourceType,
            nodes,
            primitives,
            scene.Warnings);
    }
}
