namespace UnityAssetParser.Classes;

/// <summary>
/// Mesh topology type enumeration.
/// This is a verbatim port from UnityPy/enums/MeshTopology.py.
/// 
/// Defines how triangle indices are interpreted when rendering the mesh.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/enums/MeshTopology.py
/// </summary>
public enum MeshTopology
{
    /// <summary>
    /// Triangle list: every 3 indices form one triangle (most common).
    /// </summary>
    Triangles = 0,

    /// <summary>
    /// Triangle strip: indices form a strip where each new index creates a triangle with the previous two.
    /// </summary>
    TriangleStrip = 1,

    /// <summary>
    /// Quad list: every 4 indices form a quad (converted to 2 triangles).
    /// </summary>
    Quads = 2,

    /// <summary>
    /// Line list: every 2 indices form a line segment.
    /// </summary>
    Lines = 3,

    /// <summary>
    /// Line strip: indices form a continuous line.
    /// </summary>
    LineStrip = 4,

    /// <summary>
    /// Point list: each index represents a single point.
    /// </summary>
    Points = 5
}
