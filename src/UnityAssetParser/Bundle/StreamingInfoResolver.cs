using System.IO;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Resolves StreamingInfo references to byte slices within .resS nodes.
/// </summary>
public sealed class StreamingInfoResolver
{
    /// <summary>
    /// Resolves a StreamingInfo reference to a byte slice.
    /// </summary>
    /// <param name="nodes">List of nodes from the bundle</param>
    /// <param name="region">The data region containing node data</param>
    /// <param name="info">StreamingInfo reference to resolve</param>
    /// <returns>ReadOnlyMemory containing the referenced slice</returns>
    /// <exception cref="StreamingInfoException">Thrown if path not found or slice is out of bounds</exception>
    public ReadOnlyMemory<byte> Resolve(
        IReadOnlyList<NodeInfo> nodes,
        DataRegion region,
        StreamingInfo info)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(info);

        // UnityPy-style resolution: try basename and common variations
        string basename = Path.GetFileName(info.Path);
        if (string.IsNullOrEmpty(basename))
        {
            basename = info.Path;
        }

        string nameWithoutExt = Path.GetFileNameWithoutExtension(basename);
        var possibleNames = new[]
        {
            basename,
            $"{nameWithoutExt}.resource",
            $"{nameWithoutExt}.assets.resS",
            $"{nameWithoutExt}.resS"
        };

        // Find node by exact path match or by basename variations
        // UnityPy approach: check if node path ends with any of the possible names
        // Path matching is case-sensitive
        var node = nodes.FirstOrDefault(n =>
            n.Path == info.Path ||
            possibleNames.Any(name => n.Path.EndsWith(name, StringComparison.Ordinal)));

        if (node == null)
        {
            throw new StreamingInfoException(
                $"StreamingInfo path '{info.Path}' does not match any node in the bundle");
        }

        // Validate slice bounds within the node
        if (info.Offset < 0)
        {
            throw new StreamingInfoException(
                $"StreamingInfo offset cannot be negative: {info.Offset}");
        }

        if (info.Size < 0)
        {
            throw new StreamingInfoException(
                $"StreamingInfo size cannot be negative: {info.Size}");
        }

        // Validate slice bounds within the node without risking integer overflow
        if (info.Size > node.Size)
        {
            throw new StreamingInfoException(
                $"StreamingInfo size {info.Size} exceeds node '{node.Path}' size {node.Size}");
        }

        if (info.Offset > node.Size - info.Size)
        {
            throw new StreamingInfoException(
                $"StreamingInfo slice [{info.Offset}, {info.Offset + info.Size}) exceeds node '{node.Path}' size {node.Size}");
        }

        // Calculate absolute offset in data region, guarding against overflow
        if (node.Offset < 0)
        {
            throw new StreamingInfoException(
                $"Node offset cannot be negative for path '{node.Path}': {node.Offset}");
        }

        if (node.Offset > long.MaxValue - info.Offset)
        {
            throw new StreamingInfoException(
                $"StreamingInfo absolute offset would overflow: node offset {node.Offset} + info offset {info.Offset}");
        }

        long absoluteOffset = node.Offset + info.Offset;

        // Read slice from data region
        return region.ReadSlice(absoluteOffset, info.Size);
    }
}
