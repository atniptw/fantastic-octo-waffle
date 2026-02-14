namespace UnityAssetParser.Export;

/// <summary>
/// Decoded texture data (RGBA32) for GLB export.
/// </summary>
public sealed class TextureInfo
{
    public string Name { get; set; } = "texture";

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>
    /// Raw RGBA32 pixel data (length = Width * Height * 4).
    /// </summary>
    public byte[] Rgba32 { get; set; } = Array.Empty<byte>();
}
