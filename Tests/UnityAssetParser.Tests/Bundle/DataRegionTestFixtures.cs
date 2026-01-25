using UnityAssetParser.Bundle;

namespace UnityAssetParser.Tests.Bundle;

/// <summary>
/// Test fixtures for data region and node extraction tests.
/// </summary>
public static class DataRegionTestFixtures
{
    /// <summary>
    /// Simple uncompressed "HelloWorld" data.
    /// </summary>
    public static byte[] HelloWorldBytes => "HelloWorld"u8.ToArray();

    /// <summary>
    /// Simple uncompressed "Test123" data.
    /// </summary>
    public static byte[] Test123Bytes => "Test123"u8.ToArray();

    /// <summary>
    /// Sample node referencing start of HelloWorld.
    /// </summary>
    public static NodeInfo HelloNodeFull => new()
    {
        Path = "archive:/CAB-test/file1.bin",
        Offset = 0,
        Size = 10,
        Flags = 0
    };

    /// <summary>
    /// Sample node with offset into data.
    /// </summary>
    public static NodeInfo PartialNode => new()
    {
        Path = "archive:/CAB-test/file2.bin",
        Offset = 5,
        Size = 5,
        Flags = 0
    };

    /// <summary>
    /// Sample .resS node.
    /// </summary>
    public static NodeInfo ResSNode => new()
    {
        Path = "archive:/CAB-abc123/CAB-abc123.resS",
        Offset = 0,
        Size = 20,
        Flags = 0
    };

    /// <summary>
    /// Uncompressed storage block.
    /// </summary>
    public static StorageBlock UncompressedBlock(uint size) => new()
    {
        UncompressedSize = size,
        CompressedSize = size,
        Flags = 0 // None compression
    };

    /// <summary>
    /// LZ4-compressed storage block.
    /// </summary>
    public static StorageBlock Lz4Block(uint uncompressedSize, uint compressedSize) => new()
    {
        UncompressedSize = uncompressedSize,
        CompressedSize = compressedSize,
        Flags = 2 // LZ4 compression
    };

    /// <summary>
    /// Block with invalid reserved bits set.
    /// </summary>
    public static StorageBlock InvalidReservedBitsBlock => new()
    {
        UncompressedSize = 10,
        CompressedSize = 10,
        Flags = 0xFF80 // All reserved bits set
    };

    /// <summary>
    /// StreamingInfo referencing a .resS slice.
    /// </summary>
    public static StreamingInfo ResSSlice(long offset, long size) => new()
    {
        Path = "archive:/CAB-abc123/CAB-abc123.resS",
        Offset = offset,
        Size = size
    };
}
