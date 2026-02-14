namespace UnityAssetParser.Classes;

/// <summary>
/// Represents a submesh within a Unity Mesh.
/// This is a verbatim port from UnityPy/classes/generated.py SubMesh.
/// 
/// A submesh defines a range of triangles that share a common material.
/// Meshes can have multiple submeshes for multi-material rendering.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/classes/generated.py
/// </summary>
public sealed class SubMesh
{
    /// <summary>
    /// Gets or sets the byte offset into the index buffer where this submesh's indices start.
    /// For Unity version &lt; 3.5, this is in bytes. For newer versions, divide by 2 (or 4 for 32-bit indices).
    /// </summary>
    public uint FirstByte { get; set; }

    /// <summary>
    /// Gets or sets the number of indices in this submesh.
    /// For triangles topology, this is typically a multiple of 3.
    /// </summary>
    public uint IndexCount { get; set; }

    /// <summary>
    /// Gets or sets the mesh topology type (triangles, triangle strip, quads, lines, etc.).
    /// </summary>
    public MeshTopology Topology { get; set; }

    /// <summary>
    /// Gets or sets the base vertex index offset for this submesh.
    /// Available in Unity version >= 4.
    /// </summary>
    public uint BaseVertex { get; set; }

    /// <summary>
    /// Gets or sets the first vertex index used by this submesh (for index range validation).
    /// Available in Unity version >= 4.
    /// </summary>
    public uint FirstVertex { get; set; }

    /// <summary>
    /// Gets or sets the number of vertices used by this submesh.
    /// Available in Unity version >= 4.
    /// </summary>
    public uint VertexCount { get; set; }

    /// <summary>
    /// Gets or sets the local bounding box of this submesh.
    /// Available in Unity version >= 3.5.
    /// </summary>
    public AABB? LocalAABB { get; set; }
}

/// <summary>
/// Axis-Aligned Bounding Box (AABB) for spatial bounds.
/// </summary>
public sealed class AABB
{
    /// <summary>
    /// Gets or sets the center point of the bounding box.
    /// </summary>
    public required Vector3f Center { get; init; }

    /// <summary>
    /// Gets or sets the extent (half-size) of the bounding box along each axis.
    /// </summary>
    public required Vector3f Extent { get; init; }
}

/// <summary>
/// 3D vector with float components.
/// </summary>
public readonly struct Vector3f
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    public Vector3f(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
/// <summary>
/// 4D vector with float components (used for tangents and bone weights).
/// </summary>
public readonly struct Vector4f
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float W { get; init; }

    public Vector4f(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }
}

/// <summary>
/// 2D vector with float components (used for UV texture coordinates).
/// </summary>
public readonly struct Vector2f
{
    public float U { get; init; }
    public float V { get; init; }

    public Vector2f(float u, float v)
    {
        U = u;
        V = v;
    }
}

/// <summary>
/// Color with RGBA channels as single-precision floats (0.0 to 1.0 range).
/// </summary>
public readonly struct ColorRGBA
{
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }
    public float A { get; init; }

    public ColorRGBA(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

/// <summary>
/// Per-bone axis-aligned bounding box (min and max corners).
/// </summary>
public sealed class MinMaxAABB
{
    /// <summary>
    /// Gets or sets the minimum corner of the bounding box.
    /// </summary>
    public required Vector3f Min { get; init; }

    /// <summary>
    /// Gets or sets the maximum corner of the bounding box.
    /// </summary>
    public required Vector3f Max { get; init; }
}