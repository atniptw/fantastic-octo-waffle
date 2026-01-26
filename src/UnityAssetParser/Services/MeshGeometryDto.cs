namespace UnityAssetParser.Services;

/// <summary>
/// Data transfer object containing renderable mesh geometry suitable for Three.js interop.
/// This DTO is designed for efficient JS marshaling with typed arrays.
/// </summary>
public sealed class MeshGeometryDto
{
    /// <summary>
    /// Gets or sets the mesh name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vertex positions as a flat Float32 array.
    /// Format: [x0, y0, z0, x1, y1, z1, ...]
    /// Length: VertexCount * 3
    /// </summary>
    public float[] Positions { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Gets or sets the triangle indices.
    /// Format: Flat array of vertex indices forming triangles.
    /// Length: TriangleCount * 3
    /// </summary>
    public uint[] Indices { get; set; } = Array.Empty<uint>();

    /// <summary>
    /// Gets or sets the vertex normals as a flat Float32 array (optional).
    /// Format: [nx0, ny0, nz0, nx1, ny1, nz1, ...]
    /// Length: VertexCount * 3, or null if not present.
    /// </summary>
    public float[]? Normals { get; set; }

    /// <summary>
    /// Gets or sets the UV coordinates as a flat Float32 array (optional).
    /// Format: [u0, v0, u1, v1, ...]
    /// Length: VertexCount * 2, or null if not present.
    /// </summary>
    public float[]? UVs { get; set; }

    /// <summary>
    /// Gets or sets the submesh groups for multi-material support.
    /// Each group defines a range of indices and material index.
    /// </summary>
    public List<SubMeshGroup> Groups { get; set; } = new();

    /// <summary>
    /// Gets or sets the vertex count.
    /// </summary>
    public int VertexCount { get; set; }

    /// <summary>
    /// Gets or sets the triangle count.
    /// </summary>
    public int TriangleCount { get; set; }

    /// <summary>
    /// Gets or sets whether 16-bit indices are used (vs 32-bit).
    /// This helps the consumer choose between Uint16Array and Uint32Array in JS.
    /// </summary>
    public bool Use16BitIndices { get; set; }

    /// <summary>
    /// Represents a submesh group for multi-material rendering.
    /// Maps to Three.js BufferGeometry.groups API.
    /// </summary>
    public sealed class SubMeshGroup
    {
        /// <summary>
        /// Gets or sets the starting index in the index buffer.
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Gets or sets the number of indices in this group.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the material index for this submesh.
        /// </summary>
        public int MaterialIndex { get; set; }
    }
}
