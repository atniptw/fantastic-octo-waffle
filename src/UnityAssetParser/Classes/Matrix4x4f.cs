namespace UnityAssetParser.Classes;

/// <summary>
/// Represents a 4x4 single-precision floating-point matrix.
/// Used for bone transforms in skinned meshes (BindPose).
/// </summary>
public struct Matrix4x4f
{
    /// <summary>
    /// Gets or sets the matrix values as a 16-element array (column-major order).
    /// </summary>
    public float[]? Values { get; set; }
}
