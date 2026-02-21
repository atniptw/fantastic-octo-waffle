using System.Collections.Generic;
using System.Linq;

namespace UnityAssetParser;

public sealed class BaseAssetsContext
{
	public List<ParsedContainer> Containers { get; } = new();
	public List<SerializedFileInfo> SerializedFiles { get; } = new();
	public List<string> Warnings { get; } = new();
	public List<SemanticObjectInfo> SemanticObjects { get; } = new();
	public List<SemanticGameObjectInfo> SemanticGameObjects { get; } = new();
	public List<SemanticTransformInfo> SemanticTransforms { get; } = new();
	public List<SemanticMeshFilterInfo> SemanticMeshFilters { get; } = new();
	public List<SemanticMeshRendererInfo> SemanticMeshRenderers { get; } = new();
	public List<SemanticMeshInfo> SemanticMeshes { get; } = new();
	public List<SemanticMaterialInfo> SemanticMaterials { get; } = new();
	public List<SemanticTextureInfo> SemanticTextures { get; } = new();
	public List<SemanticAnchorPointInfo> SemanticAnchorPoints { get; } = new();
	public PackageInventory Inventory { get; } = new();

	public IReadOnlyDictionary<string, int> BuildSemanticObjectTypeCounts()
	{
		return SemanticObjects
			.GroupBy(item => item.TypeName)
			.OrderBy(group => group.Key)
			.ToDictionary(group => group.Key, group => group.Count());
	}
}

public enum ContainerKind
{
	Unknown,
	UnityPackageTar,
	UnityFs,
	UnityWeb,
	UnityRaw,
	SerializedFile
}

public sealed class ParsedContainer
{
	public ParsedContainer(string sourceName, ContainerKind kind, long size)
	{
		SourceName = sourceName;
		Kind = kind;
		Size = size;
	}

	public string SourceName { get; }
	public ContainerKind Kind { get; }
	public long Size { get; }
	public uint Version { get; init; }
	public string? UnityVersion { get; init; }
	public string? UnityRevision { get; init; }
	public List<ContainerEntry> Entries { get; } = new();
}

public sealed class ContainerEntry
{
	public ContainerEntry(string path, long offset, long size, uint flags)
	{
		Path = path;
		Offset = offset;
		Size = size;
		Flags = flags;
	}

	public string Path { get; }
	public long Offset { get; }
	public long Size { get; }
	public uint Flags { get; }
	public byte[]? Payload { get; set; }
}

public sealed class SerializedFileInfo
{
	public SerializedFileInfo(string sourceName)
	{
		SourceName = sourceName;
	}

	public string SourceName { get; }
	public uint Version { get; init; }
	public long FileSize { get; init; }
	public uint MetadataSize { get; init; }
	public long DataOffset { get; init; }
	public bool BigEndian { get; init; }
	public List<SerializedObjectInfo> Objects { get; } = new();
}

public sealed class SerializedObjectInfo
{
	public SerializedObjectInfo(long pathId, long byteStart, uint byteSize, int typeId, int? classId)
	{
		PathId = pathId;
		ByteStart = byteStart;
		ByteSize = byteSize;
		TypeId = typeId;
		ClassId = classId;
	}

	public long PathId { get; }
	public long ByteStart { get; }
	public uint ByteSize { get; }
	public int TypeId { get; }
	public int? ClassId { get; }
}

public sealed class SemanticObjectInfo
{
	public SemanticObjectInfo(long pathId, int? classId, string typeName)
	{
		PathId = pathId;
		ClassId = classId;
		TypeName = typeName;
	}

	public long PathId { get; }
	public int? ClassId { get; }
	public string TypeName { get; }
}

public sealed class SemanticGameObjectInfo
{
	public SemanticGameObjectInfo(long pathId, string name, bool isActive, int layer)
	{
		PathId = pathId;
		Name = name;
		IsActive = isActive;
		Layer = layer;
	}

	public long PathId { get; }
	public string Name { get; }
	public bool IsActive { get; }
	public int Layer { get; }
}

public sealed class SemanticTransformInfo
{
	public SemanticTransformInfo(
		long pathId,
		long gameObjectPathId,
		long? parentPathId,
		IReadOnlyList<long> childrenPathIds,
		SemanticVector3 localPosition,
		SemanticQuaternion localRotation,
		SemanticVector3 localScale)
	{
		PathId = pathId;
		GameObjectPathId = gameObjectPathId;
		ParentPathId = parentPathId;
		ChildrenPathIds = childrenPathIds;
		LocalPosition = localPosition;
		LocalRotation = localRotation;
		LocalScale = localScale;
	}

	public long PathId { get; }
	public long GameObjectPathId { get; }
	public long? ParentPathId { get; }
	public IReadOnlyList<long> ChildrenPathIds { get; }
	public SemanticVector3 LocalPosition { get; }
	public SemanticQuaternion LocalRotation { get; }
	public SemanticVector3 LocalScale { get; }
}

public readonly record struct SemanticVector3(float X, float Y, float Z);
public readonly record struct SemanticVector2(float X, float Y);
public readonly record struct SemanticVector4(float X, float Y, float Z, float W);
public readonly record struct SemanticQuaternion(float W, float X, float Y, float Z);

public sealed class SemanticMeshFilterInfo
{
	public SemanticMeshFilterInfo(long pathId, long gameObjectPathId, long meshPathId)
	{
		PathId = pathId;
		GameObjectPathId = gameObjectPathId;
		MeshPathId = meshPathId;
	}

	public long PathId { get; }
	public long GameObjectPathId { get; }
	public long MeshPathId { get; }
}

public sealed class SemanticMeshRendererInfo
{
	public SemanticMeshRendererInfo(long pathId, long gameObjectPathId, IReadOnlyList<long> materialPathIds)
	{
		PathId = pathId;
		GameObjectPathId = gameObjectPathId;
		MaterialPathIds = materialPathIds;
	}

	public long PathId { get; }
	public long GameObjectPathId { get; }
	public IReadOnlyList<long> MaterialPathIds { get; }
}

public sealed class SemanticMeshInfo
{
	public SemanticMeshInfo(
		long pathId,
		string name,
		SemanticBoundsInfo bounds,
		SemanticMeshChannelFlags channelFlags,
		int? indexFormat,
		IReadOnlyList<uint> decodedIndices,
		int vertexDataByteLength,
		IReadOnlyList<SemanticVector3> decodedPositions,
		IReadOnlyList<SemanticVector3> decodedNormals,
		IReadOnlyList<SemanticVector4> decodedTangents,
		IReadOnlyList<SemanticVector4> decodedColors,
		IReadOnlyList<SemanticVector2> decodedUv0,
		IReadOnlyList<SemanticVector2> decodedUv1,
		IReadOnlyList<SemanticVertexChannelInfo> vertexChannels,
		IReadOnlyList<SemanticVertexStreamInfo> vertexStreams,
		int indexElementSizeBytes,
		int indexElementCount,
		int indexCount,
		int subMeshCount,
		IReadOnlyList<SemanticSubMeshInfo> subMeshes,
		IReadOnlyList<int> topology,
		int vertexCount)
	{
		PathId = pathId;
		Name = name;
		Bounds = bounds;
		ChannelFlags = channelFlags;
		IndexFormat = indexFormat;
		DecodedIndices = decodedIndices;
		VertexDataByteLength = vertexDataByteLength;
		DecodedPositions = decodedPositions;
		DecodedNormals = decodedNormals;
		DecodedTangents = decodedTangents;
		DecodedColors = decodedColors;
		DecodedUv0 = decodedUv0;
		DecodedUv1 = decodedUv1;
		VertexChannels = vertexChannels;
		VertexStreams = vertexStreams;
		IndexElementSizeBytes = indexElementSizeBytes;
		IndexElementCount = indexElementCount;
		IndexCount = indexCount;
		SubMeshCount = subMeshCount;
		SubMeshes = subMeshes;
		Topology = topology;
		VertexCount = vertexCount;
	}

	public long PathId { get; }
	public string Name { get; }
	public SemanticBoundsInfo Bounds { get; }
	public SemanticMeshChannelFlags ChannelFlags { get; }
	public int? IndexFormat { get; }
	public IReadOnlyList<uint> DecodedIndices { get; }
	public int VertexDataByteLength { get; }
	public IReadOnlyList<SemanticVector3> DecodedPositions { get; }
	public IReadOnlyList<SemanticVector3> DecodedNormals { get; }
	public IReadOnlyList<SemanticVector4> DecodedTangents { get; }
	public IReadOnlyList<SemanticVector4> DecodedColors { get; }
	public IReadOnlyList<SemanticVector2> DecodedUv0 { get; }
	public IReadOnlyList<SemanticVector2> DecodedUv1 { get; }
	public IReadOnlyList<SemanticVertexChannelInfo> VertexChannels { get; }
	public IReadOnlyList<SemanticVertexStreamInfo> VertexStreams { get; }
	public int IndexElementSizeBytes { get; }
	public int IndexElementCount { get; }
	public int IndexCount { get; }
	public int SubMeshCount { get; }
	public IReadOnlyList<SemanticSubMeshInfo> SubMeshes { get; }
	public IReadOnlyList<int> Topology { get; }
	public int VertexCount { get; }
}

public sealed class SemanticMeshChannelFlags
{
	public SemanticMeshChannelFlags(bool positions, bool normals, bool tangents, bool colors, bool uv0, bool uv1)
	{
		Positions = positions;
		Normals = normals;
		Tangents = tangents;
		Colors = colors;
		Uv0 = uv0;
		Uv1 = uv1;
	}

	public bool Positions { get; }
	public bool Normals { get; }
	public bool Tangents { get; }
	public bool Colors { get; }
	public bool Uv0 { get; }
	public bool Uv1 { get; }
}

public sealed class SemanticVertexChannelInfo
{
	public SemanticVertexChannelInfo(int channelIndex, int stream, int offset, int format, int dimension)
	{
		ChannelIndex = channelIndex;
		Stream = stream;
		Offset = offset;
		Format = format;
		Dimension = dimension;
	}

	public int ChannelIndex { get; }
	public int Stream { get; }
	public int Offset { get; }
	public int Format { get; }
	public int Dimension { get; }
}

public sealed class SemanticVertexStreamInfo
{
	public SemanticVertexStreamInfo(int stream, int offset, int stride, int byteLength)
	{
		Stream = stream;
		Offset = offset;
		Stride = stride;
		ByteLength = byteLength;
	}

	public int Stream { get; }
	public int Offset { get; }
	public int Stride { get; }
	public int ByteLength { get; }
}

public sealed class SemanticSubMeshInfo
{
	public SemanticSubMeshInfo(int firstByte, int indexCount, int topology, int firstVertex, int vertexCount)
	{
		FirstByte = firstByte;
		IndexCount = indexCount;
		Topology = topology;
		FirstVertex = firstVertex;
		VertexCount = vertexCount;
	}

	public int FirstByte { get; }
	public int IndexCount { get; }
	public int Topology { get; }
	public int FirstVertex { get; }
	public int VertexCount { get; }
}

public sealed class SemanticBoundsInfo
{
	public SemanticBoundsInfo(SemanticVector3 center, SemanticVector3 extent)
	{
		Center = center;
		Extent = extent;
	}

	public SemanticVector3 Center { get; }
	public SemanticVector3 Extent { get; }
}

public sealed class SemanticMaterialInfo
{
	public SemanticMaterialInfo(long pathId, string name, long? shaderPathId, 
		float[]? baseColorFactor = null, float metallic = 0f, float roughness = 0.5f, string alphaMode = "OPAQUE")
	{
		PathId = pathId;
		Name = name;
		ShaderPathId = shaderPathId;
		BaseColorFactor = baseColorFactor ?? new[] { 1f, 1f, 1f, 1f }; // Default to white
		Metallic = metallic;
		Roughness = roughness;
		AlphaMode = alphaMode;
	}

	public long PathId { get; }
	public string Name { get; }
	public long? ShaderPathId { get; }
	public float[] BaseColorFactor { get; } // RGBA array
	public float Metallic { get; }
	public float Roughness { get; }
	public string AlphaMode { get; }
}

public sealed class SemanticTextureInfo
{
	public SemanticTextureInfo(long pathId, string name, int width, int height, int format, int mipCount)
	{
		PathId = pathId;
		Name = name;
		Width = width;
		Height = height;
		Format = format;
		MipCount = mipCount;
	}

	public long PathId { get; }
	public string Name { get; }
	public int Width { get; }
	public int Height { get; }
	public int Format { get; }
	public int MipCount { get; }
}

public sealed class SemanticAnchorPointInfo
{
	public SemanticAnchorPointInfo(long gameObjectPathId, string tag, string name)
	{
		GameObjectPathId = gameObjectPathId;
		Tag = tag;
		Name = name;
	}

	public long GameObjectPathId { get; }
	public string Tag { get; }
	public string Name { get; }
}

public sealed class PackageInventory
{
	public List<PackageEntryInfo> Entries { get; } = new();
	public List<PackageGameObjectInfo> GameObjects { get; } = new();
	public List<PackageTransformInfo> Transforms { get; } = new();
	public List<PackageMeshInfo> Meshes { get; } = new();
	public List<PackageMaterialInfo> Materials { get; } = new();
	public List<PackageTextureInfo> Textures { get; } = new();
}

public sealed class PackageEntryInfo
{
	public PackageEntryInfo(
		string containerSource,
		ContainerKind containerKind,
		string path,
		long size,
		uint flags,
		string? guid,
		string? resolvedPath)
	{
		ContainerSource = containerSource;
		ContainerKind = containerKind;
		Path = path;
		Size = size;
		Flags = flags;
		Guid = guid;
		ResolvedPath = resolvedPath;
	}

	public string ContainerSource { get; }
	public ContainerKind ContainerKind { get; }
	public string Path { get; }
	public long Size { get; }
	public uint Flags { get; }
	public string? Guid { get; }
	public string? ResolvedPath { get; }
}

public sealed class PackageGameObjectInfo
{
	public PackageGameObjectInfo(long pathId, string name, bool isActive, int layer)
	{
		PathId = pathId;
		Name = name;
		IsActive = isActive;
		Layer = layer;
	}

	public long PathId { get; }
	public string Name { get; }
	public bool IsActive { get; }
	public int Layer { get; }
}

public sealed class PackageTransformInfo
{
	public PackageTransformInfo(long pathId, long gameObjectPathId, long? parentPathId, int childCount)
	{
		PathId = pathId;
		GameObjectPathId = gameObjectPathId;
		ParentPathId = parentPathId;
		ChildCount = childCount;
	}

	public long PathId { get; }
	public long GameObjectPathId { get; }
	public long? ParentPathId { get; }
	public int ChildCount { get; }
}

public sealed class PackageMeshInfo
{
	public PackageMeshInfo(
		long pathId,
		string name,
		int vertexCount,
		int subMeshCount,
		SemanticMeshChannelFlags channelFlags)
	{
		PathId = pathId;
		Name = name;
		VertexCount = vertexCount;
		SubMeshCount = subMeshCount;
		ChannelFlags = channelFlags;
	}

	public long PathId { get; }
	public string Name { get; }
	public int VertexCount { get; }
	public int SubMeshCount { get; }
	public SemanticMeshChannelFlags ChannelFlags { get; }
}

public sealed class PackageMaterialInfo
{
	public PackageMaterialInfo(long pathId, string name, long? shaderPathId)
	{
		PathId = pathId;
		Name = name;
		ShaderPathId = shaderPathId;
	}

	public long PathId { get; }
	public string Name { get; }
	public long? ShaderPathId { get; }
}

public sealed class PackageTextureInfo
{
	public PackageTextureInfo(long pathId, string name, int width, int height, int format, int mipCount)
	{
		PathId = pathId;
		Name = name;
		Width = width;
		Height = height;
		Format = format;
		MipCount = mipCount;
	}

	public long PathId { get; }
	public string Name { get; }
	public int Width { get; }
	public int Height { get; }
	public int Format { get; }
	public int MipCount { get; }
}
