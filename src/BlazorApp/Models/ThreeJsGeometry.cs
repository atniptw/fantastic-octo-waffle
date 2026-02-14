namespace BlazorApp.Models;

/// <summary>Geometry data for Three.js rendering.</summary>
public sealed class ThreeJsGeometry
{
    public required float[] Positions { get; init; }
    public required uint[] Indices { get; init; }
    public float[]? Normals { get; init; }
    public float[]? Uvs { get; init; }
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
    public List<SubMeshGroup>? Groups { get; init; }
}

/// <summary>Submesh group for multi-material meshes.</summary>
public sealed class SubMeshGroup
{
    public required int Start { get; init; }
    public required int Count { get; init; }
    public required int MaterialIndex { get; init; }
}
