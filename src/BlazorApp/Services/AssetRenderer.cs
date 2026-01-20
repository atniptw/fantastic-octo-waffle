using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Stub implementation of asset renderer. Throws NotImplementedException for render operations.
/// </summary>
/// <remarks>
/// Temporary stub for development. Real implementation will be added in future issue.
/// </remarks>
public class AssetRenderer : IAssetRenderer
{
    /// <inheritdoc/>
    public async Task<ThreeJsGeometry> RenderAsync(FileIndexItem file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ct.ThrowIfCancellationRequested();

        await Task.CompletedTask;

        // TODO: [Future] Implement UnityPy-based parsing (see docs/UnityParsing.md)
        // Expected: Parse .hhh file (UnityFS bundle), extract Mesh objects (ClassID 43),
        // read vertex data from .resS resource, unpack PackedBitVector attributes,
        // convert to ThreeJsGeometry with positions, indices, normals, and UVs
        throw new NotImplementedException("Asset rendering not yet implemented. Full UnityPy parsing logic required.");
    }
}
