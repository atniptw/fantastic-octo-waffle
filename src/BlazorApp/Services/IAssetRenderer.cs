using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Parses Unity asset files and extracts Three.js-compatible geometry.</summary>
public interface IAssetRenderer
{
    /// <summary>
    /// Parse asset file and extract mesh geometry for Three.js rendering.
    /// </summary>
    /// <param name="file">File to render (must be marked as Renderable).</param>
    /// <param name="zipBytes">Raw ZIP bytes containing the asset file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Geometry data ready for Three.js.</returns>
    /// <exception cref="InvalidDataException">Corrupt asset or parsing error.</exception>
    /// <exception cref="InvalidOperationException">File is not renderable.</exception>
    Task<ThreeJsGeometry> RenderAsync(FileIndexItem file, byte[] zipBytes, CancellationToken ct = default);

    /// <summary>
    /// Renders geometry directly from a bundle file byte array (e.g., .hhh).
    /// </summary>
    Task<ThreeJsGeometry> RenderFromBundleAsync(FileIndexItem file, byte[] bundleBytes, CancellationToken ct = default);

    /// <summary>
    /// Parse asset file and export as GLB binary format.
    /// More efficient for complex meshes and supports materials/textures.
    /// </summary>
    /// <param name="file">File to render (must be marked as Renderable).</param>
    /// <param name="zipBytes">Raw ZIP bytes containing the asset file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>GLB binary data ready for Three.js GLTFLoader.</returns>
    /// <exception cref="InvalidDataException">Corrupt asset or parsing error.</exception>
    /// <exception cref="InvalidOperationException">File is not renderable.</exception>
    Task<GlbExportResult> RenderAsGlbAsync(FileIndexItem file, byte[] zipBytes, CancellationToken ct = default);

    /// <summary>
    /// Renders GLB directly from a bundle file byte array (e.g., .hhh).
    /// </summary>
    Task<GlbExportResult> RenderAsGlbFromBundleAsync(FileIndexItem file, byte[] bundleBytes, CancellationToken ct = default);
}
