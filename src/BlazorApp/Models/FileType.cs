namespace BlazorApp.Models;

/// <summary>Type of file found in mod archive.</summary>
public enum FileType
{
    Unknown = 0,
    UnityFS = 1,
    SerializedFile = 2,
    Resource = 3
}
