using BlazorApp.Models;
using Microsoft.JSInterop;

namespace BlazorApp.Services;

/// <summary>
/// Implementation of 3D viewer service that interops with Three.js meshRenderer.
/// </summary>
public class ViewerService : IViewerService
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewerService"/> class.
    /// </summary>
    /// <param name="jsRuntime">JavaScript runtime for interop with Three.js.</param>
    /// <exception cref="ArgumentNullException">Thrown when jsRuntime is null.</exception>
    public ViewerService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(string canvasId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(canvasId))
        {
            throw new ArgumentException("Canvas ID cannot be empty or whitespace", nameof(canvasId));
        }
        ct.ThrowIfCancellationRequested();

        await _jsRuntime.InvokeVoidAsync("meshRenderer.init", ct, canvasId, new { });
    }

    /// <inheritdoc/>
    public async Task<string> ShowAsync(ThreeJsGeometry geometry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ct.ThrowIfCancellationRequested();

        var geometryData = new
        {
            positions = geometry.Positions,
            indices = geometry.Indices,
            normals = geometry.Normals,
            uvs = geometry.Uvs,
            vertexCount = geometry.VertexCount,
            triangleCount = geometry.TriangleCount
        };

        var meshId = await _jsRuntime.InvokeAsync<string>("meshRenderer.loadMesh", ct, geometryData, null, new { });
        return meshId;
    }

    /// <inheritdoc/>
    public async Task UpdateMaterialAsync(string meshId, string? color = null, bool? wireframe = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meshId))
        {
            throw new ArgumentException("Mesh ID cannot be empty or whitespace", nameof(meshId));
        }
        ct.ThrowIfCancellationRequested();

        var materialOpts = new Dictionary<string, object?>();
        if (color != null) materialOpts["color"] = color;
        if (wireframe.HasValue) materialOpts["wireframe"] = wireframe.Value;

        await _jsRuntime.InvokeVoidAsync("meshRenderer.updateMaterial", ct, meshId, materialOpts);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _jsRuntime.InvokeVoidAsync("meshRenderer.clear", ct);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _jsRuntime.InvokeVoidAsync("meshRenderer.dispose", ct);
    }
}
