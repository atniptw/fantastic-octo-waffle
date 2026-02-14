using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>Performs shallow scan of asset files to determine renderability.</summary>
public interface IAssetScanner
{
    /// <summary>
    /// Check if a file contains renderable Mesh objects without full parsing.
    /// </summary>
    /// <param name="file">File index item to scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if file contains any Mesh objects (ClassID 43).</returns>
    /// <exception cref="InvalidDataException">Corrupt or unsupported asset format.</exception>
    Task<bool> IsRenderableAsync(FileIndexItem file, CancellationToken ct = default);
}
