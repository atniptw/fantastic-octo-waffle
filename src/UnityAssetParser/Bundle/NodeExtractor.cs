using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Extracts node payloads from a DataRegion with bounds validation and optional overlap detection.
/// </summary>
public sealed class NodeExtractor
{
    private readonly bool _detectOverlaps;

    /// <summary>
    /// Creates a NodeExtractor.
    /// </summary>
    /// <param name="detectOverlaps">If true, validates that nodes don't overlap</param>
    public NodeExtractor(bool detectOverlaps = true)
    {
        _detectOverlaps = detectOverlaps;
    }

    /// <summary>
    /// Reads a single node's payload from the data region.
    /// </summary>
    /// <param name="region">The data region containing node data</param>
    /// <param name="node">Node metadata with offset and size</param>
    /// <returns>ReadOnlyMemory containing the node's payload</returns>
    /// <exception cref="BoundsException">Thrown if node bounds are invalid</exception>
    public ReadOnlyMemory<byte> ReadNode(DataRegion region, NodeInfo node)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(node);

        ValidateNodeBounds(region, node);

        return region.ReadSlice(node.Offset, node.Size);
    }

    /// <summary>
    /// Validates node bounds against the data region.
    /// </summary>
    private static void ValidateNodeBounds(DataRegion region, NodeInfo node)
    {
        if (node.Offset < 0)
        {
            throw new BoundsException($"Node '{node.Path}' has negative offset: {node.Offset}");
        }

        if (node.Size < 0)
        {
            throw new BoundsException($"Node '{node.Path}' has negative size: {node.Size}");
        }

        if (node.Offset + node.Size > region.Length)
        {
            throw new BoundsException(
                $"Node '{node.Path}' bounds [{node.Offset}, {node.Offset + node.Size}) exceed data region length {region.Length}");
        }
    }

    /// <summary>
    /// Validates that nodes in the list don't overlap.
    /// </summary>
    /// <param name="nodes">List of nodes to validate</param>
    /// <exception cref="NodeOverlapException">Thrown if any nodes overlap</exception>
    public void ValidateNoOverlaps(IReadOnlyList<NodeInfo> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (!_detectOverlaps || nodes.Count <= 1)
        {
            return;
        }

        // Sort nodes by offset for overlap detection
        var sortedNodes = nodes.OrderBy(n => n.Offset).ToList();

        for (int i = 0; i < sortedNodes.Count - 1; i++)
        {
            var current = sortedNodes[i];
            var next = sortedNodes[i + 1];

            long currentEnd = current.Offset + current.Size;
            
            if (currentEnd > next.Offset)
            {
                throw new NodeOverlapException(
                    $"Nodes '{current.Path}' [{current.Offset}, {currentEnd}) and '{next.Path}' [{next.Offset}, {next.Offset + next.Size}) overlap");
            }
        }
    }
}
