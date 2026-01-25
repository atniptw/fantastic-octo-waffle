using UnityAssetParser.Exceptions;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Tests.SerializedFile;

/// <summary>
/// Unit tests for SerializedFile parsing.
/// Tests header parsing, type tree, object table, and validation.
/// </summary>
public class SerializedFileTests
{
    [Fact]
    public void Parse_EmptyData_ThrowsCorruptedHeaderException()
    {
        // Arrange
        byte[] data = [];

        // Act & Assert
        var ex = Assert.Throws<CorruptedHeaderException>(() => UnityAssetParser.SerializedFile.SerializedFile.Parse(data));
        Assert.Contains("too small", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TooSmallData_ThrowsCorruptedHeaderException()
    {
        // Arrange
        byte[] data = new byte[10]; // Less than minimum header size

        // Act & Assert
        Assert.Throws<CorruptedHeaderException>(() => UnityAssetParser.SerializedFile.SerializedFile.Parse(data));
    }

    [Fact]
    public void Parse_InvalidEndianness_ThrowsEndiannessMismatchException()
    {
        // Arrange: Create minimal header with invalid endianness (2)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Version 22 header
        writer.Write((uint)100);      // MetadataSize
        writer.Write((long)1000);     // FileSize
        writer.Write((uint)22);       // Version
        writer.Write((long)500);      // DataOffset
        writer.Write((byte)2);        // Invalid Endianness
        writer.Write(new byte[3]);    // Reserved

        byte[] data = stream.ToArray();

        // Act & Assert
        var ex = Assert.Throws<EndiannessMismatchException>(
            () => UnityAssetParser.SerializedFile.SerializedFile.Parse(data));
        Assert.Equal(2, ex.EndiannessValue);
    }

    [Fact]
    public void Parse_UnsupportedVersion_ThrowsInvalidVersionException()
    {
        // Arrange: Create header with unsupported version (99)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((uint)100);      // MetadataSize
        writer.Write((long)1000);     // FileSize
        writer.Write((uint)99);       // Invalid Version
        writer.Write((long)500);      // DataOffset
        writer.Write((byte)0);        // Endianness
        writer.Write(new byte[3]);    // Reserved

        byte[] data = stream.ToArray();

        // Act & Assert
        var ex = Assert.Throws<InvalidVersionException>(
            () => UnityAssetParser.SerializedFile.SerializedFile.Parse(data));
        Assert.Equal(99u, ex.Version);
    }

    [Fact]
    public void TryParse_InvalidData_ReturnsFalseWithError()
    {
        // Arrange
        byte[] data = new byte[10];

        // Act
        bool result = UnityAssetParser.SerializedFile.SerializedFile.TryParse(data, out var parsed, out var error);

        // Assert
        Assert.False(result);
        Assert.Null(parsed);
        Assert.NotNull(error);
        Assert.Contains("too small", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindObject_NonExistentPathId_ReturnsNull()
    {
        // Arrange: Create minimal valid SerializedFile
        var serializedFile = CreateMinimalSerializedFile();

        // Act
        var result = serializedFile.FindObject(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetObjectsByClassId_ReturnsMatchingObjects()
    {
        // Arrange: Create SerializedFile with mixed objects
        var serializedFile = CreateSerializedFileWithObjects();

        // Act
        var meshObjects = serializedFile.GetObjectsByClassId(43).ToList();

        // Assert
        Assert.NotEmpty(meshObjects);
        Assert.All(meshObjects, obj => Assert.Equal(43, obj.ClassId));
    }

    [Fact]
    public void ReadObjectData_ValidObject_ReturnsCorrectData()
    {
        // Arrange
        var serializedFile = CreateSerializedFileWithObjectData();
        var obj = serializedFile.Objects.First();

        // Act
        var data = serializedFile.ReadObjectData(obj);

        // Assert
        Assert.NotEmpty(data.ToArray());
        Assert.Equal(obj.ByteSize, (uint)data.Length);
    }

    [Fact]
    public void ReadObjectData_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        var serializedFile = CreateMinimalSerializedFile();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializedFile.ReadObjectData(null!));
    }

    // Helper methods to create test fixtures

    private static UnityAssetParser.SerializedFile.SerializedFile CreateMinimalSerializedFile()
    {
        // Create a minimal valid SerializedFile (version 22, no objects)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Calculate sizes
        int headerSize = 28; // MetadataSize(4) + FileSize(8) + Version(4) + DataOffset(8) + Endianness(1) + Reserved(3)
        int metadataSize = headerSize + 4 + 4 + 4; // Header + type count + object count + identifier count
        int fileSize = metadataSize + 100; // Add some padding for object data region

        // Header (version 22 format)
        writer.Write((uint)metadataSize);  // MetadataSize
        writer.Write((long)fileSize);      // FileSize (int64)
        writer.Write((uint)22);            // Version
        writer.Write((long)metadataSize);  // DataOffset
        writer.Write((byte)0);             // Endianness (little)
        writer.Write(new byte[3]);         // Reserved

        // Type tree (empty)
        writer.Write((int)0);              // Type count

        // Object table (empty)
        writer.Write((int)0);              // Object count

        // File identifiers (empty)
        writer.Write((int)0);              // Identifier count

        // Pad to FileSize
        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        byte[] data = stream.ToArray();
        return UnityAssetParser.SerializedFile.SerializedFile.Parse(data);
    }

    private static UnityAssetParser.SerializedFile.SerializedFile CreateSerializedFileWithObjects()
    {
        // Create SerializedFile with some objects including Mesh (ClassID 43)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Calculate header size (version 22)
        int headerSize = 28; // MetadataSize(4) + FileSize(8) + Version(4) + DataOffset(8) + Endianness(1) + Reserved(3)
        
        // Calculate metadata sections
        int typeTreeSize = 4; // Type count only (empty type tree for simplicity)
        int objectCount = 2;
        // For version 22: align + PathId(8) + ByteStart(8) + ByteSize(4) + TypeId(4) + Stripped(1) = 25 bytes per object (with alignment)
        int objectEntrySize = 29; // With 4-byte alignment padding
        int objectTableSize = 4 + (objectCount * objectEntrySize); // Object count + entries
        int externalSize = 4; // Identifier count
        
        int metadataSize = headerSize + typeTreeSize + objectTableSize + externalSize;
        int objectDataSize = 150; // Space for object data
        int fileSize = metadataSize + objectDataSize;

        // Header (version 22)
        writer.Write((uint)metadataSize);
        writer.Write((long)fileSize);
        writer.Write((uint)22);
        writer.Write((long)metadataSize);
        writer.Write((byte)0);             // Little endian
        writer.Write(new byte[3]);         // Reserved

        // Type tree (empty - will use TypeId as ClassId)
        writer.Write((int)0);              // Type count

        // Align to 4 bytes before object table
        while (stream.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }

        // Object table
        writer.Write((int)objectCount);

        // Object 1 (Mesh, ClassID 43 via TypeId)
        while (stream.Position % 4 != 0) writer.Write((byte)0); // Align
        writer.Write((long)1);             // PathId
        writer.Write((long)0);             // ByteStart
        writer.Write((uint)100);           // ByteSize
        writer.Write((int)43);             // TypeId (will be used as ClassId since no type tree)
        writer.Write((byte)0);             // Stripped

        // Object 2 (Texture, ClassID 28)
        while (stream.Position % 4 != 0) writer.Write((byte)0); // Align
        writer.Write((long)2);             // PathId
        writer.Write((long)100);           // ByteStart
        writer.Write((uint)50);            // ByteSize
        writer.Write((int)28);             // TypeId (Texture2D)
        writer.Write((byte)0);             // Stripped

        // Align to 4 bytes before file identifiers
        while (stream.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }

        // File identifiers (empty)
        writer.Write((int)0);

        // Align to metadata size
        while (stream.Position < metadataSize)
        {
            writer.Write((byte)0);
        }

        // Object data region (fill with dummy data)
        byte[] objectData = new byte[objectDataSize];
        writer.Write(objectData);

        byte[] data = stream.ToArray();
        return UnityAssetParser.SerializedFile.SerializedFile.Parse(data);
    }

    private static UnityAssetParser.SerializedFile.SerializedFile CreateSerializedFileWithObjectData()
    {
        return CreateSerializedFileWithObjects();
    }
}
