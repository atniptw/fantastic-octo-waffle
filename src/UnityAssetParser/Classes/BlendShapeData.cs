namespace UnityAssetParser.Classes;

/// <summary>
/// Represents blend shape (morph target) data for a Unity Mesh.
/// Introduced in Unity 4.3 to replace the older List&lt;MeshBlendShape&gt; format.
/// Reference: UnityPy/classes/generated.py - BlendShapeData
/// </summary>
public sealed class BlendShapeData
{
    /// <summary>
    /// Vertices affected by blend shapes with their delta values.
    /// </summary>
    public BlendShapeVertex[]? Vertices { get; set; }

    /// <summary>
    /// Individual blend shape definitions (maps to vertex ranges).
    /// </summary>
    public MeshBlendShape[]? Shapes { get; set; }

    /// <summary>
    /// Named channels that control blend shapes.
    /// </summary>
    public MeshBlendShapeChannel[]? Channels { get; set; }

    /// <summary>
    /// Full weight values for blend shapes.
    /// </summary>
    public float[]? FullWeights { get; set; }
}

/// <summary>
/// Vertex delta for blend shape animation.
/// </summary>
public sealed class BlendShapeVertex
{
    public Vector3f Vertex { get; set; }
    public Vector3f Normal { get; set; }
    public Vector3f Tangent { get; set; }
    public uint Index { get; set; }
}

/// <summary>
/// Individual blend shape (morph target) definition.
/// </summary>
public sealed class MeshBlendShape
{
    public uint FirstVertex { get; set; }
    public uint VertexCount { get; set; }
    public bool HasNormals { get; set; }
    public bool HasTangents { get; set; }
}

/// <summary>
/// Named channel for controlling blend shapes.
/// </summary>
public sealed class MeshBlendShapeChannel
{
    public string Name { get; set; } = string.Empty;
    public uint NameHash { get; set; }
    public int FrameIndex { get; set; }
    public int FrameCount { get; set; }
}
