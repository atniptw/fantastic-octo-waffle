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
}
