using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Stub implementation of asset scanner. Always returns false for renderability checks.
/// </summary>
/// <remarks>
/// Temporary stub for development. Real implementation will be added in future issue.
/// </remarks>
public class AssetScanner : IAssetScanner
{
    /// <inheritdoc/>
    public async Task<bool> IsRenderableAsync(FileIndexItem file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ct.ThrowIfCancellationRequested();

        // TODO: [Future] Implement shallow UnityFS bundle parsing
        // Expected: Parse bundle header, scan SerializedFile for Mesh objects (ClassID 43),
        // return true if any Mesh objects found without fully parsing geometry data
        await Task.CompletedTask;
        return false;
    }
}
