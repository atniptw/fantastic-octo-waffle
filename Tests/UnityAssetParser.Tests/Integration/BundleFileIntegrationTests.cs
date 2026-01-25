using System.Text;
using UnityAssetParser.Bundle;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// Integration tests for BundleFile orchestration layer.
/// Tests the full parsing pipeline from stream to validated bundle.
/// </summary>
public class BundleFileIntegrationTests
{
    /// <summary>
    /// Tests parsing a minimal V6 bundle with uncompressed BlocksInfo and single node.
    /// </summary>
    [Fact]
    public void Parse_MinimalV6Bundle_Success()
    {
        // Arrange: Create a minimal valid UnityFS V6 bundle
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);

        // Act
        var bundle = BundleFile.Parse(stream);

        // Assert
        Assert.NotNull(bundle);
        Assert.Equal("UnityFS", bundle.Header.Signature);
        Assert.Equal(6u, bundle.Header.Version);
        Assert.NotNull(bundle.BlocksInfo);
        Assert.NotNull(bundle.DataRegion);
        Assert.Single(bundle.Nodes);
    }

    /// <summary>
    /// Tests TryParse with a valid bundle returns success.
    /// </summary>
    [Fact]
    public void TryParse_ValidBundle_ReturnsSuccess()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);

        // Act
        var result = BundleFile.TryParse(stream);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Bundle);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Tests TryParse with invalid signature returns error.
    /// </summary>
    [Fact]
    public void TryParse_InvalidSignature_ReturnsError()
    {
        // Arrange: Create bundle with wrong signature
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.UTF8.GetBytes("BadSig\0"));
        stream.Position = 0;

        // Act
        var result = BundleFile.TryParse(stream);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Bundle);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("signature", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests GetNode finds node by exact path match.
    /// </summary>
    [Fact]
    public void GetNode_ExactPath_ReturnsNode()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);

        // Act
        var node = bundle.GetNode("CAB-test");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("CAB-test", node.Path);
    }

    /// <summary>
    /// Tests GetNode with non-existent path returns null.
    /// </summary>
    [Fact]
    public void GetNode_NonExistentPath_ReturnsNull()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);

        // Act
        var node = bundle.GetNode("NonExistent");

        // Assert
        Assert.Null(node);
    }

    /// <summary>
    /// Tests ExtractNode returns correct data.
    /// </summary>
    [Fact]
    public void ExtractNode_ValidNode_ReturnsData()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);
        var node = bundle.Nodes[0];

        // Act
        var data = bundle.ExtractNode(node);

        // Assert
        Assert.NotNull(data);
        Assert.Equal(node.Size, data.Length);
    }

    /// <summary>
    /// Tests GetMetadataNode returns first node.
    /// </summary>
    [Fact]
    public void GetMetadataNode_BundleWithNodes_ReturnsFirstNode()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);

        // Act
        var metadataNode = bundle.GetMetadataNode();

        // Assert
        Assert.NotNull(metadataNode);
        Assert.Equal(bundle.Nodes[0], metadataNode);
    }

    /// <summary>
    /// Tests ToJson produces valid JSON with expected structure.
    /// </summary>
    [Fact]
    public void ToJson_ValidBundle_ProducesValidJson()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);

        // Act
        var json = bundle.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"header\"", json);
        Assert.Contains("\"storage_blocks\"", json);
        Assert.Contains("\"nodes\"", json);
        Assert.Contains("\"data_offset\"", json);
        Assert.Contains("\"signature\": \"UnityFS\"", json);
    }

    /// <summary>
    /// Tests ToMetadata produces metadata with correct values.
    /// </summary>
    [Fact]
    public void ToMetadata_ValidBundle_ProducesCorrectMetadata()
    {
        // Arrange
        var bundleBytes = CreateMinimalV6Bundle();
        using var stream = new MemoryStream(bundleBytes);
        var bundle = BundleFile.Parse(stream);

        // Act
        var metadata = bundle.ToMetadata();

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("UnityFS", metadata.Header.Signature);
        Assert.Equal(6u, metadata.Header.Version);
        Assert.NotEmpty(metadata.StorageBlocks);
        Assert.NotEmpty(metadata.Nodes);
    }

    /// <summary>
    /// Creates a minimal valid V6 UnityFS bundle for testing.
    /// Contains header, uncompressed BlocksInfo, and a single small data block with one node.
    /// </summary>
    private static byte[] CreateMinimalV6Bundle()
    {
        using var bundle = new MemoryStream();
        using var writer = new BinaryWriter(bundle);

        // === HEADER ===
        // Signature: "UnityFS\0"
        writer.Write(Encoding.UTF8.GetBytes("UnityFS"));
        writer.Write((byte)0);

        // Version: 6
        writer.Write((uint)6);

        // Unity version: "2020.3.48f1\0"
        writer.Write(Encoding.UTF8.GetBytes("2020.3.48f1"));
        writer.Write((byte)0);

        // Unity revision: "b805b124c6b7\0"
        writer.Write(Encoding.UTF8.GetBytes("b805b124c6b7"));
        writer.Write((byte)0);

        // Size: placeholder (will calculate later)
        long sizePosition = bundle.Position;
        writer.Write((long)0);

        // Compressed BlocksInfo size: placeholder
        long compressedSizePosition = bundle.Position;
        writer.Write((uint)0);

        // Uncompressed BlocksInfo size: placeholder
        long uncompressedSizePosition = bundle.Position;
        writer.Write((uint)0);

        // Flags: 0 (no compression, embedded BlocksInfo)
        writer.Write((uint)0);

        // Apply 4-byte alignment after header
        while (bundle.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }

        long blocksInfoStart = bundle.Position;

        // === BLOCKSINFO ===
        using var blocksInfo = new MemoryStream();
        using var blocksInfoWriter = new BinaryWriter(blocksInfo);

        // Hash placeholder (20 bytes, will calculate later)
        blocksInfoWriter.Write(new byte[20]);

        // Block count: 1
        blocksInfoWriter.Write((int)1);

        // Storage block: 16 bytes uncompressed, 16 bytes compressed, flags 0
        blocksInfoWriter.Write((uint)16); // uncompressed size
        blocksInfoWriter.Write((uint)16); // compressed size
        blocksInfoWriter.Write((ushort)0); // flags

        // 4-byte alignment after blocks
        while (blocksInfo.Position % 4 != 0)
        {
            blocksInfoWriter.Write((byte)0);
        }

        // Node count: 1
        blocksInfoWriter.Write((int)1);

        // Node: offset 0, size 16, flags 0, path "CAB-test\0"
        blocksInfoWriter.Write((long)0); // offset
        blocksInfoWriter.Write((long)16); // size
        blocksInfoWriter.Write((int)0); // flags
        blocksInfoWriter.Write(Encoding.UTF8.GetBytes("CAB-test"));
        blocksInfoWriter.Write((byte)0);

        // Calculate SHA1 hash of everything after the hash field
        byte[] blocksInfoBytes = blocksInfo.ToArray();
        byte[] payloadForHash = new byte[blocksInfoBytes.Length - 20];
        Array.Copy(blocksInfoBytes, 20, payloadForHash, 0, payloadForHash.Length);
        
        using (var sha1 = System.Security.Cryptography.SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(payloadForHash);
            Array.Copy(hash, 0, blocksInfoBytes, 0, 20);
        }

        // Write BlocksInfo to bundle
        writer.Write(blocksInfoBytes);
        long blocksInfoEnd = bundle.Position;

        // === DATA REGION ===
        // Single storage block: 16 bytes of dummy data
        byte[] dataBlock = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            dataBlock[i] = (byte)i;
        }
        writer.Write(dataBlock);

        long bundleEnd = bundle.Position;

        // === FIX UP HEADER ===
        bundle.Position = sizePosition;
        writer.Write(bundleEnd);

        uint blocksInfoSize = (uint)(blocksInfoEnd - blocksInfoStart);
        bundle.Position = compressedSizePosition;
        writer.Write(blocksInfoSize);
        bundle.Position = uncompressedSizePosition;
        writer.Write(blocksInfoSize);

        return bundle.ToArray();
    }
}
