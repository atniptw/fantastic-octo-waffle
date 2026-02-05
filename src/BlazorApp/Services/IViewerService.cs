using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Coordinates 3D viewer interaction via JS interop with Three.js.</summary>
public interface IViewerService
{
    /// <summary>
    /// Initialize Three.js viewer with canvas element.
    /// </summary>
    /// <param name="canvasId">ID of the canvas element.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Canvas not found or already initialized.</exception>
    Task InitializeAsync(string canvasId, CancellationToken ct = default);
    
    /// <summary>
    /// Load geometry into the viewer and display it.
    /// </summary>
    /// <param name="geometry">Geometry data from asset renderer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Mesh handle/ID for future updates.</returns>
    /// <exception cref="InvalidOperationException">Viewer not initialized.</exception>
    Task<string> ShowAsync(ThreeJsGeometry geometry, CancellationToken ct = default);
    
    /// <summary>
    /// Load GLB binary data into the viewer using Three.js GLTFLoader.
    /// More efficient than raw geometry for complex meshes with materials.
    /// </summary>
    /// <param name="glbData">GLB binary data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Mesh handle/ID for future updates.</returns>
    /// <exception cref="InvalidOperationException">Viewer not initialized or GLB load failed.</exception>
    Task<string> ShowGlbAsync(byte[] glbData, CancellationToken ct = default);
    
    /// <summary>
    /// Update material properties of a displayed mesh.
    /// </summary>
    /// <param name="meshId">Mesh handle from ShowAsync.</param>
    /// <param name="color">Hex color (e.g., "#FF0000").</param>
    /// <param name="wireframe">Enable wireframe mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Mesh not found.</exception>
    Task UpdateMaterialAsync(string meshId, string? color = null, bool? wireframe = null, CancellationToken ct = default);
    
    /// <summary>
    /// Clear all meshes from the viewer.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ClearAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Dispose viewer resources.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task DisposeAsync(CancellationToken ct = default);
}
