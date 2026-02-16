using System.Collections.Generic;

namespace UnityAssetParser;

public sealed class BaseAssetsContext
{
	public List<ParsedContainer> Containers { get; } = new();
	public List<SerializedFileInfo> SerializedFiles { get; } = new();
	public List<string> Warnings { get; } = new();
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
