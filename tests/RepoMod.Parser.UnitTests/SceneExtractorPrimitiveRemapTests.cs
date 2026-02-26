using System.Reflection;
using RepoMod.Parser.Contracts;
using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class SceneExtractorPrimitiveRemapTests
{
    [Fact]
    public void BuildPrimitiveIndexValues_UsesFirstByteCountAndBaseVertex_For16BitIndices()
    {
        var mesh = CreateMesh(indexFormat: 0, indexValues: [0, 1, 2, 3, 4, 5]);
        var subMesh = new UnityRenderSubMesh(
            0,
            FirstByte: 4,
            IndexCount: 3,
            Topology: 0,
            BaseVertex: 10,
            FirstVertex: null,
            VertexCount: null);

        var sliced = InvokeBuildPrimitiveIndexValues(mesh, subMesh);

        Assert.Equal(new[] { 12, 13, 14 }, sliced);
    }

    [Fact]
    public void BuildPrimitiveIndexValues_UsesFirstByteCountAndBaseVertex_For32BitIndices()
    {
        var mesh = CreateMesh(indexFormat: 1, indexValues: [7, 8, 9, 10, 11]);
        var subMesh = new UnityRenderSubMesh(
            0,
            FirstByte: 8,
            IndexCount: 2,
            Topology: 0,
            BaseVertex: -2,
            FirstVertex: null,
            VertexCount: null);

        var sliced = InvokeBuildPrimitiveIndexValues(mesh, subMesh);

        Assert.Equal(new[] { 7, 8 }, sliced);
    }

    [Fact]
    public void BuildPrimitiveGeometry_RemapsIndicesAndCopiesVertexSlices()
    {
        var positions = new float[]
        {
            0f, 0f, 0f,   // v0
            1f, 0f, 0f,   // v1
            2f, 0f, 0f,   // v2
            3f, 0f, 0f,   // v3
            4f, 0f, 0f    // v4
        };

        var mesh = CreateMesh(
            indexFormat: 0,
            indexValues: [3, 4, 3, 2],
            positions: positions,
            uv0: [0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f, 0.5f, 0.5f]);

        var geometry = InvokeBuildPrimitiveGeometry(mesh, null);
        var localIndices = GetProperty<IReadOnlyList<int>>(geometry, "IndexValues");
        var localPositions = GetProperty<IReadOnlyList<float>>(geometry, "Positions");

        Assert.Equal(new[] { 0, 1, 0, 2 }, localIndices);
        Assert.Equal(new[]
        {
            3f, 0f, 0f,   // source v3
            4f, 0f, 0f,   // source v4
            2f, 0f, 0f    // source v2
        }, localPositions);
    }

    private static UnityRenderMesh CreateMesh(
        int? indexFormat,
        IReadOnlyList<int>? indexValues,
        IReadOnlyList<float>? positions = null,
        IReadOnlyList<float>? normals = null,
        IReadOnlyList<float>? tangents = null,
        IReadOnlyList<float>? colors = null,
        IReadOnlyList<float>? uv0 = null,
        IReadOnlyList<float>? uv1 = null)
    {
        return new UnityRenderMesh(
            "mesh:1",
            "asset:1",
            "mesh",
            null,
            null,
            null,
            null,
            null,
            null,
            indexFormat,
            null,
            indexValues,
            positions,
            normals,
            tangents,
            colors,
            uv0,
            uv1,
            []);
    }

    private static IReadOnlyList<int>? InvokeBuildPrimitiveIndexValues(UnityRenderMesh mesh, UnityRenderSubMesh? subMesh)
    {
        var method = typeof(SceneExtractor).GetMethod("BuildPrimitiveIndexValues", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (IReadOnlyList<int>?)method.Invoke(null, [mesh, subMesh]);
    }

    private static object InvokeBuildPrimitiveGeometry(UnityRenderMesh mesh, UnityRenderSubMesh? subMesh)
    {
        var method = typeof(SceneExtractor).GetMethod("BuildPrimitiveGeometry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var value = method.Invoke(null, [mesh, subMesh]);
        Assert.NotNull(value);
        return value;
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        var value = property.GetValue(instance);
        Assert.NotNull(value);
        return (T)value;
    }
}
