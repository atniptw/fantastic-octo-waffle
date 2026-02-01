using System.Text;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.SerializedFile;

/// <summary>
/// Represents a fully parsed SerializedFile containing Unity object metadata.
/// Provides access to object table, type tree, and object data region.
/// </summary>
public sealed class SerializedFile
{
    private static readonly Dictionary<uint, string> CommonStrings = new()
    {
        { 0, "AABB" },
        { 5, "AnimationClip" },
        { 19, "AnimationCurve" },
        { 34, "AnimationState" },
        { 49, "Array" },
        { 55, "Base" },
        { 60, "BitField" },
        { 69, "bitset" },
        { 76, "bool" },
        { 81, "char" },
        { 86, "ColorRGBA" },
        { 96, "Component" },
        { 106, "data" },
        { 111, "deque" },
        { 117, "double" },
        { 124, "dynamic_array" },
        { 138, "FastPropertyName" },
        { 155, "first" },
        { 161, "float" },
        { 167, "Font" },
        { 172, "GameObject" },
        { 183, "Generic Mono" },
        { 196, "GradientNEW" },
        { 208, "GUID" },
        { 213, "GUIStyle" },
        { 222, "int" },
        { 226, "list" },
        { 231, "long long" },
        { 241, "map" },
        { 245, "Matrix4x4f" },
        { 256, "MdFour" },
        { 263, "MonoBehaviour" },
        { 277, "MonoScript" },
        { 288, "m_ByteSize" },
        { 299, "m_Curve" },
        { 307, "m_EditorClassIdentifier" },
        { 331, "m_EditorHideFlags" },
        { 349, "m_Enabled" },
        { 359, "m_ExtensionPtr" },
        { 374, "m_GameObject" },
        { 387, "m_Index" },
        { 395, "m_IsArray" },
        { 405, "m_IsStatic" },
        { 416, "m_MetaFlag" },
        { 427, "m_Name" },
        { 434, "m_ObjectHideFlags" },
        { 452, "m_PrefabInternal" },
        { 469, "m_PrefabParentObject" },
        { 490, "m_Script" },
        { 499, "m_StaticEditorFlags" },
        { 519, "m_Type" },
        { 526, "m_Version" },
        { 536, "Object" },
        { 543, "pair" },
        { 548, "PPtr<Component>" },
        { 564, "PPtr<GameObject>" },
        { 581, "PPtr<Material>" },
        { 596, "PPtr<MonoBehaviour>" },
        { 616, "PPtr<MonoScript>" },
        { 633, "PPtr<Object>" },
        { 646, "PPtr<Prefab>" },
        { 659, "PPtr<Sprite>" },
        { 672, "PPtr<TextAsset>" },
        { 688, "PPtr<Texture>" },
        { 702, "PPtr<Texture2D>" },
        { 718, "PPtr<Transform>" },
        { 734, "Prefab" },
        { 741, "Quaternionf" },
        { 753, "Rectf" },
        { 759, "RectInt" },
        { 767, "RectOffset" },
        { 778, "second" },
        { 785, "set" },
        { 789, "short" },
        { 795, "size" },
        { 800, "SInt16" },
        { 807, "SInt32" },
        { 814, "SInt64" },
        { 821, "SInt8" },
        { 827, "staticvector" },
        { 840, "string" },
        { 847, "TextAsset" },
        { 857, "TextMesh" },
        { 866, "Texture" },
        { 874, "Texture2D" },
        { 884, "Transform" },
        { 894, "TypelessData" },
        { 907, "UInt16" },
        { 914, "UInt32" },
        { 921, "UInt64" },
        { 928, "UInt8" },
        { 934, "unsigned int" },
        { 947, "unsigned long long" },
        { 966, "unsigned short" },
        { 981, "vector" },
        { 988, "Vector2f" },
        { 997, "Vector3f" },
        { 1006, "Vector4f" },
        { 1015, "m_ScriptingClassIdentifier" },
        { 1042, "Gradient" },
        { 1051, "Type*" },
        { 1057, "int2_storage" },
        { 1070, "int3_storage" },
        { 1083, "BoundsInt" },
        { 1093, "m_CorrespondingSourceObject" },
        { 1121, "m_PrefabInstance" },
        { 1138, "m_PrefabAsset" },
        { 1152, "FileSize" },
    };

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
    /// Gets the list of local serialized object identifiers (script types).
    /// </summary>
    public IReadOnlyList<LocalSerializedObjectIdentifier> ScriptTypes { get; }

    /// <summary>
    /// Gets the list of referenced types (version >= 20).
    /// </summary>
    public IReadOnlyList<SerializedType> RefTypes { get; }

    /// <summary>
    /// Gets the user information string (version >= 5), if present.
    /// </summary>
    public string? UserInformation { get; }

    /// <summary>
    /// Gets the raw object data region for downstream readers.
    /// </summary>
    public ReadOnlyMemory<byte> ObjectDataRegion => _objectDataRegion;

    private SerializedFile(
        SerializedFileHeader header,
        TypeTree typeTree,
        IReadOnlyList<ObjectInfo> objects,
        IReadOnlyList<FileIdentifier> externals,
        IReadOnlyList<LocalSerializedObjectIdentifier> scriptTypes,
        IReadOnlyList<SerializedType> refTypes,
        string? userInformation,
        ReadOnlyMemory<byte> objectDataRegion)
    {
        Header = header;
        TypeTree = typeTree;
        Objects = objects;
        Externals = externals;
        ScriptTypes = scriptTypes;
        RefTypes = refTypes;
        UserInformation = userInformation;
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
        Console.WriteLine($"DEBUG: SF Header Version={header.Version}, Endian={(header.Endianness == 1 ? "BE" : "LE")}, MetadataSize={header.MetadataSize}, FileSize={header.FileSize}, DataOffset={header.DataOffset}");

        // Validate header consistency
        ValidateHeader(header, buffer.Length);

        // Step 2: Create endian-aware reader
        // The header.Endianness tells us the endianness for metadata and objects
        bool isBigEndian = header.Endianness == 1;
        // Note: stream is already positioned after header by ParseHeader
        using var endianReader = new EndianBinaryReader(stream, isBigEndian);

        // Note: For version 22+, metadata starts immediately after the extended header.
        // Do NOT consume additional bytes here; the first metadata field is the Unity version string.

        // Step 3: Parse metadata region following UnityPy's exact order
        // See UnityPy/files/SerializedFile.py lines 270-280

        // Read Unity version string if version >= 7
        if (header.Version >= 7)
        {
            header.UnityVersionString = endianReader.ReadUtf8NullTerminated();
            Console.WriteLine($"DEBUG: UnityVersion='{header.UnityVersionString}'");
        }

        // Read target platform if version >= 8
        if (header.Version >= 8)
        {
            header.TargetPlatform = endianReader.ReadUInt32();
            Console.WriteLine($"DEBUG: TargetPlatform={header.TargetPlatform}");
        }

        // Read EnableTypeTree flag if version >= 13
        bool enableTypeTree = true; // Default
        if (header.Version >= 13)
        {
            enableTypeTree = endianReader.ReadBoolean();
            header.EnableTypeTree = enableTypeTree;
            Console.WriteLine($"DEBUG: EnableTypeTree={enableTypeTree}");
        }

        // Step 4: Parse type tree
        var typeTree = ParseTypeTree(endianReader, header.Version, enableTypeTree);
        Console.WriteLine($"DEBUG: After TypeTree parse, position={endianReader.Position}");

        // Step 5: Parse object table (do NOT align before reading count)
        long posBeforeObj = endianReader.Position;
        var objects = ParseObjectTable(endianReader, header.Version);
        Console.WriteLine($"DEBUG: ObjectTable at pos={posBeforeObj}, count={objects.Count}");

        // Step 6: Resolve ClassIds for objects
        ResolveClassIds(objects, typeTree);

        // Step 7: Parse script types (version >= 11)
        List<LocalSerializedObjectIdentifier> scriptTypes = new();
        if (header.Version >= 11)
        {
            scriptTypes = ParseScriptTypes(endianReader, header.Version);
        }

        // Step 8: Align and parse file identifiers (externals)
        endianReader.Align(4);

        // Try to parse externals, but if we're at the metadata boundary, skip
        List<FileIdentifier> externals;
        try
        {
            externals = ParseFileIdentifiers(endianReader, header.Version);
        }
        catch (Exception)
        {
            // If parsing externals fails, we've likely reached the end of metadata
            externals = new List<FileIdentifier>();
        }

        // Step 9: Parse ref types (version >= 20)
        List<SerializedType> refTypes = new();
        if (header.Version >= 20)
        {
            refTypes = ParseRefTypes(endianReader, header.Version, enableTypeTree);
        }

        // Step 10: Read user information (version >= 5)
        string? userInformation = null;
        if (header.Version >= 5)
        {
            try
            {
                userInformation = endianReader.ReadUtf8NullTerminated();
            }
            catch (Exception)
            {
                userInformation = null;
            }
        }

        // Step 11: Extract object data region
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

        return new SerializedFile(header, typeTree, objects, externals, scriptTypes, refTypes, userInformation, objectDataRegion);
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

        // CRITICAL: Match UnityPy's exact parsing sequence!
        // The initial 4 uint32s can be either big-endian or little-endian.
        // We detect by checking which gives a valid version (9-30).

        long startPos = reader.BaseStream.Position;
        byte[] headerBytes = reader.ReadBytes(20); // First 16 bytes + 4 for endian+reserved

        // Try reading as big-endian first (common for SerializedFiles)
        uint metadataSizeBE = (uint)((headerBytes[0] << 24) | (headerBytes[1] << 16) | (headerBytes[2] << 8) | headerBytes[3]);
        uint fileSizeBE = (uint)((headerBytes[4] << 24) | (headerBytes[5] << 16) | (headerBytes[6] << 8) | headerBytes[7]);
        uint versionBE = (uint)((headerBytes[8] << 24) | (headerBytes[9] << 16) | (headerBytes[10] << 8) | headerBytes[11]);
        uint dataOffsetBE = (uint)((headerBytes[12] << 24) | (headerBytes[13] << 16) | (headerBytes[14] << 8) | headerBytes[15]);

        // Try reading as little-endian
        uint metadataSizeLE = (uint)(headerBytes[0] | (headerBytes[1] << 8) | (headerBytes[2] << 16) | (headerBytes[3] << 24));
        uint fileSizeLE = (uint)(headerBytes[4] | (headerBytes[5] << 8) | (headerBytes[6] << 16) | (headerBytes[7] << 24));
        uint versionLE = (uint)(headerBytes[8] | (headerBytes[9] << 8) | (headerBytes[10] << 16) | (headerBytes[11] << 24));
        uint dataOffsetLE = (uint)(headerBytes[12] | (headerBytes[13] << 8) | (headerBytes[14] << 16) | (headerBytes[15] << 24));

        // Determine which endianness gives a valid version (9-30)
        uint version;
        bool initialEndianWasBig; // Track which endianness was used for initial read
        if (versionBE >= 9 && versionBE <= 30)
        {
            initialEndianWasBig = true;
            version = versionBE;
            header.MetadataSize = metadataSizeBE;
            header.FileSize = fileSizeBE;
            header.Version = versionBE;
            header.DataOffset = dataOffsetBE;
        }
        else if (versionLE >= 9 && versionLE <= 30)
        {
            initialEndianWasBig = false;
            version = versionLE;
            header.MetadataSize = metadataSizeLE;
            header.FileSize = fileSizeLE;
            header.Version = versionLE;
            header.DataOffset = dataOffsetLE;
        }
        else
        {
            throw new InvalidVersionException(
                $"Could not detect valid SerializedFile version (BE={versionBE}, LE={versionLE})", versionLE);
        }

        // Read endianness byte and reserved (bytes 16-19)
        header.Endianness = headerBytes[16];
        header.Reserved = new byte[] { headerBytes[17], headerBytes[18], headerBytes[19] };

        // For version >= 22, actual header values come AFTER endian+reserved bytes
        // CRITICAL: Use the SAME endianness as we detected for the initial read!
        // The endianness byte tells us about metadata/objects, NOT the version 22+ header fields.
        if (version >= 22)
        {
            using var endianReader = new EndianBinaryReader(reader.BaseStream, initialEndianWasBig);

            header.MetadataSize = endianReader.ReadUInt32();
            header.FileSize = endianReader.ReadInt64();
            header.DataOffset = endianReader.ReadInt64();
            long unknown = endianReader.ReadInt64();
        }
        else
        {
            reader.BaseStream.Position = startPos + 20;
        }

        if (header.Endianness > 1)
        {
            throw new EndiannessMismatchException(
                $"Invalid endianness value: {header.Endianness}", header.Endianness);
        }

        return header;
    }

    private static void ValidateHeader(SerializedFileHeader header, int dataLength)
    {
        // UnityPy supports versions 9-30 (we parse >= 9)
        if (header.Version < 9 || header.Version > 30)
        {
            throw new InvalidVersionException(
                $"Unsupported SerializedFile version: {header.Version} (supported: 9-30)", header.Version);
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
        // For version >= 22, header is: 20 bytes initial + 28 bytes extended = 48 bytes total
        // For version < 22, header is: 20 bytes total

        if (version >= 22)
        {
            reader.Position = 48; // 20 (initial header) + 28 (extended header)
        }
        else
        {
            reader.Position = 20; // Just initial header
        }
    }

    private static TypeTree ParseTypeTree(EndianBinaryReader reader, uint version, bool enableTypeTree)
    {
        // Basic sanity check to avoid silent overruns when metadata is truncated
        var remainingForCount = reader.BaseStream.Length - reader.Position;
        if (remainingForCount < sizeof(int))
        {
            throw new InvalidOperationException(
                $"Insufficient bytes for type count. Remaining={remainingForCount}, Position={reader.Position}, Length={reader.BaseStream.Length}");
        }

        var types = new List<SerializedType>();
        int typeCount = reader.ReadInt32();

        if (typeCount < 0)
        {
            throw new InvalidOperationException($"Negative typeCount {typeCount} at position {reader.Position}");
        }

        // Guard against absurd counts that would obviously exceed remaining metadata
        var remainingAfterCount = reader.BaseStream.Length - reader.Position;
        if (typeCount > 0 && remainingAfterCount < typeCount * 4L)
        {
            throw new InvalidOperationException(
                $"Type count {typeCount} exceeds remaining metadata bytes {remainingAfterCount} at position {reader.Position}");
        }

        for (int i = 0; i < typeCount; i++)
        {
            try
            {
                Console.WriteLine($"DEBUG: About to parse type {i} at pos={reader.Position}");
                var type = ParseSerializedType(reader, version, enableTypeTree);
                Console.WriteLine($"DEBUG: Finished parsing type {i} at pos={reader.Position}");
                types.Add(type);
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidOperationException(
                    $"Unexpected end of stream while reading type {i + 1}/{typeCount} at position {reader.Position} of {reader.BaseStream.Length}",
                    ex);
            }
        }

        return new TypeTree(types, enableTypeTree);
    }

    /// <summary>
    /// Parses a SerializedType structure.
    /// Reference: UnityPy/files/SerializedFile.py SerializedType.__init__ lines 116-148.
    /// </summary>
    /// <param name="isRefType">True if parsing a ref type (for v>=20 ref_types list)</param>
    private static SerializedType ParseSerializedType(EndianBinaryReader reader, uint version, bool enableTypeTree, bool isRefType = false)
    {
        long startPos = reader.BaseStream.Position;
        Console.WriteLine($"DEBUG: ParseSerializedType starting at pos={startPos}, isRefType={isRefType}");

        var type = new SerializedType
        {
            ClassId = reader.ReadInt32()
        };
        Console.WriteLine($"DEBUG: ClassId={type.ClassId}");

        if (version >= 16)
        {
            type.IsStrippedType = reader.ReadBoolean();
        }

        if (version >= 17)
        {
            type.ScriptTypeIndex = reader.ReadInt16();
        }

        // UnityPy logic for ScriptId/OldTypeHash (lines 128-133)
        if (version >= 13)
        {
            bool needsHashes = (isRefType && type.ScriptTypeIndex >= 0)
                              || (version < 16 && type.ClassId < 0)
                              || (version >= 16 && type.ClassId == 114);

            if (needsHashes)
            {
                type.ScriptId = reader.ReadBytes(16); // Hash128
            }

            type.OldTypeHash = reader.ReadBytes(16); // Hash128
        }

        // Read type tree nodes if enabled (lines 135-142)
        if (enableTypeTree)
        {
            var nodes = ParseTypeTreeNodes(reader, version);
            type.Nodes = nodes;

            // Build hierarchical tree from flat list (skip if nodes is empty)
            if (nodes.Count > 0)
            {
                type.TreeRoot = BuildTypeTree(nodes);
            }
        }

        // For v>=21, ref types have class metadata OR type dependencies (lines 144-148)
        if (version >= 21)
        {
            if (isRefType)
            {
                // Ref types: read MonoBehaviour class metadata
                type.ClassName = reader.ReadUtf8NullTerminated();
                type.NameSpace = reader.ReadUtf8NullTerminated();
                type.AssemblyName = reader.ReadUtf8NullTerminated();
                Console.WriteLine($"DEBUG: RefType metadata: {type.ClassName} ({type.NameSpace}.{type.AssemblyName})");
            }
            else
            {
                // Regular types: read type dependencies array
                long depCountPos = reader.BaseStream.Position;
                int depCount = reader.ReadInt32();
                Console.WriteLine($"DEBUG: TypeDependencies at pos={depCountPos}, count={depCount}");
                if (depCount > 0)
                {
                    var deps = new int[depCount];
                    for (int i = 0; i < depCount; i++)
                    {
                        deps[i] = reader.ReadInt32();
                    }
                    type.TypeDependencies = deps;
                }
                Console.WriteLine($"DEBUG: After TypeDependencies, pos={reader.BaseStream.Position}");
            }
        }

        return type;
    }

    private static List<TypeTreeNode> ParseTypeTreeNodes(EndianBinaryReader reader, uint version)
    {
        // CRITICAL: TypeTree has TWO formats depending on version (UnityPy: SerializedFile.py#L134-135)
        // - Legacy format (v < 10): Text-based null-terminated strings, completely different struct
        // - Blob format (v >= 12 or v == 10): Binary struct with string offsets (packed array)

        long startPos = reader.Position;
        Console.WriteLine($"DEBUG: ParseTypeTreeNodes at pos={startPos}, version={version}");

        // Determine which format to use
        bool usesBlobFormat = version >= 12 || version == 10;

        if (usesBlobFormat)
        {
            return ParseTypeTreeNodesBlob(reader, version, startPos);
        }
        else
        {
            // Legacy format for v < 10 (but v == 10 uses blob)
            // This handles v0-9 (very old Unity versions)
            return ParseTypeTreeNodesLegacy(reader, version, startPos);
        }
    }

    /// <summary>
    /// Parses blob format TypeTree (v >= 12 or v == 10).
    /// Binary struct array with string offset pool.
    /// </summary>
    private static List<TypeTreeNode> ParseTypeTreeNodesBlob(EndianBinaryReader reader, uint version, long startPos)
    {
        int nodeCount = reader.ReadInt32();
        int stringBufferSize = reader.ReadInt32();

        Console.WriteLine($"DEBUG: ParseTypeTreeNodesBlob at pos={startPos}, nodeCount={nodeCount}, stringBufSize={stringBufferSize}");

        // Handle empty node lists - but still need to consume the data if present
        if (nodeCount == 0)
        {
            // Even with 0 nodes, we might have string buffer data in v22+ (for ref types)
            // Skip it to maintain alignment
            if (stringBufferSize > 0)
            {
                reader.ReadBytes(stringBufferSize);
                Console.WriteLine($"DEBUG: Skipped {stringBufferSize} bytes of empty string buffer");
            }
            return new List<TypeTreeNode>();
        }

        // Determine struct size per UnityPy's _get_blob_node_struct (TypeTreeNode.py#L330-347)
        // v < 19: hBBIIiii = 2+1+1+4+4+4+4+4 = 24 bytes
        // v >= 19: hBBIIiiiQ = 2+1+1+4+4+4+4+4+8 = 32 bytes (adds m_RefTypeHash)
        int nodeStructSize = version >= 19 ? 32 : 24;

        // Read all node structs
        int expectedNodeDataSize = nodeStructSize * nodeCount;
        Console.WriteLine($"DEBUG: About to read {expectedNodeDataSize} bytes of node data (nodeStructSize={nodeStructSize}, nodeCount={nodeCount})");
        var nodeData = reader.ReadBytes(expectedNodeDataSize);
        Console.WriteLine($"DEBUG: Actually read {nodeData.Length} bytes of node data");

        // Read string buffer
        Console.WriteLine($"DEBUG: About to read {stringBufferSize} bytes of string buffer");
        var stringBuffer = reader.ReadBytes(stringBufferSize);
        Console.WriteLine($"DEBUG: Actually read {stringBuffer.Length} bytes of string buffer");

        Console.WriteLine($"DEBUG: After reading TypeTree blob, pos={reader.Position}");

        // Parse node structs into TypeTreeNode objects
        var nodes = new List<TypeTreeNode>(nodeCount);

        using var nodeStream = new MemoryStream(nodeData, writable: false);
        using var nodeReader = new BinaryReader(nodeStream);

        for (int i = 0; i < nodeCount; i++)
        {
            // BLOB FORMAT FIELD ORDER (UnityPy: TypeTreeNode.py#L330-347):
            // m_Version (int16) → m_Level (uint8) → m_TypeFlags (uint8) 
            // → m_TypeStrOffset (uint32) → m_NameStrOffset (uint32)
            // → m_ByteSize (int32) → m_Index (int32) → m_MetaFlag (int32)
            // [→ m_RefTypeHash (uint64) if v >= 19]

            var node = new TypeTreeNode
            {
                Version = nodeReader.ReadInt16(),      // 2 bytes
                Level = nodeReader.ReadByte(),         // 1 byte
                TypeFlags = nodeReader.ReadByte()      // 1 byte
            };

            // Read string offsets from string pool or common string table (UnityPy: TypeTreeNode.parse_blob)
            uint typeStrOffset = nodeReader.ReadUInt32();    // 4 bytes
            uint nameStrOffset = nodeReader.ReadUInt32();    // 4 bytes

            node.ByteSize = nodeReader.ReadInt32();          // 4 bytes
            int index = nodeReader.ReadInt32();              // 4 bytes  
            node.MetaFlag = nodeReader.ReadInt32();          // 4 bytes

            // For version >= 19, there are 8 more bytes (refTypeHash - not used yet)
            if (version >= 19)
            {
                nodeReader.ReadUInt64(); // 8 bytes - m_RefTypeHash
            }

            // Resolve strings using blob offset/common string rules
            node.Type = ResolveTypeTreeString(stringBuffer, typeStrOffset);
            node.Name = ResolveTypeTreeString(stringBuffer, nameStrOffset);
            node.Index = i;  // Use loop counter as logical index

            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Parses legacy format TypeTree (v < 10, except v == 10 which uses blob).
    /// Text-based format with null-terminated strings and different field order.
    /// Port of UnityPy: TypeTreeNode.py#L248-306 (parse method for legacy format).
    /// </summary>
    private static List<TypeTreeNode> ParseTypeTreeNodesLegacy(EndianBinaryReader reader, uint version, long startPos)
    {
        var nodes = new List<TypeTreeNode>();

        // LEGACY FORMAT (v < 10): Reads nodes sequentially with text strings
        // Field order differs from blob format!
        // UnityPy: TypeTreeNode.parse (legacy) reads:
        // 1. m_Version (int32, 4 bytes) - differs from blob (int16)!
        // 2. m_Type (string_to_null) - read directly, not offset!
        // 3. m_Name (string_to_null) - read directly, not offset!
        // 4. m_ByteSize (int32, 4 bytes)
        // 5. m_Index (int32, 4 bytes)
        // 6. [if v >= 2] m_TypeFlags (int32, 4 bytes) - stored as int32, not uint8!
        // 7. [if v >= 3] m_MetaFlag (int32, 4 bytes)
        // 8. [if v == 2] m_VariableCount (int32, 4 bytes)
        // NO m_Level in legacy format!
        // NO string offsets - strings embedded inline!

        try
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                // Try to read legacy node - if we fail, stop
                int startNodePos = (int)reader.BaseStream.Position;

                var node = new TypeTreeNode();

                // Field 1: m_Version (int32 in legacy, NOT int16 like blob!)
                if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                    break;
                node.Version = reader.ReadInt32();

                // Field 2: m_Type (null-terminated string, NOT offset!)
                node.Type = reader.ReadUtf8NullTerminated();

                // Field 3: m_Name (null-terminated string, NOT offset!)
                node.Name = reader.ReadUtf8NullTerminated();

                // Field 4: m_ByteSize (int32)
                if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                    break;
                node.ByteSize = reader.ReadInt32();

                // Field 5: m_Index (int32)
                node.Index = reader.ReadInt32();

                // Field 6: m_TypeFlags (int32 in legacy, not uint8 like blob!)
                if (version >= 2)
                {
                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                        break;
                    node.TypeFlags = reader.ReadInt32();
                }

                // Field 7: m_MetaFlag (int32)
                if (version >= 3)
                {
                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                        break;
                    node.MetaFlag = reader.ReadInt32();
                }

                // Field 8: m_VariableCount (legacy only, v==2)
                if (version == 2)
                {
                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                        break;
                    // m_VariableCount not stored in our TypeTreeNode, but we must read it
                    int variableCount = reader.ReadInt32();
                }

                // m_Level doesn't exist in legacy format - default to 0
                node.Level = 0;

                nodes.Add(node);

                // Safety check: if we haven't moved forward, something is wrong
                if ((int)reader.BaseStream.Position == startNodePos)
                    break;
            }

            Console.WriteLine($"DEBUG: ParseTypeTreeNodesLegacy read {nodes.Count} nodes (v{version})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error parsing legacy TypeTree nodes: {ex.Message}");
            // Return what we got so far
        }

        return nodes;
    }

    private static string ExtractNullTerminatedString(byte[] stringBuffer, uint offset)
    {
        if (offset >= stringBuffer.Length)
        {
            return string.Empty;
        }

        // Find null terminator
        int nullIndex = Array.IndexOf(stringBuffer, (byte)0, (int)offset);
        if (nullIndex < 0)
        {
            nullIndex = stringBuffer.Length;
        }

        int length = nullIndex - (int)offset;
        if (length <= 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(stringBuffer, (int)offset, length);
    }

    private static string ResolveTypeTreeString(byte[] stringBuffer, uint value)
    {
        bool isOffset = (value & 0x80000000) == 0;
        if (isOffset)
        {
            return ExtractNullTerminatedString(stringBuffer, value);
        }

        uint offset = value & 0x7FFFFFFF;
        return CommonStrings.TryGetValue(offset, out var common) ? common : offset.ToString();
    }

    /// <summary>
    /// Parses script types (LocalSerializedObjectIdentifier entries) for v>=11.
    /// Reference: UnityPy/files/SerializedFile.py lines 298-300.
    /// </summary>
    private static List<LocalSerializedObjectIdentifier> ParseScriptTypes(EndianBinaryReader reader, uint version)
    {
        int scriptCount = reader.ReadInt32();
        Console.WriteLine($"DEBUG: Parsing {scriptCount} script types (v{version})");

        var scriptTypes = new List<LocalSerializedObjectIdentifier>(scriptCount);
        for (int i = 0; i < scriptCount; i++)
        {
            int localSerializedFileIndex = reader.ReadInt32();
            long localIdentifierInFile;

            if (version < 14)
            {
                localIdentifierInFile = reader.ReadInt32();
            }
            else
            {
                reader.Align(4); // align_stream() in UnityPy
                localIdentifierInFile = reader.ReadInt64();
            }

            scriptTypes.Add(new LocalSerializedObjectIdentifier
            {
                LocalSerializedFileIndex = localSerializedFileIndex,
                LocalIdentifierInFile = localIdentifierInFile
            });
        }

        return scriptTypes;
    }

    /// <summary>
    /// Parses ref types (SerializedType entries with is_ref_type=true) for v>=20.
    /// Reference: UnityPy/files/SerializedFile.py lines 305-307.
    /// </summary>
    private static List<SerializedType> ParseRefTypes(EndianBinaryReader reader, uint version, bool enableTypeTree)
    {
        int refTypeCount = reader.ReadInt32();
        Console.WriteLine($"DEBUG: Parsing {refTypeCount} ref types (v{version}) at pos={reader.Position}");

        // Sanity check - refTypeCount should be reasonable
        if (refTypeCount < 0 || refTypeCount > 100000)
        {
            Console.WriteLine($"DEBUG: WARNING - RefTypeCount {refTypeCount} seems invalid, treating as 0");
            return new List<SerializedType>();
        }

        var refTypes = new List<SerializedType>(refTypeCount);
        for (int i = 0; i < refTypeCount; i++)
        {
            // Parse as ref type (is_ref_type=true)
            var refType = ParseSerializedType(reader, version, enableTypeTree, isRefType: true);
            refTypes.Add(refType);
        }

        return refTypes;
    }

    private static List<ObjectInfo> ParseObjectTable(EndianBinaryReader reader, uint version)
    {
        // Read big_id_enabled flag for v7-13 (UnityPy: SerializedFile.py lines 284-286)
        // CRITICAL: Must read BEFORE object count for v7-13
        if (version >= 7 && version < 14)
        {
            int bigIdEnabled = reader.ReadInt32();
            Console.WriteLine($"DEBUG: BigIdEnabled={bigIdEnabled} (v{version})");
        }

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
            if (i < 3)
            {
                Console.WriteLine($"DEBUG: Object[{i}] TypeId={obj.TypeId}, PathId={obj.PathId}, ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}");
            }

            // NOTE: For version >= 14, there are NO additional fields after TypeId.
            // The fields below are for OLDER versions only, and our parser only supports v9+
            // which for practical purposes is v14+.

            objects.Add(obj);
        }

        return objects;
    }

    private static void ResolveClassIds(List<ObjectInfo> objects, TypeTree typeTree)
    {
        // Build a lookup of TypeId -> SerializedType
        // For bundles with enableTypeTree, TypeId is an index into the types array
        // However, UnityPy also supports TypeId as a hash value when types are indexed by hash

        foreach (var obj in objects)
        {
            // If ClassId is already set, skip
            if (obj.ClassId != 0)
                continue;

            // Try TypeId as an array index first (works for most cases)
            int? classId = typeTree.ResolveClassId(obj.TypeId);
            if (classId.HasValue)
            {
                obj.ClassId = classId.Value;
            }
            else if (typeTree.Types.Count > 0)
            {
                // If TypeId doesn't resolve and type tree exists but is populated,
                // treat TypeId as ClassId directly (fallback for hash-based lookup)
                // This assumes TypeId might already BE a ClassId in some cases
                obj.ClassId = obj.TypeId;
                Console.WriteLine($"DEBUG: Resolved TypeId={obj.TypeId} as ClassId (no index match, using directly)");
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

    /// <summary>
    /// Builds a hierarchical tree structure from a flat list of TypeTreeNodes.
    /// Matches UnityPy's recursive tree architecture.
    /// </summary>
    private static TypeTreeNode BuildTypeTree(List<TypeTreeNode> flatNodes)
    {
        if (flatNodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot build tree from empty node list. Check calling code to avoid calling BuildTypeTree with empty lists.");
        }

        var root = flatNodes[0];

        // For each node, populate its Children list with immediate children
        // A child has level = parent.level + 1
        for (int i = 0; i < flatNodes.Count; i++)
        {
            if (i % 500 == 0 && i > 0)
            {
                Console.WriteLine($"[BuildTypeTree] Processed {i}/{flatNodes.Count} nodes...");
            }

            var parentNode = flatNodes[i];
            int parentLevel = parentNode.Level;
            int childLevel = parentLevel + 1;

            // Find all immediate children (next level down)
            var childrenIndices = new List<int>();
            for (int j = i + 1; j < flatNodes.Count; j++)
            {
                var potentialChild = flatNodes[j];

                // If we've reached a node at same level or lower, stop looking
                if (potentialChild.Level <= parentLevel)
                    break;

                // If this is an immediate child, add it
                if (potentialChild.Level == childLevel)
                {
                    childrenIndices.Add(j);
                }
            }

            // Attach children to parent
            foreach (var childIdx in childrenIndices)
            {
                parentNode.Children.Add(flatNodes[childIdx]);
            }
        }

        return root;
    }
}

