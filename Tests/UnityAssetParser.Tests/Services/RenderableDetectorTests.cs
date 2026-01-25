using System.Diagnostics;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Services;

namespace UnityAssetParser.Tests.Services;

/// <summary>
/// Unit tests for RenderableDetector service.
/// Tests metadata-only scanning for Mesh objects with fast-exit strategy.
/// </summary>
public class RenderableDetectorTests
{
    #region Happy Path Tests

    [Fact]
    public void DetectRenderable_SerializedFileWithMesh_ReturnsTrue()
    {
        // Arrange: Create SerializedFile with Mesh (ClassID 43)
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_SerializedFileWithoutMesh_ReturnsFalse()
    {
        // Arrange: Create SerializedFile with Texture2D only (ClassID 28)
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: false);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DetectRenderable_MeshAtEnd_ReturnsTrue()
    {
        // Arrange: Create SerializedFile with multiple objects, Mesh at end
        var data = CreateSerializedFileWithMultipleObjects(version: 22, meshAtStart: false);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_MeshAtStart_ReturnsTrueWithFastExit()
    {
        // Arrange: Create SerializedFile with Mesh as first object
        var data = CreateSerializedFileWithMultipleObjects(version: 22, meshAtStart: true);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = RenderableDetector.DetectRenderable(data);
        stopwatch.Stop();

        // Assert
        Assert.True(result);
        // Fast exit should complete in <10ms (relaxed from 1ms for CI)
        Assert.True(stopwatch.ElapsedMilliseconds < 10, 
            $"Fast exit should be <10ms, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Version Variant Tests

    [Fact]
    public void DetectRenderable_Version19_WithMesh_ReturnsTrue()
    {
        // Arrange: Unity 2019 format (version 19)
        var data = CreateSerializedFileWithObjects(version: 19, hasMesh: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_Version22_WithMesh_ReturnsTrue()
    {
        // Arrange: Unity 2022 format (version 22)
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_Version14_WithMesh_ReturnsTrue()
    {
        // Arrange: Older version (14)
        var data = CreateSerializedFileWithObjects(version: 14, hasMesh: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_Version16_WithMesh_ReturnsTrue()
    {
        // Arrange: Version 16
        var data = CreateSerializedFileWithObjects(version: 16, hasMesh: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Endianness Tests

    [Fact]
    public void DetectRenderable_LittleEndian_DetectsClassIdCorrectly()
    {
        // Arrange: Little-endian SerializedFile (Endianness = 0)
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: true, isBigEndian: false);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_BigEndian_DetectsClassIdCorrectly()
    {
        // Arrange: Big-endian SerializedFile (Endianness = 1)
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: true, isBigEndian: true);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void DetectRenderable_EmptyObjectTable_ReturnsFalse()
    {
        // Arrange: SerializedFile with 0 objects
        var data = CreateSerializedFileWithEmptyObjectTable(version: 22);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DetectRenderable_SingleMeshObject_ReturnsTrue()
    {
        // Arrange: SerializedFile with single Mesh
        var data = CreateSerializedFileWithSingleMesh(version: 22);

        // Act
        var result = RenderableDetector.DetectRenderable(data);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DetectRenderable_MultipleMeshes_ReturnsTrueOnFirst()
    {
        // Arrange: SerializedFile with multiple Meshes
        var data = CreateSerializedFileWithMultipleMeshes(version: 22);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = RenderableDetector.DetectRenderable(data);
        stopwatch.Stop();

        // Assert
        Assert.True(result);
        // Should exit on first Mesh, not scan all
        Assert.True(stopwatch.ElapsedMilliseconds < 10);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void DetectRenderable_TruncatedHeader_ThrowsCorruptedHeaderException()
    {
        // Arrange: Header smaller than minimum
        byte[] data = new byte[10];

        // Act & Assert
        var ex = Assert.Throws<CorruptedHeaderException>(
            () => RenderableDetector.DetectRenderable(data));
        Assert.Contains("too small", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectRenderable_CorruptedVersion_ThrowsInvalidVersionException()
    {
        // Arrange: Invalid version (99)
        var data = CreateCorruptedSerializedFile(invalidVersion: 99);

        // Act & Assert
        Assert.Throws<InvalidVersionException>(
            () => RenderableDetector.DetectRenderable(data));
    }

    [Fact]
    public void DetectRenderable_InvalidEndianness_ThrowsEndiannessMismatchException()
    {
        // Arrange: Invalid endianness (2)
        var data = CreateCorruptedSerializedFile(invalidEndianness: 2);

        // Act & Assert
        var ex = Assert.Throws<EndiannessMismatchException>(
            () => RenderableDetector.DetectRenderable(data));
        Assert.Equal(2, ex.EndiannessValue);
    }

    #endregion

    #region DetectRenderableClassIds Tests

    [Fact]
    public void DetectRenderableClassIds_FileWithMesh_ReturnsMeshClassId()
    {
        // Arrange
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: true);

        // Act
        var classIds = RenderableDetector.DetectRenderableClassIds(data);

        // Assert
        Assert.Contains(RenderableDetector.RenderableClassIds.Mesh, classIds);
        Assert.Single(classIds);
    }

    [Fact]
    public void DetectRenderableClassIds_FileWithoutRenderables_ReturnsEmptySet()
    {
        // Arrange
        var data = CreateSerializedFileWithObjects(version: 22, hasMesh: false);

        // Act
        var classIds = RenderableDetector.DetectRenderableClassIds(data);

        // Assert
        Assert.Empty(classIds);
    }

    [Fact]
    public void DetectRenderableClassIds_FileWithMultipleRenderableTypes_ReturnsAllTypes()
    {
        // Arrange: File with Mesh and SkinnedMeshRenderer
        var data = CreateSerializedFileWithMixedRenderables(version: 22);

        // Act
        var classIds = RenderableDetector.DetectRenderableClassIds(data);

        // Assert
        Assert.Contains(RenderableDetector.RenderableClassIds.Mesh, classIds);
        Assert.Contains(RenderableDetector.RenderableClassIds.SkinnedMeshRenderer, classIds);
        Assert.Equal(2, classIds.Count);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void DetectRenderable_TypicalAsset_CompletesUnder50ms()
    {
        // Arrange: Typical asset size (~500 bytes metadata)
        var data = CreateTypicalSerializedFile();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = RenderableDetector.DetectRenderable(data);
        stopwatch.Stop();

        // Assert
        Assert.True(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Detection should complete <50ms, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Helper Methods - Test Fixture Creators

    private static byte[] CreateSerializedFileWithObjects(uint version, bool hasMesh, bool isBigEndian = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Calculate sizes
        int headerSize = version >= 22 ? 28 : 20;
        int typeTreeSize = 4; // Just type count (0)
        int objectCount = 1;
        int objectEntrySize = CalculateObjectEntrySize(version);
        int objectTableSize = 4 + (objectCount * objectEntrySize);
        int externalSize = 4; // Identifier count
        int metadataSize = headerSize + typeTreeSize + objectTableSize + externalSize + 20; // Add padding
        int objectDataSize = 100;
        int fileSize = metadataSize + objectDataSize;

        // Write header (always little-endian)
        writer.Write((uint)metadataSize);
        if (version >= 22)
        {
            writer.Write((long)fileSize);
            writer.Write((uint)version);
            writer.Write((long)metadataSize);
        }
        else
        {
            writer.Write((uint)fileSize);
            writer.Write((uint)version);
            writer.Write((uint)metadataSize);
        }
        writer.Write((byte)(isBigEndian ? 1 : 0));
        writer.Write(new byte[3]);

        // Metadata follows (with endianness applied if needed)
        if (isBigEndian)
        {
            WriteMetadataWithEndianness(stream, version, hasMesh, true);
        }
        else
        {
            WriteMetadataWithEndianness(stream, version, hasMesh, false);
        }

        // Pad to file size
        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static void WriteMetadataWithEndianness(MemoryStream stream, uint version, bool hasMesh, bool isBigEndian)
    {
        // Type tree (empty)
        WriteInt32(stream, 0, isBigEndian);

        // Align to 4 bytes
        while (stream.Position % 4 != 0)
        {
            stream.WriteByte(0);
        }

        // Object table
        WriteInt32(stream, 1, isBigEndian); // Object count

        // Align before object entry
        if (version >= 14)
        {
            while (stream.Position % 4 != 0)
            {
                stream.WriteByte(0);
            }
        }

        // Object entry
        if (version >= 14)
        {
            WriteInt64(stream, 1, isBigEndian); // PathId
        }
        else
        {
            WriteInt32(stream, 1, isBigEndian); // PathId
        }

        if (version >= 22)
        {
            WriteInt64(stream, 0, isBigEndian); // ByteStart
        }
        else
        {
            WriteUInt32(stream, 0, isBigEndian); // ByteStart
        }

        WriteUInt32(stream, 100, isBigEndian); // ByteSize

        int classId = hasMesh ? 43 : 28; // Mesh or Texture2D
        WriteInt32(stream, classId, isBigEndian); // TypeId (used as ClassId)

        if (version >= 11 && version < 17)
        {
            WriteUInt16(stream, 0xFFFF, isBigEndian); // ScriptTypeIndex
        }

        if (version >= 15)
        {
            stream.WriteByte(0); // Stripped
        }

        // Align to 4 bytes before file identifiers
        while (stream.Position % 4 != 0)
        {
            stream.WriteByte(0);
        }

        // File identifiers (empty)
        WriteInt32(stream, 0, isBigEndian);
    }

    private static void WriteInt32(Stream stream, int value, bool isBigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (isBigEndian && BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, 4);
    }

    private static void WriteUInt32(Stream stream, uint value, bool isBigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (isBigEndian && BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, 4);
    }

    private static void WriteInt64(Stream stream, long value, bool isBigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (isBigEndian && BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, 8);
    }

    private static void WriteUInt16(Stream stream, ushort value, bool isBigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (isBigEndian && BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, 2);
    }

    private static int CalculateObjectEntrySize(uint version)
    {
        int size = 0;
        size += version >= 14 ? 8 : 4; // PathId
        size += version >= 22 ? 8 : 4; // ByteStart
        size += 4; // ByteSize
        size += 4; // TypeId
        if (version < 11)
        {
            size += 2; // ClassId
        }
        if (version >= 11 && version < 17)
        {
            size += 2; // ScriptTypeIndex
        }
        if (version >= 15)
        {
            size += 1; // Stripped
        }
        return size + 4; // Add alignment padding
    }

    private static byte[] CreateSerializedFileWithMultipleObjects(uint version, bool meshAtStart)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int headerSize = version >= 22 ? 28 : 20;
        int objectCount = 3;
        int objectEntrySize = CalculateObjectEntrySize(version);
        int metadataSize = headerSize + 4 + (objectCount * objectEntrySize) + 4 + 20;
        int fileSize = metadataSize + 300;

        // Header
        writer.Write((uint)metadataSize);
        if (version >= 22)
        {
            writer.Write((long)fileSize);
            writer.Write((uint)version);
            writer.Write((long)metadataSize);
        }
        else
        {
            writer.Write((uint)fileSize);
            writer.Write((uint)version);
            writer.Write((uint)metadataSize);
        }
        writer.Write((byte)0);
        writer.Write(new byte[3]);

        // Type tree (empty)
        writer.Write((int)0);

        // Align
        while (stream.Position % 4 != 0) writer.Write((byte)0);

        // Object table
        writer.Write((int)objectCount);

        // Write objects
        int[] classIds = meshAtStart 
            ? [43, 28, 28]  // Mesh first
            : [28, 28, 43]; // Mesh last

        for (int i = 0; i < objectCount; i++)
        {
            if (version >= 14)
            {
                while (stream.Position % 4 != 0) writer.Write((byte)0);
            }

            if (version >= 14)
            {
                writer.Write((long)(i + 1));
            }
            else
            {
                writer.Write((int)(i + 1));
            }

            if (version >= 22)
            {
                writer.Write((long)(i * 100));
            }
            else
            {
                writer.Write((uint)(i * 100));
            }

            writer.Write((uint)100);
            writer.Write((int)classIds[i]);

            if (version >= 11 && version < 17)
            {
                writer.Write((ushort)0xFFFF);
            }

            if (version >= 15)
            {
                writer.Write((byte)0);
            }
        }

        // Align and file identifiers
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)0);

        // Pad to file size
        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static byte[] CreateSerializedFileWithEmptyObjectTable(uint version)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int headerSize = version >= 22 ? 28 : 20;
        int metadataSize = headerSize + 4 + 4 + 10;
        int fileSize = metadataSize + 50;

        writer.Write((uint)metadataSize);
        if (version >= 22)
        {
            writer.Write((long)fileSize);
            writer.Write((uint)version);
            writer.Write((long)metadataSize);
        }
        else
        {
            writer.Write((uint)fileSize);
            writer.Write((uint)version);
            writer.Write((uint)metadataSize);
        }
        writer.Write((byte)0);
        writer.Write(new byte[3]);

        writer.Write((int)0); // Type count
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)0); // Object count
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)0); // Identifier count

        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static byte[] CreateSerializedFileWithSingleMesh(uint version)
    {
        return CreateSerializedFileWithObjects(version, hasMesh: true);
    }

    private static byte[] CreateSerializedFileWithMultipleMeshes(uint version)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int headerSize = version >= 22 ? 28 : 20;
        int objectCount = 3;
        int objectEntrySize = CalculateObjectEntrySize(version);
        int metadataSize = headerSize + 4 + (objectCount * objectEntrySize) + 4 + 20;
        int fileSize = metadataSize + 300;

        writer.Write((uint)metadataSize);
        if (version >= 22)
        {
            writer.Write((long)fileSize);
            writer.Write((uint)version);
            writer.Write((long)metadataSize);
        }
        else
        {
            writer.Write((uint)fileSize);
            writer.Write((uint)version);
            writer.Write((uint)metadataSize);
        }
        writer.Write((byte)0);
        writer.Write(new byte[3]);

        writer.Write((int)0); // Type tree

        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)objectCount);

        // All Meshes
        for (int i = 0; i < objectCount; i++)
        {
            if (version >= 14)
            {
                while (stream.Position % 4 != 0) writer.Write((byte)0);
            }

            if (version >= 14) writer.Write((long)(i + 1));
            else writer.Write((int)(i + 1));

            if (version >= 22) writer.Write((long)(i * 100));
            else writer.Write((uint)(i * 100));

            writer.Write((uint)100);
            writer.Write((int)43); // Mesh

            if (version >= 11 && version < 17) writer.Write((ushort)0xFFFF);
            if (version >= 15) writer.Write((byte)0);
        }

        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)0);

        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static byte[] CreateCorruptedSerializedFile(uint? invalidVersion = null, byte? invalidEndianness = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        uint version = invalidVersion ?? 22;
        byte endianness = invalidEndianness ?? 0;

        writer.Write((uint)100);
        if (version >= 22 || invalidVersion >= 22)
        {
            writer.Write((long)1000);
            writer.Write((uint)version);
            writer.Write((long)500);
        }
        else
        {
            writer.Write((uint)1000);
            writer.Write((uint)version);
            writer.Write((uint)500);
        }
        writer.Write((byte)endianness);
        writer.Write(new byte[3]);

        return stream.ToArray();
    }

    private static byte[] CreateSerializedFileWithMixedRenderables(uint version)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        int headerSize = version >= 22 ? 28 : 20;
        int objectCount = 2;
        int objectEntrySize = CalculateObjectEntrySize(version);
        int metadataSize = headerSize + 4 + (objectCount * objectEntrySize) + 4 + 20;
        int fileSize = metadataSize + 200;

        writer.Write((uint)metadataSize);
        if (version >= 22)
        {
            writer.Write((long)fileSize);
            writer.Write((uint)version);
            writer.Write((long)metadataSize);
        }
        else
        {
            writer.Write((uint)fileSize);
            writer.Write((uint)version);
            writer.Write((uint)metadataSize);
        }
        writer.Write((byte)0);
        writer.Write(new byte[3]);

        writer.Write((int)0); // Type tree
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)objectCount);

        // Mesh
        if (version >= 14) while (stream.Position % 4 != 0) writer.Write((byte)0);
        if (version >= 14) writer.Write((long)1);
        else writer.Write((int)1);
        if (version >= 22) writer.Write((long)0);
        else writer.Write((uint)0);
        writer.Write((uint)100);
        writer.Write((int)43); // Mesh
        if (version >= 11 && version < 17) writer.Write((ushort)0xFFFF);
        if (version >= 15) writer.Write((byte)0);

        // SkinnedMeshRenderer
        if (version >= 14) while (stream.Position % 4 != 0) writer.Write((byte)0);
        if (version >= 14) writer.Write((long)2);
        else writer.Write((int)2);
        if (version >= 22) writer.Write((long)100);
        else writer.Write((uint)100);
        writer.Write((uint)50);
        writer.Write((int)137); // SkinnedMeshRenderer
        if (version >= 11 && version < 17) writer.Write((ushort)0xFFFF);
        if (version >= 15) writer.Write((byte)0);

        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write((int)0);

        while (stream.Position < fileSize)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static byte[] CreateTypicalSerializedFile()
    {
        // Create a typical asset with ~500 bytes metadata
        return CreateSerializedFileWithMultipleObjects(version: 22, meshAtStart: false);
    }

    #endregion
}
