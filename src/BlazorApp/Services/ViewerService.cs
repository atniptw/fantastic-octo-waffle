using BlazorApp.Models;
using Microsoft.JSInterop;

namespace BlazorApp.Services;

/// <summary>
/// Implementation of 3D viewer service using Three.js via JavaScript interop.
/// </summary>
/// <remarks>
/// <para>This service provides C# bindings to the meshRenderer.js Three.js wrapper.
/// See wwwroot/js/meshRenderer.js for the JavaScript implementation.</para>
/// <para><strong>Thread Safety:</strong> This service is not thread-safe. Callers must ensure
/// that methods are not invoked concurrently from multiple threads.</para>
/// <para><strong>Lifecycle:</strong> This service is designed for single-use. After calling
/// <see cref="DisposeAsync"/>, the service cannot be reinitialized and a new instance
/// must be created.</para>
/// </remarks>
public class ViewerService : IViewerService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isInitialized = false;

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
        
        if (_isInitialized)
        {
            throw new InvalidOperationException("Viewer is already initialized. Cannot initialize multiple times.");
        }
        
        ct.ThrowIfCancellationRequested();

        // Call meshRenderer.init(canvasId, options)
        var options = new
        {
            fov = 60,
            near = 0.1,
            far = 1000,
            background = 0x1a1a1a
        };

        try
        {
            await _jsRuntime.InvokeVoidAsync("meshRenderer.init", ct, canvasId, options);
            _isInitialized = true;
        }
        catch
        {
            // Ensure state remains consistent if JavaScript call fails
            _isInitialized = false;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ShowAsync(ThreeJsGeometry geometry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ct.ThrowIfCancellationRequested();

        if (!_isInitialized)
        {
            throw new InvalidOperationException("Viewer not initialized. Call InitializeAsync first.");
        }

        // Prepare geometry data for JS interop
        // Note: Arrays are automatically marshaled to typed arrays by Blazor
        var geometryData = new
        {
            positions = geometry.Positions,
            indices = geometry.Indices,
            normals = geometry.Normals,
            uvs = geometry.Uvs
        };

        // Prepare groups if present
        object? groups = null;
        if (geometry.Groups != null && geometry.Groups.Count > 0)
        {
            groups = geometry.Groups.Select(g => new
            {
                start = g.Start,
                count = g.Count,
                materialIndex = g.MaterialIndex
            }).ToArray();
        }

        // Default material options
        var materialOpts = new
        {
            color = 0x888888,
            wireframe = false,
            metalness = 0.5,
            roughness = 0.5
        };

        // Call meshRenderer.loadMesh(geometry, groups, materialOpts)
        var meshId = await _jsRuntime.InvokeAsync<string>(
            "meshRenderer.loadMesh", ct, geometryData, groups, materialOpts);

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

        // Prepare material options (only include non-null values)
        var opts = new Dictionary<string, object>();
        if (color != null)
        {
            opts["color"] = color;
        }
        if (wireframe.HasValue)
        {
            opts["wireframe"] = wireframe.Value;
        }

        // Call meshRenderer.updateMaterial(meshId, opts)
        await _jsRuntime.InvokeVoidAsync("meshRenderer.updateMaterial", ct, meshId, opts);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Call meshRenderer.clear() to remove all meshes
        await _jsRuntime.InvokeVoidAsync("meshRenderer.clear", ct);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Call meshRenderer.dispose() to cleanup all resources
        await _jsRuntime.InvokeVoidAsync("meshRenderer.dispose", ct);
        _isInitialized = false;
    }
}
