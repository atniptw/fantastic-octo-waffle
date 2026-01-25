using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a fully parsed SerializedFile containing Unity object metadata.
/// Provides access to object table, type tree, and object data region.
/// </summary>
public sealed class SerializedFile
{
    private readonly Dictionary<long, ObjectInfo> _objectsByPathId;
    private readonly ReadOnlyMemory<byte> _objectDataRegion;

    /// <summary>
    /// Gets the parsed header metadata.
    /// </summary>
    public SerializedFileHeader Header { get; }

    /// <summary>
    /// Gets the type tree containing type metadata.
    /// </summary>
    public TypeTree TypeTree { get; }

    /// <summary>
    /// Gets the list of objects in this file.
    /// </summary>
    public IReadOnlyList<ObjectInfo> Objects { get; }

    /// <summary>
    /// Gets the list of external file references.
    /// </summary>
    public IReadOnlyList<FileIdentifier> Externals { get; }

    /// <summary>
    /// Gets the raw object data region for downstream readers.
    /// </summary>
    public ReadOnlyMemory<byte> ObjectDataRegion => _objectDataRegion;

    private SerializedFile(
        SerializedFileHeader header,
        TypeTree typeTree,
        IReadOnlyList<ObjectInfo> objects,
        IReadOnlyList<FileIdentifier> externals,
        ReadOnlyMemory<byte> objectDataRegion)
    {
        Header = header;
        TypeTree = typeTree;
        Objects = objects;
        Externals = externals;
        _objectDataRegion = objectDataRegion;

        // Build PathId lookup dictionary
        _objectsByPathId = new Dictionary<long, ObjectInfo>(objects.Count);
        foreach (var obj in objects)
        {
            if (!_objectsByPathId.TryAdd(obj.PathId, obj))
            {
                throw new DuplicatePathIdException(
                    $"Duplicate PathId {obj.PathId} detected in object table", obj.PathId);
            }
        }
    }

    /// <summary>
    /// Parses a SerializedFile from raw bytes.
    /// </summary>
    /// <param name="data">The raw SerializedFile data.</param>
    /// <returns>Parsed SerializedFile instance.</returns>
    /// <exception cref="InvalidVersionException">Unsupported format version.</exception>
    /// <exception cref="CorruptedHeaderException">Invalid header data.</exception>
    /// <exception cref="TruncatedMetadataException">Metadata shorter than expected.</exception>
    /// <exception cref="InvalidObjectInfoException">Invalid object metadata.</exception>
    /// <exception cref="DuplicatePathIdException">Duplicate PathID detected.</exception>
    public static SerializedFile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
        {
            throw new CorruptedHeaderException("File too small to contain SerializedFile header");
        }

        // Materialize a single backing buffer once and reuse it
        var buffer = data.ToArray();

        using var stream = new MemoryStream(buffer, false);
        using var reader = new BinaryReader(stream);

        // Step 1: Parse header (determines version and endianness)
        var header = ParseHeader(reader);

        // Validate header consistency
        ValidateHeader(header, buffer.Length);

        // Step 2: Create endian-aware reader
        bool isBigEndian = header.Endianness == 1;
        stream.Position = 0; // Reset to beginning
        using var endianReader = new EndianBinaryReader(stream, isBigEndian);

        // Re-parse header with endian reader to skip past it
        SkipHeader(endianReader, header.Version);

        // Step 3: Parse metadata region
        bool enableTypeTree = header.EnableTypeTree ?? true; // Default true for version >= 14
        
        // Read Unity version string if version < 9
        if (header.Version < 9)
        {
            header.UnityVersionString = endianReader.ReadUtf8NullTerminated();
        }

        // Read metadata fields for version >= 9 < 14
        if (header.Version >= 9 && header.Version < 14)
        {
            header.TargetPlatform = endianReader.ReadUInt32();
        }

        // Read EnableTypeTree flag for version >= 7 < 14
        if (header.Version >= 7 && header.Version < 14)
        {
            enableTypeTree = endianReader.ReadBoolean();
            header.EnableTypeTree = enableTypeTree;
        }

        // Step 4: Parse type tree
        var typeTree = ParseTypeTree(endianReader, header.Version, enableTypeTree);

        // Step 5: Align and parse object table
        endianReader.Align(4);
        var objects = ParseObjectTable(endianReader, header.Version);

        // Step 6: Resolve ClassIds for objects
        ResolveClassIds(objects, typeTree);

        // Step 7: Align and parse file identifiers (externals)
        endianReader.Align(4);
        var externals = ParseFileIdentifiers(endianReader, header.Version);

        // Step 8: Extract object data region
        var dataStartOffset = (int)header.DataOffset;
        if (dataStartOffset < 0 || dataStartOffset > buffer.Length)
        {
            throw new CorruptedHeaderException(
                $"Invalid DataOffset {header.DataOffset}: exceeds file size {buffer.Length}");
        }

        // Bound the object data region using the serialized file size from the header.
        // This keeps all downstream offsets and sizes consistent with the header metadata.
        var declaredFileSize = header.FileSize;
        if (declaredFileSize < 0)
        {
            throw new CorruptedHeaderException($"Invalid FileSize {header.FileSize}: must be non-negative");
        }

        // Clamp end-of-file index to the actual buffer length to avoid overruns if the header is inconsistent.
        var fileSizeEnd = (int)Math.Min(declaredFileSize, buffer.Length);
        if (fileSizeEnd < dataStartOffset)
        {
            throw new CorruptedHeaderException(
                $"Invalid DataOffset {header.DataOffset} and FileSize {header.FileSize}: data region is empty or negative");
        }

        var objectDataRegionLength = fileSizeEnd - dataStartOffset;
        var objectDataRegion = new ReadOnlyMemory<byte>(buffer, dataStartOffset, objectDataRegionLength);

        // Step 9: Validate object bounds
        ValidateObjectBounds(objects, objectDataRegion.Length, header.DataOffset);

        return new SerializedFile(header, typeTree, objects, externals, objectDataRegion);
    }

    /// <summary>
    /// Attempts to parse a SerializedFile with error handling.
    /// </summary>
    /// <param name="data">The raw SerializedFile data.</param>
    /// <param name="result">The parsed SerializedFile if successful.</param>
    /// <param name="error">Error message if parsing failed.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out SerializedFile? result, out string? error)
    {
        try
        {
            result = Parse(data);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Finds an object by its PathId.
    /// </summary>
    /// <param name="pathId">The PathId to search for.</param>
    /// <returns>ObjectInfo if found, null otherwise.</returns>
    public ObjectInfo? FindObject(long pathId)
    {
        return _objectsByPathId.GetValueOrDefault(pathId);
    }

    /// <summary>
    /// Gets all objects with a specific ClassId.
    /// </summary>
    /// <param name="classId">The ClassId to filter by (e.g., 43 for Mesh).</param>
    /// <returns>Enumerable of matching objects.</returns>
    public IEnumerable<ObjectInfo> GetObjectsByClassId(int classId)
    {
        return Objects.Where(obj => obj.ClassId == classId);
    }

    /// <summary>
    /// Reads object data for a specific object.
    /// </summary>
    /// <param name="obj">The object to read data for.</param>
    /// <returns>Raw object data bytes.</returns>
    /// <exception cref="ArgumentNullException">Object is null.</exception>
    /// <exception cref="InvalidObjectInfoException">Object bounds are invalid.</exception>
    public ReadOnlyMemory<byte> ReadObjectData(ObjectInfo obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj.ByteStart < 0 || obj.ByteStart + obj.ByteSize > _objectDataRegion.Length)
        {
            throw new InvalidObjectInfoException(
                $"Object {obj.PathId} bounds invalid: ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}, " +
                $"DataRegion.Length={_objectDataRegion.Length}", obj.PathId);
        }

        return _objectDataRegion.Slice((int)obj.ByteStart, (int)obj.ByteSize);
    }

    // Parsing helper methods follow...
    private static SerializedFileHeader ParseHeader(BinaryReader reader)
    {
        var header = new SerializedFileHeader();

        // NOTE: The SerializedFile header itself is always little-endian in Unity's format.
        // The Endianness byte only affects the metadata and object data that follows.
        // This matches UnityPy's implementation behavior.

        // Read first 4 bytes as MetadataSize
        header.MetadataSize = reader.ReadUInt32();

        // Read next 4 bytes 
        uint value1 = reader.ReadUInt32();
        
        // Read following 4 bytes
        uint value2 = reader.ReadUInt32();

        // Determine format based on heuristics:
        // Version 22+: MetadataSize, FileSize(int64=8bytes), Version, DataOffset(int64)
        // Version < 22: MetadataSize, FileSize(uint32=4bytes), Version, DataOffset(uint32)
        
        // Key insight: For version >= 22, if FileSize < 4GB, the high 32 bits will be 0
        // So if value2 is 0 and value1 is reasonable, we're likely reading int64 FileSize
        // Then we need to read the Version next
        
        // But this is still ambiguous. Better approach: Check if value2 is a reasonable version number (14-30)
        // AND value1 is a reasonable file size (> metadata size, < 1GB)
        
        if (value2 >= 14 && value2 <= 30 && value1 >= header.MetadataSize && value1 < 1_000_000_000)
        {
            // Version < 22 format: value1=FileSize(uint32), value2=Version
            header.FileSize = value1;
            header.Version = value2;
            header.DataOffset = reader.ReadUInt32();
        }
        else
        {
            // Version >= 22 format: value1+value2 = FileSize(int64)
            header.FileSize = (long)value1 | ((long)value2 << 32);
            header.Version = reader.ReadUInt32();
            header.DataOffset = reader.ReadInt64();
        }

        // Read Endianness
        header.Endianness = reader.ReadByte();

        // Validate endianness
        if (header.Endianness > 1)
        {
            throw new EndiannessMismatchException(
                $"Invalid endianness value: {header.Endianness} (expected 0 or 1)", header.Endianness);
        }

        // Read Reserved bytes (3 bytes padding)
        header.Reserved = reader.ReadBytes(3);

        return header;
    }

    private static void ValidateHeader(SerializedFileHeader header, int dataLength)
    {
        if (header.Version < 14 || header.Version > 30)
        {
            throw new InvalidVersionException(
                $"Unsupported SerializedFile version: {header.Version} (supported: 14-30)", header.Version);
        }

        if (header.FileSize < 0)
        {
            throw new CorruptedHeaderException($"Invalid FileSize: {header.FileSize}");
        }

        if (header.FileSize > dataLength)
        {
            throw new CorruptedHeaderException(
                $"FileSize {header.FileSize} exceeds actual data length {dataLength}");
        }

        if (header.DataOffset < 0 || header.DataOffset > header.FileSize)
        {
            throw new CorruptedHeaderException(
                $"Invalid DataOffset: {header.DataOffset} (FileSize: {header.FileSize})");
        }

        if (header.MetadataSize > dataLength)
        {
            throw new TruncatedMetadataException(
                $"Metadata size {header.MetadataSize} exceeds file length {dataLength}",
                header.MetadataSize, dataLength);
        }
    }

    private static void SkipHeader(EndianBinaryReader reader, uint version)
    {
        // Skip past header to start of metadata
        reader.Position = 0;
        reader.ReadUInt32(); // MetadataSize

        if (version >= 22)
        {
            reader.ReadInt64(); // FileSize
            reader.ReadUInt32(); // Version
            reader.ReadInt64(); // DataOffset
        }
        else if (version >= 14)
        {
            reader.ReadUInt32(); // FileSize
            reader.ReadUInt32(); // Version
            reader.ReadUInt32(); // DataOffset
        }
        else
        {
            // Version < 14 has different layout, needs specific handling
            // For now, throw exception for unsupported old versions
            throw new InvalidVersionException($"Version < 14 not yet implemented: {version}", version);
        }

        reader.ReadByte(); // Endianness
        reader.ReadBytes(3); // Reserved
    }

    private static TypeTree ParseTypeTree(EndianBinaryReader reader, uint version, bool enableTypeTree)
    {
        var types = new List<SerializedType>();
        int typeCount = reader.ReadInt32();

        for (int i = 0; i < typeCount; i++)
        {
            var type = ParseSerializedType(reader, version, enableTypeTree);
            types.Add(type);
        }

        return new TypeTree(types, enableTypeTree);
    }

    private static SerializedType ParseSerializedType(EndianBinaryReader reader, uint version, bool enableTypeTree)
    {
        var type = new SerializedType
        {
            ClassId = reader.ReadInt32()
        };

        if (version >= 16)
        {
            type.IsStrippedType = reader.ReadBoolean();
        }

        if (version >= 17)
        {
            type.ScriptTypeIndex = reader.ReadInt16();
        }

        // ScriptId hash (16 bytes) for MonoBehaviour (ClassId 114)
        if (version >= 17 && type.ClassId == 114)
        {
            type.ScriptId = reader.ReadBytes(16);
        }

        // OldTypeHash (16 bytes)
        if (version >= 5)
        {
            type.OldTypeHash = reader.ReadBytes(16);
        }

        // Read type tree nodes if enabled
        if (enableTypeTree)
        {
            var nodes = ParseTypeTreeNodes(reader, version);
            type.Nodes = nodes;
        }

        // Type dependencies (version >= 21)
        if (version >= 21)
        {
            int depCount = reader.ReadInt32();
            if (depCount > 0)
            {
                var deps = new int[depCount];
                for (int i = 0; i < depCount; i++)
                {
                    deps[i] = reader.ReadInt32();
                }
                type.TypeDependencies = deps;
            }
        }

        return type;
    }

    private static List<TypeTreeNode> ParseTypeTreeNodes(EndianBinaryReader reader, uint version)
    {
        // TODO: Implement type tree node parsing with string table support
        // This is a stub for now
        int nodeCount = reader.ReadInt32();
        int stringTableSize = reader.ReadInt32();

        var nodes = new List<TypeTreeNode>(nodeCount);

        // Read string table if version >= 10
        byte[]? stringTable = null;
        if (version >= 10 && stringTableSize > 0)
        {
            stringTable = reader.ReadBytes(stringTableSize);
        }

        // Read nodes
        for (int i = 0; i < nodeCount; i++)
        {
            var node = new TypeTreeNode
            {
                Version = reader.ReadInt16(),
                Level = reader.ReadByte(),
                TypeFlags = reader.ReadUInt16(),
                Index = i
            };

            // Read type and name (version-dependent)
            if (version >= 10 && stringTable != null)
            {
                // String table mode: read offsets
                uint typeOffset = reader.ReadUInt32();
                uint nameOffset = reader.ReadUInt32();
                node.Type = ReadStringFromTable(stringTable, typeOffset);
                node.Name = ReadStringFromTable(stringTable, nameOffset);
            }
            else
            {
                // Inline string mode
                node.Type = reader.ReadUtf8NullTerminated();
                node.Name = reader.ReadUtf8NullTerminated();
            }

            node.ByteSize = reader.ReadInt32();
            node.MetaFlag = reader.ReadInt32();

            nodes.Add(node);
        }

        return nodes;
    }

    private static string ReadStringFromTable(byte[] table, uint offset)
    {
        if (offset >= table.Length)
        {
            return string.Empty;
        }

        // Find null terminator
        int length = 0;
        for (int i = (int)offset; i < table.Length && table[i] != 0; i++)
        {
            length++;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        return System.Text.Encoding.UTF8.GetString(table, (int)offset, length);
    }

    private static List<ObjectInfo> ParseObjectTable(EndianBinaryReader reader, uint version)
    {
        int objectCount = reader.ReadInt32();
        var objects = new List<ObjectInfo>(objectCount);

        for (int i = 0; i < objectCount; i++)
        {
            var obj = new ObjectInfo();

            // Align before each object entry (version >= 14)
            if (version >= 14)
            {
                reader.Align(4);
            }

            // PathId
            obj.PathId = version >= 14
                ? reader.ReadInt64()
                : reader.ReadInt32();

            // ByteStart
            obj.ByteStart = version >= 22
                ? reader.ReadInt64()
                : reader.ReadUInt32();

            // ByteSize
            obj.ByteSize = reader.ReadUInt32();

            // TypeId
            obj.TypeId = reader.ReadInt32();

            // Additional fields for version >= 11
            if (version < 11)
            {
                // ClassId is stored directly
                obj.ClassId = reader.ReadUInt16();
            }

            // ScriptTypeIndex (version >= 11)
            if (version >= 11 && version < 17)
            {
                obj.ScriptTypeIndex = reader.ReadUInt16();
            }

            // Stripped (version >= 15)
            if (version >= 15 && version < 17)
            {
                obj.Stripped = reader.ReadByte();
            }
            else if (version >= 17)
            {
                obj.Stripped = reader.ReadByte();
            }

            objects.Add(obj);
        }

        return objects;
    }

    private static void ResolveClassIds(List<ObjectInfo> objects, TypeTree typeTree)
    {
        foreach (var obj in objects.Where(o => o.ClassId == 0))
        {
            // Try to resolve from type tree
            var classId = typeTree.ResolveClassId(obj.TypeId);
            if (classId.HasValue)
            {
                obj.ClassId = classId.Value;
            }
            else if (typeTree.Types.Count > 0)
            {
                // docs/UnityParsing.md: when a type tree is present and TypeId
                // cannot be resolved, treat this as invalid object metadata.
                throw new InvalidObjectInfoException(
                    $"Failed to resolve ClassId from TypeTree for object {obj.PathId} with TypeId {obj.TypeId}.",
                    obj.PathId);
            }
            else
            {
                // No type tree present, fall back to using TypeId as ClassId
                obj.ClassId = obj.TypeId;
            }
        }
    }

    private static List<FileIdentifier> ParseFileIdentifiers(EndianBinaryReader reader, uint version)
    {
        int identifierCount = reader.ReadInt32();
        var identifiers = new List<FileIdentifier>(identifierCount);

        for (int i = 0; i < identifierCount; i++)
        {
            var identifier = new FileIdentifier();

            // Guid (16 bytes) for version >= 6
            if (version >= 6)
            {
                var guidBytes = reader.ReadBytes(16);
                identifier.Guid = new Guid(guidBytes);
            }

            // Type
            identifier.Type = reader.ReadInt32();

            // PathName
            identifier.PathName = reader.ReadUtf8NullTerminated();

            identifiers.Add(identifier);
        }

        return identifiers;
    }

    private static void ValidateObjectBounds(List<ObjectInfo> objects, int dataRegionLength, long dataOffset)
    {
        foreach (var obj in objects)
        {
            // Compute relative bounds as 64-bit to allow overflow checks.
            long relativeStart = obj.ByteStart;
            long relativeEnd = relativeStart + obj.ByteSize;

            // Detect overflow in the relative range computation.
            if (relativeEnd < relativeStart)
            {
                throw new InvalidObjectInfoException(
                    $"Object {obj.PathId} has overflowing bounds: " +
                    $"ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}", obj.PathId);
            }

            // Compute absolute offsets using dataOffset and detect overflow.
            long absoluteStart = dataOffset + relativeStart;
            long absoluteEnd = dataOffset + relativeEnd;
            if (absoluteStart < dataOffset || absoluteEnd < dataOffset)
            {
                throw new InvalidObjectInfoException(
                    $"Object {obj.PathId} has overflowing absolute bounds: " +
                    $"DataOffset={dataOffset}, ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}", obj.PathId);
            }

            if (obj.ByteStart < 0)
            {
                throw new InvalidObjectInfoException(
                    $"Object {obj.PathId} has negative ByteStart: {obj.ByteStart}", obj.PathId);
            }

            if (relativeEnd > dataRegionLength)
            {
                throw new InvalidObjectInfoException(
                    $"Object {obj.PathId} bounds exceed data region: " +
                    $"ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}, DataRegion.Length={dataRegionLength}",
                    obj.PathId);
            }
        }
    }
}
