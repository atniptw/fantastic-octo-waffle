namespace BlazorApp.Models;

/// <summary>Index entry for a file in a mod archive.</summary>
public sealed record FileIndexItem(
    string FileName, 
    long SizeBytes, 
    FileType Type, 
    bool Renderable
);
