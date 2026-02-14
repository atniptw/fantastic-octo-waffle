namespace UnityAssetParser.Bundle;

/// <summary>
/// Compression methods used in UnityFS bundles.
/// Maps to the lower 6 bits (0x3F) of the Flags field.
/// </summary>
public enum CompressionType : byte
{
    /// <summary>
    /// No compression applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// LZMA compression (Lempel-Ziv-Markov chain Algorithm).
    /// </summary>
    LZMA = 1,

    /// <summary>
    /// LZ4 compression (fast compression/decompression).
    /// </summary>
    LZ4 = 2,

    /// <summary>
    /// LZ4HC compression (LZ4 High Compression).
    /// </summary>
    LZ4HC = 3,

    /// <summary>
    /// LZHAM compression (rare, may not be supported by all implementations).
    /// </summary>
    LZHAM = 4
}
