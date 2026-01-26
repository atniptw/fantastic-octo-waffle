using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Manages cross-page state for mod preview workflow.
/// Stores file index and metadata when navigating from ModDetail to Viewer3D.
/// </summary>
public interface IModDetailStateService
{
    /// <summary>
    /// Store current mod's file index and metadata for Viewer3D access.
    /// </summary>
    /// <param name="modId">Unique mod identifier (namespace_name).</param>
    /// <param name="fileIndex">List of indexed files from the mod.</param>
    /// <param name="metadata">Package metadata from Thunderstore.</param>
    /// <param name="zipBytes">Raw ZIP bytes for asset extraction.</param>
    Task SetCurrentModAsync(string modId, List<FileIndexItem> fileIndex, ThunderstorePackage metadata, byte[] zipBytes);
    
    /// <summary>
    /// Retrieve stored mod state (if available).
    /// </summary>
    /// <returns>The stored mod state, or null if not available.</returns>
    Task<ModDetailState?> GetCurrentModAsync();
    
    /// <summary>
    /// Clear stored mod state (cleanup).
    /// </summary>
    Task ClearAsync();
}
