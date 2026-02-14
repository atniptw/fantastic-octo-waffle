namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents the unified data region reconstructed from storage blocks.
/// Provides safe slice access with bounds validation.
/// </summary>
public sealed class DataRegion
{
    private readonly byte[] _data;

    /// <summary>
    /// Creates a DataRegion from a byte array.
    /// </summary>
    /// <param name="data">The complete data region bytes</param>
    public DataRegion(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    /// <summary>
    /// Total length of the data region in bytes.
    /// </summary>
    public long Length => _data.Length;

    /// <summary>
    /// Reads a slice from the data region.
    /// </summary>
    /// <param name="offset">Offset from the start of the data region</param>
    /// <param name="size">Size of the slice in bytes</param>
    /// <returns>ReadOnlyMemory containing the requested slice</returns>
    /// <exception cref="Exceptions.BoundsException">Thrown if offset or size are invalid</exception>
    public ReadOnlyMemory<byte> ReadSlice(long offset, long size)
    {
        if (offset < 0)
        {
            throw new Exceptions.BoundsException($"Offset cannot be negative: {offset}");
        }

        if (size < 0)
        {
            throw new Exceptions.BoundsException($"Size cannot be negative: {size}");
        }

        // Use overflow-safe bounds check: offset + size can overflow
        if (offset > Length || size > Length - offset)
        {
            throw new Exceptions.BoundsException(
                $"Slice [{offset}, {offset + size}) exceeds data region bounds [0, {Length})");
        }

        // Convert to int for array indexing (safe because we validated against Length which is int-backed)
        int intOffset = (int)offset;
        int intSize = (int)size;

        return new ReadOnlyMemory<byte>(_data, intOffset, intSize);
    }
}
