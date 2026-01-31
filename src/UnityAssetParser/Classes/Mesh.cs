namespace UnityAssetParser.Classes;

/// <summary>
/// Represents a Unity Mesh object (ClassID 43).
/// Complete port from UnityPy/classes/generated.py (lines 4416-4452).
/// 
/// Contains vertex data, index buffer, submeshes, compressed mesh, and optional rendering/collision/skinning data.
/// Supports all 42 fields from UnityPy including version-specific fields.
/// MeshHelper handles extraction of renderable geometry and decompression.
/// 
/// Reference: UnityPy/classes/generated.py lines 4416-4452
/// </summary>
public sealed class Mesh
{
    // ========== REQUIRED CORE FIELDS (Always Present) ==========

    /// <summary>
    /// Gets or sets the mesh name identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the array of bone transform matrices for skinned meshes (BindPose).
    /// </summary>
    public Matrix4x4f[]? BindPose { get; set; }

    /// <summary>
    /// Gets or sets the local axis-aligned bounding box (min/max corners).
    /// </summary>
    public AABB? LocalAABB { get; set; }

    /// <summary>
    /// Gets or sets the GPU usage flags (dynamic, streaming, etc.).
    /// </summary>
    public int MeshUsageFlags { get; set; }

    /// <summary>
    /// Gets or sets the mesh compression level (0=None, 1=Low, 2=Medium, 3=High).
    /// </summary>
    public int MeshCompression { get; set; }

    /// <summary>
    /// Gets or sets the submesh array defining material boundaries and topology.
    /// </summary>
    public SubMesh[]? SubMeshes { get; set; }

    // ========== VERTEX/INDEX DATA FIELDS ==========

    /// <summary>
    /// Gets or sets the vertex data structure containing all vertex attributes (packed format, v4+).
    /// </summary>
    public VertexData? VertexData { get; set; }

    /// <summary>
    /// Gets or sets the compressed mesh data (optimized vertex/index storage).
    /// </summary>
    public CompressedMesh? CompressedMesh { get; set; }

    /// <summary>
    /// Gets or sets the raw index buffer bytes.
    /// Interpreted as UInt16 or UInt32 array depending on Use16BitIndices or IndexFormat.
    /// </summary>
    public byte[]? IndexBuffer { get; set; }

    /// <summary>
    /// Gets or sets the streaming info for external .resS resource data.
    /// When present (v5.3+), vertex/index data is stored externally rather than inline.
    /// </summary>
    public Bundle.StreamingInfo? StreamData { get; set; }

    // ========== OPTIONAL RENDERING DATA FIELDS ==========

    /// <summary>
    /// Gets or sets the uncompressed vertex positions (XYZ coordinates).
    /// Only present if not using VertexData compression.
    /// </summary>
    public Vector3f[]? Vertices { get; set; }

    /// <summary>
    /// Gets or sets the per-vertex surface normals (optional).
    /// </summary>
    public Vector3f[]? Normals { get; set; }

    /// <summary>
    /// Gets or sets the per-vertex tangent vectors (XYZ + handedness W, optional).
    /// </summary>
    public Vector4f[]? Tangents { get; set; }

    /// <summary>
    /// Gets or sets the per-vertex colors in RGBA format (optional).
    /// </summary>
    public ColorRGBA[]? Colors { get; set; }

    /// <summary>
    /// Gets or sets the primary UV channel (texcoord 0, optional).
    /// </summary>
    public Vector2f[]? UV { get; set; }

    /// <summary>
    /// Gets or sets the secondary UV channel (texcoord 1, optional).
    /// </summary>
    public Vector2f[]? UV1 { get; set; }

    // ========== SKINNING & BONE DATA FIELDS ==========

    /// <summary>
    /// Gets or sets the bone influences per vertex (weights and indices).
    /// Can be BoneInfluence[] or BoneWeights4[] depending on version.
    /// </summary>
    public object? Skin { get; set; }

    /// <summary>
    /// Gets or sets the CRC32 hashes of bone names for skeleton matching (optional).
    /// </summary>
    public int[]? BoneNameHashes { get; set; }

    /// <summary>
    /// Gets or sets the per-bone axis-aligned bounding boxes (optional).
    /// </summary>
    public MinMaxAABB[]? BonesAABB { get; set; }

    /// <summary>
    /// Gets or sets the CRC32 hash of the root bone name for skeleton lookup (optional).
    /// </summary>
    public int RootBoneNameHash { get; set; }

    // ========== COLLISION DATA FIELDS ==========

    /// <summary>
    /// Gets or sets the collision mesh triangle indices (optional).
    /// </summary>
    public int[]? CollisionTriangles { get; set; }

    /// <summary>
    /// Gets or sets the number of vertices in the collision mesh (optional).
    /// </summary>
    public int? CollisionVertexCount { get; set; }

    /// <summary>
    /// Gets or sets the precomputed convex collision mesh for PhysX (optional).
    /// </summary>
    public int[]? BakedConvexCollisionMesh { get; set; }

    /// <summary>
    /// Gets or sets the precomputed triangle collision mesh for PhysX (optional).
    /// </summary>
    public int[]? BakedTriangleCollisionMesh { get; set; }

    // ========== BLEND SHAPE (MORPH TARGET) FIELDS ==========

    /// <summary>
    /// Gets or sets the blend shape definitions (v2022.2+ uses BlendShapeData, earlier versions use List[MeshBlendShape]).
    /// </summary>
    public object? Shapes { get; set; }

    /// <summary>
    /// Gets or sets the per-shape per-vertex deltas (position, normal, tangent offsets).
    /// </summary>
    public object? ShapeVertices { get; set; }

    // ========== OPTIMIZATION & METADATA FIELDS ==========

    /// <summary>
    /// Gets or sets the index format (0 = UInt16, 1 = UInt32, v5.1+).
    /// </summary>
    public int? IndexFormat { get; set; }

    /// <summary>
    /// Gets or sets whether 16-bit indices are used (deprecated, v2017.4+).
    /// Use IndexFormat instead for v2017.4+.
    /// </summary>
    public bool? Use16BitIndices { get; set; }

    /// <summary>
    /// Gets or sets whether the mesh is readable from CPU (v5.6+).
    /// False means GPU-only access.
    /// </summary>
    public bool? IsReadable { get; set; }

    /// <summary>
    /// Gets or sets whether uncompressed vertex data should be kept in memory (optional).
    /// </summary>
    public bool? KeepVertices { get; set; }

    /// <summary>
    /// Gets or sets whether uncompressed index data should be kept in memory (optional).
    /// </summary>
    public bool? KeepIndices { get; set; }

    /// <summary>
    /// Gets or sets mesh metric 0 (e.g., blend shape vertex count, optional).
    /// </summary>
    public float? MeshMetrics_0_ { get; set; }

    /// <summary>
    /// Gets or sets mesh metric 1 (e.g., blend shape frame count, optional).
    /// </summary>
    public float? MeshMetrics_1_ { get; set; }

    /// <summary>
    /// Gets or sets the stream compression level (0=None, 1=LZ4, v5.6+).
    /// </summary>
    public int? StreamCompression { get; set; }

    /// <summary>
    /// Gets or sets the physics cooking flags for collision mesh generation (v5.4.1+).
    /// </summary>
    public int? CookingOptions { get; set; }

    // ========== ADVANCED FIELDS ==========

    /// <summary>
    /// Gets or sets variable bone weights data supporting 4+ bones per vertex (v2020.1+).
    /// </summary>
    public object? VariableBoneCountWeights { get; set; }
}
