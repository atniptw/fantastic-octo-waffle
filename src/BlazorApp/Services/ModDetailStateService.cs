using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// In-memory implementation of mod detail state service.
/// Stores state for navigation between ModDetail and Viewer3D pages.
/// </summary>
public sealed class ModDetailStateService : IModDetailStateService
{
    private ModDetailState? _currentState;

    /// <inheritdoc/>
    public Task SetCurrentModAsync(string modId, List<FileIndexItem> fileIndex, ThunderstorePackage metadata)
    {
        ArgumentNullException.ThrowIfNull(modId);
        ArgumentNullException.ThrowIfNull(fileIndex);
        ArgumentNullException.ThrowIfNull(metadata);

        _currentState = new ModDetailState
        {
            ModId = modId,
            FileIndex = fileIndex,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ModDetailState?> GetCurrentModAsync()
    {
        return Task.FromResult(_currentState);
    }

    /// <inheritdoc/>
    public Task ClearAsync()
    {
        _currentState = null;
        return Task.CompletedTask;
    }
}
