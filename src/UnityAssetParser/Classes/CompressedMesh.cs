using UnityAssetParser.Helpers;

namespace UnityAssetParser.Classes;

/// <summary>
/// Represents compressed mesh data in Unity.
/// This is a verbatim port from UnityPy/classes/generated.py CompressedMesh.
/// 
/// CompressedMesh stores vertex attributes and indices in bit-packed format using PackedBitVector.
/// Used when mesh compression is enabled to reduce memory footprint.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/generated.py
/// </summary>
public sealed class CompressedMesh
{
    /// <summary>
    /// Gets or sets the compressed vertex positions (XYZ triplets).
    /// </summary>
    public required PackedBitVector Vertices { get; init; }

    /// <summary>
    /// Gets or sets the compressed UV coordinates.
    /// May contain multiple UV sets packed sequentially.
    /// </summary>
    public required PackedBitVector UV { get; init; }

    /// <summary>
    /// Gets or sets UV encoding information (bit flags describing UV layout).
    /// Available in newer Unity versions.
    /// </summary>
    public uint UVInfo { get; set; }

    /// <summary>
    /// Gets or sets the compressed normals (stored as XY, Z is reconstructed).
    /// </summary>
    public required PackedBitVector Normals { get; init; }

    /// <summary>
    /// Gets or sets the sign bits for normal Z component reconstruction.
    /// </summary>
    public required PackedBitVector NormalSigns { get; init; }

    /// <summary>
    /// Gets or sets the compressed tangents (stored as XY, ZW are reconstructed).
    /// </summary>
    public required PackedBitVector Tangents { get; init; }

    /// <summary>
    /// Gets or sets the sign bits for tangent Z and W component reconstruction.
    /// </summary>
    public required PackedBitVector TangentSigns { get; init; }

    /// <summary>
    /// Gets or sets the compressed bone weights (quantized to 0-31 range).
    /// </summary>
    public required PackedBitVector Weights { get; init; }

    /// <summary>
    /// Gets or sets the compressed bone indices.
    /// </summary>
    public required PackedBitVector BoneIndices { get; init; }

    /// <summary>
    /// Gets or sets the compressed triangle indices.
    /// </summary>
    public required PackedBitVector Triangles { get; init; }

    /// <summary>
    /// Gets or sets the compressed float colors (Unity 5.0+).
    /// Stored as normalized floats (0-1 range).
    /// Always present in the TypeTree (may be empty).
    /// </summary>
    public required PackedBitVector FloatColors { get; init; }

    /// <summary>
    /// Gets or sets the compressed vertex colors (optional, older Unity versions).
    /// Stored as packed RGBA integers.
    /// </summary>
    public PackedBitVector? Colors { get; init; }

    /// <summary>
    /// Gets or sets the compressed bind poses (Unity &lt; 5.0).
    /// 4x4 matrices for skeletal animation.
    /// </summary>
    public PackedBitVector? BindPoses { get; init; }
}
