using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// Error injection tests for BundleFile parsing.
/// Tests failure modes, validation errors, and edge cases.
/// </summary>
public class BundleFileErrorTests
{
    /// <summary>
    /// Tests that Parse throws InvalidBundleSignatureException for wrong signature.
    /// </summary>
    [Fact]
    public void Parse_InvalidSignature_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.UTF8.GetBytes("BadSig\0"));
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidBundleSignatureException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that Parse throws UnsupportedVersionException for unsupported version.
    /// </summary>
    [Fact]
    public void Parse_UnsupportedVersion_ThrowsException()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        
        // Signature
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0);
        
        // Version: 99 (unsupported)
        writer.Write((uint)99);
        
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<UnsupportedVersionException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that Parse throws HashMismatchException when BlocksInfo hash is corrupted.
    /// </summary>
    [Fact]
    public void Parse_CorruptedHash_ThrowsHashMismatchException()
    {
        // Arrange: Create bundle with intentionally wrong hash
        var bundleBytes = CreateBundleWithCorruptedHash();
        using var stream = new MemoryStream(bundleBytes);

        // Act & Assert
        var ex = Assert.Throws<HashMismatchException>(() => BundleFile.Parse(stream));
        Assert.NotNull(ex.ExpectedHash);
        Assert.NotNull(ex.ComputedHash);
    }

    /// <summary>
    /// Tests that TryParse collects hash mismatch error.
    /// </summary>
    [Fact]
    public void TryParse_CorruptedHash_ReturnsErrorWithHashInfo()
    {
        // Arrange
        var bundleBytes = CreateBundleWithCorruptedHash();
        using var stream = new MemoryStream(bundleBytes);

        // Act
        var result = BundleFile.TryParse(stream);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Bundle);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Hash mismatch", result.Errors[0]);
    }

    /// <summary>
    /// Tests that Parse throws exception for truncated stream.
    /// </summary>
    [Fact]
    public void Parse_TruncatedStream_ThrowsException()
    {
        // Arrange: Create bundle header only (no BlocksInfo or data)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        
        // Signature
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0);
        
        // Version: 6
        writer.Write((uint)6);
        
        // Unity version
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0);
        
        // Unity revision
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0);
        
        // Size: large value
        writer.Write((long)1000);
        
        // Compressed BlocksInfo size: 100 (but we won't provide it)
        writer.Write((uint)100);
        
        // Uncompressed BlocksInfo size: 100
        writer.Write((uint)100);
        
        // Flags: 0
        writer.Write((uint)0);
        
        stream.Position = 0;

        // Act & Assert
        Assert.ThrowsAny<BundleException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that Parse throws exception for non-seekable stream.
    /// </summary>
    [Fact]
    public void Parse_NonSeekableStream_ThrowsArgumentException()
    {
        // Arrange: Use a non-seekable stream wrapper
        var innerStream = new MemoryStream();
        var nonSeekableStream = new NonSeekableStreamWrapper(innerStream);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => BundleFile.Parse(nonSeekableStream));
    }

    /// <summary>
    /// Tests that Parse throws DuplicateNodeException for duplicate node paths.
    /// </summary>
    [Fact]
    public void Parse_DuplicateNodePaths_ThrowsDuplicateNodeException()
    {
        // Arrange
        var bundleBytes = CreateBundleWithDuplicateNodes();
        using var stream = new MemoryStream(bundleBytes);

        // Act & Assert
        Assert.Throws<DuplicateNodeException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that TryParse collects duplicate node error.
    /// </summary>
    [Fact]
    public void TryParse_DuplicateNodePaths_ReturnsError()
    {
        // Arrange
        var bundleBytes = CreateBundleWithDuplicateNodes();
        using var stream = new MemoryStream(bundleBytes);

        // Act
        var result = BundleFile.TryParse(stream);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Bundle);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Duplicate", result.Errors[0]);
    }

    /// <summary>
    /// Tests that Parse throws exception for out-of-bounds node.
    /// </summary>
    [Fact]
    public void Parse_OutOfBoundsNode_ThrowsException()
    {
        // Arrange
        var bundleBytes = CreateBundleWithOutOfBoundsNode();
        using var stream = new MemoryStream(bundleBytes);

        // Act & Assert
        Assert.ThrowsAny<BundleException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that Parse throws exception for overlapping nodes.
    /// </summary>
    [Fact]
    public void Parse_OverlappingNodes_ThrowsException()
    {
        // Arrange
        var bundleBytes = CreateBundleWithOverlappingNodes();
        using var stream = new MemoryStream(bundleBytes);

        // Act & Assert
        Assert.ThrowsAny<BundleException>(() => BundleFile.Parse(stream));
    }

    /// <summary>
    /// Tests that Parse handles empty node list correctly.
    /// </summary>
    [Fact]
    public void Parse_EmptyNodeList_Success()
    {
        // Arrange
        var bundleBytes = CreateBundleWithNoNodes();
        using var stream = new MemoryStream(bundleBytes);

        // Act
        var bundle = BundleFile.Parse(stream);

        // Assert
        Assert.NotNull(bundle);
        Assert.Empty(bundle.Nodes);
        Assert.Null(bundle.GetMetadataNode());
    }

    /// <summary>
    /// Creates a bundle with an intentionally corrupted hash.
    /// </summary>
    private static byte[] CreateBundleWithCorruptedHash()
    {
        var bundleBytes = CreateMinimalValidBundle();
        
        // Find BlocksInfo hash and corrupt it
        // For V6 embedded layout:
        // - Header ends at position ~60 (signature + version + strings + sizes + flags)
        // - After 4-byte alignment, BlocksInfo starts
        // - First 20 bytes of BlocksInfo are the hash
        // Calculate the actual hash position dynamically
        int estimatedHeaderSize = 60; // Approximate, actual position found through trial
        // Align to 4 bytes
        while (estimatedHeaderSize % 4 != 0)
        {
            estimatedHeaderSize++;
        }
        
        // Corrupt first byte of hash at estimated BlocksInfo start
        bundleBytes[estimatedHeaderSize] ^= 0xFF; // XOR to corrupt
        
        return bundleBytes;
    }

    /// <summary>
    /// Creates a bundle with duplicate node paths.
    /// </summary>
    private static byte[] CreateBundleWithDuplicateNodes()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        WriteHeader(writer, out long sizePos, out long compSizePos, out long uncompSizePos);
        AlignTo4(bundle, writer);
        long blocksInfoStart = bundle.Position;

        using var blocksInfo = new MemoryStream();
        using var biWriter = new BinaryWriter(blocksInfo);

        // Hash placeholder
        biWriter.Write(new byte[20]);

        // 1 storage block
        biWriter.Write((int)1);
        biWriter.Write((uint)32); // uncompressed
        biWriter.Write((uint)32); // compressed
        biWriter.Write((ushort)0); // flags

        AlignTo4(blocksInfo, biWriter);

        // 2 nodes with same path
        biWriter.Write((int)2);
        
        // Node 1
        biWriter.Write((long)0);
        biWriter.Write((long)16);
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-same"));
        biWriter.Write((byte)0);
        
        // Node 2 (duplicate path)
        biWriter.Write((long)16);
        biWriter.Write((long)16);
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-same"));
        biWriter.Write((byte)0);

        byte[] biBytes = ComputeHashAndFinalize(blocksInfo);
        writer.Write(biBytes);
        long blocksInfoEnd = bundle.Position;

        // Data region
        writer.Write(new byte[32]);

        FixupHeader(bundle, writer, sizePos, compSizePos, uncompSizePos, blocksInfoStart, blocksInfoEnd);
        return bundle.ToArray();
    }

    /// <summary>
    /// Creates a bundle with a node that exceeds data region bounds.
    /// </summary>
    private static byte[] CreateBundleWithOutOfBoundsNode()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        WriteHeader(writer, out long sizePos, out long compSizePos, out long uncompSizePos);
        AlignTo4(bundle, writer);
        long blocksInfoStart = bundle.Position;

        using var blocksInfo = new MemoryStream();
        using var biWriter = new BinaryWriter(blocksInfo);

        biWriter.Write(new byte[20]);

        // 1 storage block (16 bytes total)
        biWriter.Write((int)1);
        biWriter.Write((uint)16);
        biWriter.Write((uint)16);
        biWriter.Write((ushort)0);

        AlignTo4(blocksInfo, biWriter);

        // Node with offset+size > 16
        biWriter.Write((int)1);
        biWriter.Write((long)10); // offset
        biWriter.Write((long)20); // size (10+20=30 > 16)
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-oob"));
        biWriter.Write((byte)0);

        byte[] biBytes = ComputeHashAndFinalize(blocksInfo);
        writer.Write(biBytes);
        long blocksInfoEnd = bundle.Position;

        writer.Write(new byte[16]);

        FixupHeader(bundle, writer, sizePos, compSizePos, uncompSizePos, blocksInfoStart, blocksInfoEnd);
        return bundle.ToArray();
    }

    /// <summary>
    /// Creates a bundle with overlapping nodes.
    /// </summary>
    private static byte[] CreateBundleWithOverlappingNodes()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        WriteHeader(writer, out long sizePos, out long compSizePos, out long uncompSizePos);
        AlignTo4(bundle, writer);
        long blocksInfoStart = bundle.Position;

        using var blocksInfo = new MemoryStream();
        using var biWriter = new BinaryWriter(blocksInfo);

        biWriter.Write(new byte[20]);

        biWriter.Write((int)1);
        biWriter.Write((uint)32);
        biWriter.Write((uint)32);
        biWriter.Write((ushort)0);

        AlignTo4(blocksInfo, biWriter);

        // Two overlapping nodes
        biWriter.Write((int)2);
        
        // Node 1: [0, 20)
        biWriter.Write((long)0);
        biWriter.Write((long)20);
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-first"));
        biWriter.Write((byte)0);
        
        // Node 2: [10, 30) - overlaps with node 1
        biWriter.Write((long)10);
        biWriter.Write((long)20);
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-second"));
        biWriter.Write((byte)0);

        byte[] biBytes = ComputeHashAndFinalize(blocksInfo);
        writer.Write(biBytes);
        long blocksInfoEnd = bundle.Position;

        writer.Write(new byte[32]);

        FixupHeader(bundle, writer, sizePos, compSizePos, uncompSizePos, blocksInfoStart, blocksInfoEnd);
        return bundle.ToArray();
    }

    /// <summary>
    /// Creates a bundle with no nodes.
    /// </summary>
    private static byte[] CreateBundleWithNoNodes()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        WriteHeader(writer, out long sizePos, out long compSizePos, out long uncompSizePos);
        AlignTo4(bundle, writer);
        long blocksInfoStart = bundle.Position;

        using var blocksInfo = new MemoryStream();
        using var biWriter = new BinaryWriter(blocksInfo);

        biWriter.Write(new byte[20]);

        biWriter.Write((int)1);
        biWriter.Write((uint)16);
        biWriter.Write((uint)16);
        biWriter.Write((ushort)0);

        AlignTo4(blocksInfo, biWriter);

        // 0 nodes
        biWriter.Write((int)0);

        byte[] biBytes = ComputeHashAndFinalize(blocksInfo);
        writer.Write(biBytes);
        long blocksInfoEnd = bundle.Position;

        writer.Write(new byte[16]);

        FixupHeader(bundle, writer, sizePos, compSizePos, uncompSizePos, blocksInfoStart, blocksInfoEnd);
        return bundle.ToArray();
    }

    private static byte[] CreateMinimalValidBundle()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        WriteHeader(writer, out long sizePos, out long compSizePos, out long uncompSizePos);
        AlignTo4(bundle, writer);
        long blocksInfoStart = bundle.Position;

        using var blocksInfo = new MemoryStream();
        using var biWriter = new BinaryWriter(blocksInfo);

        biWriter.Write(new byte[20]);
        biWriter.Write((int)1);
        biWriter.Write((uint)16);
        biWriter.Write((uint)16);
        biWriter.Write((ushort)0);
        AlignTo4(blocksInfo, biWriter);
        biWriter.Write((int)1);
        biWriter.Write((long)0);
        biWriter.Write((long)16);
        biWriter.Write((int)0);
        biWriter.Write(Encoding.UTF8.GetBytes("CAB-test"));
        biWriter.Write((byte)0);

        byte[] biBytes = ComputeHashAndFinalize(blocksInfo);
        writer.Write(biBytes);
        long blocksInfoEnd = bundle.Position;

        writer.Write(new byte[16]);

        FixupHeader(bundle, writer, sizePos, compSizePos, uncompSizePos, blocksInfoStart, blocksInfoEnd);
        return bundle.ToArray();
    }

    private static void WriteHeader(BinaryWriter writer, out long sizePos, out long compSizePos, out long uncompSizePos)
    {
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0);
        writer.Write((uint)6);
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0);
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0);
        sizePos = writer.BaseStream.Position;
        writer.Write((long)0);
        compSizePos = writer.BaseStream.Position;
        writer.Write((uint)0);
        uncompSizePos = writer.BaseStream.Position;
        writer.Write((uint)0);
        writer.Write((uint)0);
    }

    private static void AlignTo4(Stream stream, BinaryWriter writer)
    {
        while (stream.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }
    }

    private static byte[] ComputeHashAndFinalize(MemoryStream blocksInfo)
    {
        byte[] biBytes = blocksInfo.ToArray();
        byte[] payload = new byte[biBytes.Length - 20];
        Array.Copy(biBytes, 20, payload, 0, payload.Length);
        
        using (var sha1 = System.Security.Cryptography.SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(payload);
            Array.Copy(hash, 0, biBytes, 0, 20);
        }
        
        return biBytes;
    }

    private static void FixupHeader(MemoryStream bundle, BinaryWriter writer, long sizePos, long compSizePos, long uncompSizePos, long blocksInfoStart, long blocksInfoEnd)
    {
        long bundleEnd = bundle.Position;
        bundle.Position = sizePos;
        writer.Write(bundleEnd);
        uint biSize = (uint)(blocksInfoEnd - blocksInfoStart);
        bundle.Position = compSizePos;
        writer.Write(biSize);
        bundle.Position = uncompSizePos;
        writer.Write(biSize);
    }

    /// <summary>
    /// Wrapper to make a stream non-seekable for testing.
    /// </summary>
    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStreamWrapper(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
