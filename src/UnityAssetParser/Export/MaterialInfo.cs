using System.Numerics;

namespace UnityAssetParser.Export;

/// <summary>
/// Minimal material data for GLB export (base color + optional base color texture).
/// </summary>
public sealed class MaterialInfo
{
    public string Name { get; set; } = "material";

    public Vector4 BaseColor { get; set; } = new(1f, 1f, 1f, 1f);

    public TextureInfo? BaseColorTexture { get; set; }

    /// <summary>
    /// Optional reference to Texture2D PathID in the same SerializedFile.
    /// Used for linking after textures are decoded.
    /// </summary>
    public long? BaseColorTexturePathId { get; set; }
}
