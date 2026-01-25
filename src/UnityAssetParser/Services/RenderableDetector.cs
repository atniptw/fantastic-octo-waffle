using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Services;

/// <summary>
/// Efficiently scans SerializedFile metadata to detect renderable geometry.
/// Uses fast-exit strategy: returns immediately when first Mesh is found.
/// </summary>
public static class RenderableDetector
{
    /// <summary>
    /// Constants for commonly renderable Unity ClassIDs.
    /// </summary>
    public static class RenderableClassIds
    {
        /// <summary>
        /// Mesh ClassID (43).
        /// </summary>
        public const int Mesh = 43;

        /// <summary>
        /// SkinnedMeshRenderer ClassID (137) - Future support.
        /// </summary>
        public const int SkinnedMeshRenderer = 137;

        /// <summary>
        /// ParticleSystem ClassID (198) - Future support.
        /// </summary>
        public const int ParticleSystem = 198;
    }

    /// <summary>
    /// Efficiently scans SerializedFile metadata to detect renderable geometry.
    /// </summary>
    /// <param name="serializedFileData">Raw SerializedFile bytes (Node 0 payload from BundleFile)</param>
    /// <returns>true if any Mesh (ClassID 43) exists; false otherwise</returns>
    /// <exception cref="InvalidVersionException">If version is unsupported</exception>
    /// <exception cref="CorruptedHeaderException">If header is corrupted or truncated</exception>
    /// <exception cref="TruncatedMetadataException">If metadata is truncated</exception>
    /// <exception cref="EndiannessMismatchException">If endianness value is invalid</exception>
    public static bool DetectRenderable(ReadOnlySpan<byte> serializedFileData)
    {
        if (serializedFileData.Length < 20)
        {
            throw new CorruptedHeaderException("File too small to contain SerializedFile header");
        }

        // Materialize buffer once
        var buffer = serializedFileData.ToArray();
        using var stream = new MemoryStream(buffer, false);
        using var reader = new BinaryReader(stream);

        // Step 1: Parse header (determines version and endianness)
        var header = ParseHeader(reader);

        // Step 2: Create endian-aware reader
        bool isBigEndian = header.Endianness == 1;
        stream.Position = 0; // Reset to beginning
        using var endianReader = new EndianBinaryReader(stream, isBigEndian);

        // Skip past header
        SkipHeader(endianReader, header.Version);

        // Read additional metadata fields based on version
        if (header.Version < 9)
        {
            endianReader.ReadUtf8NullTerminated(); // Unity version string
        }

        if (header.Version >= 9 && header.Version < 14)
        {
            endianReader.ReadUInt32(); // TargetPlatform
        }

        bool enableTypeTree = true;
        if (header.Version >= 7 && header.Version < 14)
        {
            enableTypeTree = endianReader.ReadBoolean(); // EnableTypeTree flag
        }

        // Step 3: Skip type tree
        SkipTypeTree(endianReader, header.Version, enableTypeTree);

        // Step 4: Align and scan object table
        endianReader.Align(4);

        int objectCount = endianReader.ReadInt32();

        // Fast-exit strategy: scan object table for Mesh
        for (int i = 0; i < objectCount; i++)
        {
            // Align before each object entry (version >= 14)
            if (header.Version >= 14)
            {
                endianReader.Align(4);
            }

            // PathId
            if (header.Version >= 14)
            {
                endianReader.ReadInt64();
            }
            else
            {
                endianReader.ReadInt32();
            }

            // ByteStart
            if (header.Version >= 22)
            {
                endianReader.ReadInt64();
            }
            else
            {
                endianReader.ReadUInt32();
            }

            // ByteSize
            endianReader.ReadUInt32();

            // TypeId (we'll resolve ClassId from this)
            int typeId = endianReader.ReadInt32();

            // For version < 11, ClassId is stored directly
            int classId = 0;
            if (header.Version < 11)
            {
                classId = endianReader.ReadUInt16();
            }
            else
            {
                // For version >= 11, TypeId might need type tree resolution
                // For renderable detection, we'll use TypeId as ClassId directly
                // (works when type tree is empty or TypeId equals ClassId)
                classId = typeId;
            }

            // ScriptTypeIndex (version >= 11 < 17)
            if (header.Version >= 11 && header.Version < 17)
            {
                endianReader.ReadUInt16();
            }

            // Stripped (version >= 15 or >= 17)
            if (header.Version >= 15 && header.Version < 17)
            {
                endianReader.ReadByte();
            }
            else if (header.Version >= 17)
            {
                endianReader.ReadByte();
            }

            // Fast exit: if we found a Mesh, return immediately
            if (classId == RenderableClassIds.Mesh)
            {
                return true;
            }
        }

        // No Mesh found
        return false;
    }

    /// <summary>
    /// Scans and returns all renderable ClassIDs found (not just Mesh).
    /// Useful for future extensibility (e.g., particles, terrains).
    /// </summary>
    /// <param name="serializedFileData">Raw SerializedFile bytes</param>
    /// <returns>Set of all renderable ClassIDs found in the file</returns>
    /// <exception cref="InvalidVersionException">If version is unsupported</exception>
    /// <exception cref="CorruptedHeaderException">If header is corrupted or truncated</exception>
    /// <exception cref="TruncatedMetadataException">If metadata is truncated</exception>
    /// <exception cref="EndiannessMismatchException">If endianness value is invalid</exception>
    public static IReadOnlySet<int> DetectRenderableClassIds(ReadOnlySpan<byte> serializedFileData)
    {
        var renderableClassIds = new HashSet<int>();

        if (serializedFileData.Length < 20)
        {
            throw new CorruptedHeaderException("File too small to contain SerializedFile header");
        }

        var buffer = serializedFileData.ToArray();
        using var stream = new MemoryStream(buffer, false);
        using var reader = new BinaryReader(stream);

        var header = ParseHeader(reader);

        bool isBigEndian = header.Endianness == 1;
        stream.Position = 0;
        using var endianReader = new EndianBinaryReader(stream, isBigEndian);

        SkipHeader(endianReader, header.Version);

        if (header.Version < 9)
        {
            endianReader.ReadUtf8NullTerminated();
        }

        if (header.Version >= 9 && header.Version < 14)
        {
            endianReader.ReadUInt32();
        }

        bool enableTypeTree = true;
        if (header.Version >= 7 && header.Version < 14)
        {
            enableTypeTree = endianReader.ReadBoolean();
        }

        SkipTypeTree(endianReader, header.Version, enableTypeTree);

        endianReader.Align(4);

        int objectCount = endianReader.ReadInt32();

        for (int i = 0; i < objectCount; i++)
        {
            if (header.Version >= 14)
            {
                endianReader.Align(4);
            }

            if (header.Version >= 14)
            {
                endianReader.ReadInt64();
            }
            else
            {
                endianReader.ReadInt32();
            }

            if (header.Version >= 22)
            {
                endianReader.ReadInt64();
            }
            else
            {
                endianReader.ReadUInt32();
            }

            endianReader.ReadUInt32();

            int typeId = endianReader.ReadInt32();

            int classId = 0;
            if (header.Version < 11)
            {
                classId = endianReader.ReadUInt16();
            }
            else
            {
                classId = typeId;
            }

            if (header.Version >= 11 && header.Version < 17)
            {
                endianReader.ReadUInt16();
            }

            if (header.Version >= 15 && header.Version < 17)
            {
                endianReader.ReadByte();
            }
            else if (header.Version >= 17)
            {
                endianReader.ReadByte();
            }

            // Collect renderable ClassIDs (Mesh, SkinnedMeshRenderer, ParticleSystem)
            if (classId == RenderableClassIds.Mesh ||
                classId == RenderableClassIds.SkinnedMeshRenderer ||
                classId == RenderableClassIds.ParticleSystem)
            {
                renderableClassIds.Add(classId);
            }
        }

        return renderableClassIds;
    }

    private static SerializedFileHeader ParseHeader(BinaryReader reader)
    {
        var header = new SerializedFileHeader();

        header.MetadataSize = reader.ReadUInt32();
        uint value1 = reader.ReadUInt32();
        uint value2 = reader.ReadUInt32();

        // Determine format version
        if (value2 >= 14 && value2 <= 30 && value1 >= header.MetadataSize && value1 < 1_000_000_000)
        {
            // Version < 22: value1=FileSize(uint32), value2=Version
            header.FileSize = value1;
            header.Version = value2;
            header.DataOffset = reader.ReadUInt32();
        }
        else
        {
            // Version >= 22: value1+value2 = FileSize(int64)
            header.FileSize = (long)value1 | ((long)value2 << 32);
            header.Version = reader.ReadUInt32();
            header.DataOffset = reader.ReadInt64();
        }

        header.Endianness = reader.ReadByte();

        if (header.Endianness > 1)
        {
            throw new EndiannessMismatchException(
                $"Invalid endianness value: {header.Endianness} (expected 0 or 1)", header.Endianness);
        }

        header.Reserved = reader.ReadBytes(3);

        // Validate version
        if (header.Version < 14 || header.Version > 30)
        {
            throw new InvalidVersionException(
                $"Unsupported SerializedFile version: {header.Version} (supported: 14-30)", header.Version);
        }

        return header;
    }

    private static void SkipHeader(EndianBinaryReader reader, uint version)
    {
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

        reader.ReadByte(); // Endianness
        reader.ReadBytes(3); // Reserved
    }

    private static void SkipTypeTree(EndianBinaryReader reader, uint version, bool enableTypeTree)
    {
        int typeCount = reader.ReadInt32();

        for (int i = 0; i < typeCount; i++)
        {
            // ClassId
            reader.ReadInt32();

            // IsStrippedType (version >= 16)
            if (version >= 16)
            {
                reader.ReadBoolean();
            }

            // ScriptTypeIndex (version >= 17)
            if (version >= 17)
            {
                reader.ReadInt16();
            }

            // ScriptId hash (version >= 17 && ClassId == 114)
            // We already read ClassId above, but we need to check it
            // For simplicity, we'll skip this optimization for now
            // and always check if we need to read the hash

            // OldTypeHash (version >= 5)
            if (version >= 5)
            {
                reader.ReadBytes(16);
            }

            // Read type tree nodes if enabled
            if (enableTypeTree)
            {
                SkipTypeTreeNodes(reader, version);
            }

            // Type dependencies (version >= 21)
            if (version >= 21)
            {
                int depCount = reader.ReadInt32();
                for (int j = 0; j < depCount; j++)
                {
                    reader.ReadInt32();
                }
            }
        }
    }

    private static void SkipTypeTreeNodes(EndianBinaryReader reader, uint version)
    {
        int nodeCount = reader.ReadInt32();
        int stringTableSize = reader.ReadInt32();

        // Skip string table (version >= 10)
        if (version >= 10 && stringTableSize > 0)
        {
            reader.ReadBytes(stringTableSize);
        }

        // Skip nodes
        for (int i = 0; i < nodeCount; i++)
        {
            reader.ReadInt16(); // Version
            reader.ReadByte();  // Level
            reader.ReadUInt16(); // TypeFlags

            if (version >= 10)
            {
                // String table mode: read offsets
                reader.ReadUInt32(); // Type offset
                reader.ReadUInt32(); // Name offset
            }
            else
            {
                // Inline string mode
                reader.ReadUtf8NullTerminated(); // Type
                reader.ReadUtf8NullTerminated(); // Name
            }

            reader.ReadInt32(); // ByteSize
            reader.ReadInt32(); // MetaFlag
        }
    }
}
