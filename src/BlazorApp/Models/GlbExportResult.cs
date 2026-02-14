namespace BlazorApp.Models;

/// <summary>
/// Result payload for GLB export, including basic geometry stats for diagnostics.
/// </summary>
public sealed class GlbExportResult
{
    public required byte[] GlbBytes { get; init; }

    public int VertexCount { get; init; }

    public int TriangleCount { get; init; }

    public int MeshCount { get; init; }
}
