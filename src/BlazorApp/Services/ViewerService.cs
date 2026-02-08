using BlazorApp.Models;
using Microsoft.JSInterop;
using System.Text.Json.Serialization;

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
        var options = new ViewerInitOptions
        {
            Fov = 60,
            Near = 0.1,
            Far = 1000,
            Background = 0x1a1a1a
        };

        try
        {
            await _jsRuntime.InvokeVoidAsync("meshRenderer.init", canvasId, options);
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
        var geometryData = new GeometryDto
        {
            Positions = geometry.Positions ?? Array.Empty<float>(),
            Indices = geometry.Indices ?? Array.Empty<uint>(),
            Normals = geometry.Normals,
            Uvs = geometry.Uvs
        };

        // Prepare groups if present
        GroupDto[]? groups = null;
        if (geometry.Groups != null && geometry.Groups.Count > 0)
        {
            groups = geometry.Groups.Select(g => new GroupDto
            {
                Start = g.Start,
                Count = g.Count,
                MaterialIndex = g.MaterialIndex
            }).ToArray();
        }

        // Default material options
        var materialOpts = new MaterialOptions
        {
            Color = 0x888888,
            Wireframe = false,
            Metalness = 0.5,
            Roughness = 0.5
        };

        // Call meshRenderer.loadMesh(geometry, groups, materialOpts)
        var meshId = await _jsRuntime.InvokeAsync<string>(
            "meshRenderer.loadMesh", ct, geometryData, groups, materialOpts);

        return meshId;
    }

    /// <inheritdoc/>
    public async Task<string> ShowGlbAsync(byte[] glbData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(glbData);
        ct.ThrowIfCancellationRequested();

        if (!_isInitialized)
        {
            throw new InvalidOperationException("Viewer not initialized. Call InitializeAsync first.");
        }

        if (glbData.Length == 0)
        {
            throw new ArgumentException("GLB data cannot be empty", nameof(glbData));
        }

        // Default material options
        var materialOpts = new MaterialOptions
        {
            Color = 0x888888,
            Wireframe = false,
            Metalness = 0.5,
            Roughness = 0.5
        };

        // Call meshRenderer.loadGLB(glbData, materialOpts)
        var meshId = await _jsRuntime.InvokeAsync<string>(
            "meshRenderer.loadGLB", ct, glbData, materialOpts);

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
        var opts = new MaterialUpdateOptions
        {
            Color = color,
            Wireframe = wireframe
        };

        // Call meshRenderer.updateMaterial(meshId, opts)
        await _jsRuntime.InvokeVoidAsync("meshRenderer.updateMaterial", meshId, opts);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Call meshRenderer.clear() to remove all meshes
        await _jsRuntime.InvokeVoidAsync("meshRenderer.clear");
    }

    /// <inheritdoc/>
    public async Task DisposeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Call meshRenderer.dispose() to cleanup all resources
        await _jsRuntime.InvokeVoidAsync("meshRenderer.dispose");
        _isInitialized = false;
    }

    private sealed class ViewerInitOptions
    {
        [JsonPropertyName("fov")]
        public int Fov { get; set; }

        [JsonPropertyName("near")]
        public double Near { get; set; }

        [JsonPropertyName("far")]
        public int Far { get; set; }

        [JsonPropertyName("background")]
        public int Background { get; set; }
    }

    private sealed class MaterialOptions
    {
        [JsonPropertyName("color")]
        public int Color { get; set; }

        [JsonPropertyName("wireframe")]
        public bool Wireframe { get; set; }

        [JsonPropertyName("metalness")]
        public double Metalness { get; set; }

        [JsonPropertyName("roughness")]
        public double Roughness { get; set; }
    }

    private sealed class MaterialUpdateOptions
    {
        [JsonPropertyName("color")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Color { get; set; }

        [JsonPropertyName("wireframe")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Wireframe { get; set; }
    }

    private sealed class GeometryDto
    {
        [JsonPropertyName("positions")]
        public float[] Positions { get; set; } = Array.Empty<float>();

        [JsonPropertyName("indices")]
        public uint[] Indices { get; set; } = Array.Empty<uint>();

        [JsonPropertyName("normals")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float[]? Normals { get; set; }

        [JsonPropertyName("uvs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float[]? Uvs { get; set; }
    }

    private sealed class GroupDto
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("materialIndex")]
        public int MaterialIndex { get; set; }
    }
}
