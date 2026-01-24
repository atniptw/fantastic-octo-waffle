namespace BlazorApp.Models;

/// <summary>
/// State passed between ModDetail and Viewer3D pages.
/// Stores file index and metadata for cross-page navigation.
/// </summary>
public sealed class ModDetailState
{
    /// <summary>Gets or initializes the unique mod identifier (namespace_name).</summary>
    public required string ModId { get; init; }
    
    /// <summary>Gets or initializes the file index from ZIP processing.</summary>
    public required List<FileIndexItem> FileIndex { get; init; }
    
    /// <summary>Gets or initializes the package metadata.</summary>
    public required ThunderstorePackage Metadata { get; init; }
    
    /// <summary>Gets the timestamp when this state was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
