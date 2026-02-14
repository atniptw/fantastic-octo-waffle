using System.Collections.ObjectModel;

namespace BlazorApp.Models;

/// <summary>
/// Represents an indexed collection of files from a mod archive.
/// Provides convenience methods for querying renderable files.
/// </summary>
public sealed class FileIndex
{
    /// <summary>
    /// All indexed file items.
    /// </summary>
    public IReadOnlyList<FileIndexItem> Items { get; }

    /// <summary>
    /// Creates a new file index with the specified items.
    /// </summary>
    /// <param name="items">The file items to index.</param>
    public FileIndex(IEnumerable<FileIndexItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = new ReadOnlyCollection<FileIndexItem>(items.ToList());
    }

    /// <summary>
    /// Gets all files marked as renderable (contain Mesh objects).
    /// </summary>
    /// <returns>Enumerable of renderable files.</returns>
    public IEnumerable<FileIndexItem> GetRenderableFiles() 
        => Items.Where(x => x.Renderable);

    /// <summary>
    /// Gets all files that are not renderable.
    /// </summary>
    /// <returns>Enumerable of non-renderable files.</returns>
    public IEnumerable<FileIndexItem> GetNonRenderableFiles() 
        => Items.Where(x => !x.Renderable);

    /// <summary>
    /// Gets the count of renderable files.
    /// </summary>
    public int RenderableCount => Items.Count(x => x.Renderable);

    /// <summary>
    /// Gets the total number of indexed files.
    /// </summary>
    public int TotalCount => Items.Count;
}
