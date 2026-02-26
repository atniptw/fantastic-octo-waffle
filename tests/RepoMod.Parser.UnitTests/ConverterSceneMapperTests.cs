using RepoMod.Parser.Contracts;
using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class ConverterSceneMapperTests
{
    [Fact]
    public void Map_ProjectsDeterministicNodesAndPrimitives()
    {
        var scene = new ParsedModScene(
            "scene:1",
            new ContainerDescriptor("container:1", "/tmp/a.unitypackage", "unitypackage", "a.unitypackage"),
            [],
            [],
            [
                new UnityRenderObject(
                    "obj:2",
                    "asset:1",
                    null,
                    "gameobject",
                    "Avatar",
                    null,
                    ["obj:3"],
                    null,
                    [],
                    [],
                    null,
                    null,
                    null),
                new UnityRenderObject(
                    "obj:3",
                    "asset:1",
                    null,
                    "transform",
                    "AvatarTransform",
                    "obj:2",
                    [],
                    null,
                    [],
                    [],
                    [0f, 1f, 2f],
                    [0f, 0f, 0f, 1f],
                    [1f, 1f, 1f])
            ],
            [
                new UnityRenderPrimitive(
                    "primitive:b",
                    "asset:1",
                    "renderer:1",
                    "obj:2",
                    "mesh:1",
                    1,
                    "mat:2",
                    [0, 1, 2],
                    [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
                    [0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f],
                    null,
                    null,
                    [0f, 0f, 1f, 0f, 1f, 1f],
                    null,
                    null,
                    null,
                    0,
                    null,
                    null,
                    null),
                new UnityRenderPrimitive(
                    "primitive:a",
                    "asset:1",
                    "renderer:1",
                    "obj:2",
                    "mesh:1",
                    0,
                    "mat:1",
                    [0, 2, 1],
                    [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f],
                    null,
                    null,
                    null,
                    [0f, 0f, 1f, 0f, 0f, 1f],
                    null,
                    null,
                    null,
                    0,
                    null,
                    null,
                    null)
            ],
            [],
            [],
            [],
            [],
            [],
            new ParsedSceneGraph([], [], []),
            ["warning:sample"]);

        var mapped = ConverterSceneMapper.Map(scene);

        Assert.Equal("scene:1", mapped.SceneId);
        Assert.Equal("container:1", mapped.ContainerId);
        Assert.Equal("unitypackage", mapped.SourceType);

        Assert.Equal(2, mapped.Nodes.Count);
        Assert.Equal("obj:2", mapped.Nodes[0].NodeId);
        Assert.Equal("obj:3", mapped.Nodes[1].NodeId);

        Assert.Equal(2, mapped.Primitives.Count);
        Assert.Equal("primitive:a", mapped.Primitives[0].PrimitiveId);
        Assert.Equal("primitive:b", mapped.Primitives[1].PrimitiveId);

        Assert.Equal("mat:1", mapped.Primitives[0].MaterialObjectId);
        Assert.Equal("mesh:1", mapped.Primitives[0].MeshObjectId);
        Assert.Equal(new[] { 0, 2, 1 }, mapped.Primitives[0].Indices);
        Assert.NotNull(mapped.Primitives[0].Uv0);

        Assert.Equal("mat:2", mapped.Primitives[1].MaterialObjectId);
        Assert.NotNull(mapped.Primitives[1].Positions);
        Assert.NotNull(mapped.Primitives[1].Normals);

        Assert.Single(mapped.Warnings);
    }

    [Fact]
    public void MapWithDiagnostics_ReportsConverterReadinessIssues()
    {
        var scene = new ParsedModScene(
            "scene:diag",
            new ContainerDescriptor("container:diag", "/tmp/b.unitypackage", "unitypackage", "b.unitypackage"),
            [],
            [],
            [],
            [
                new UnityRenderPrimitive(
                    "primitive:diag",
                    "asset:1",
                    "renderer:1",
                    "node:1",
                    "mesh:1",
                    0,
                    null,
                    [0, 1, 2],
                    [0f, 0f, 0f, 1f, 0f, 0f],
                    null,
                    null,
                    null,
                    [0f, 0f, 1f],
                    null,
                    null,
                    null,
                    3,
                    null,
                    null,
                    null),
                new UnityRenderPrimitive(
                    "primitive:missing",
                    "asset:1",
                    "renderer:1",
                    "node:1",
                    "mesh:1",
                    1,
                    null,
                    [],
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0,
                    null,
                    null,
                    null)
            ],
            [],
            [],
            [],
            [],
            [],
            new ParsedSceneGraph([], [], []),
            []);

        var projection = ConverterSceneMapper.MapWithDiagnostics(scene);

        Assert.NotEmpty(projection.Diagnostics);
        Assert.Contains(projection.Diagnostics, item => item.Code == "UNSUPPORTED_TOPOLOGY");
        Assert.Contains(projection.Diagnostics, item => item.Code == "INDEX_OUT_OF_RANGE");
        Assert.Contains(projection.Diagnostics, item => item.Code == "INVALID_UV0_COMPONENTS");
        Assert.Contains(projection.Diagnostics, item => item.Code == "MISSING_INDICES");
    }
}
