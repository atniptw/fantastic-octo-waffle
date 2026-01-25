using System.Text.Json;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Represents a fully parsed UnityFS bundle file.
/// Provides unified API for header, blocks info, data region, and node access.
/// </summary>
public sealed class BundleFile
{
    private readonly NodeExtractor _nodeExtractor;
    private readonly StreamingInfoResolver _streamingInfoResolver;

    /// <summary>
    /// Parsed header metadata.
    /// </summary>
    public UnityFSHeader Header { get; }

    /// <summary>
    /// Parsed BlocksInfo containing storage blocks and node directory.
    /// </summary>
    public BlocksInfo BlocksInfo { get; }

    /// <summary>
    /// Reconstructed data region from storage blocks.
    /// </summary>
    public DataRegion DataRegion { get; }

    /// <summary>
    /// Absolute file offset where data region begins.
    /// </summary>
    public long DataOffset { get; }

    /// <summary>
    /// List of nodes (virtual files) in the bundle.
    /// </summary>
    public IReadOnlyList<NodeInfo> Nodes => BlocksInfo.Nodes;

    private BundleFile(
        UnityFSHeader header,
        BlocksInfo blocksInfo,
        DataRegion dataRegion,
        long dataOffset)
    {
        Header = header;
        BlocksInfo = blocksInfo;
        DataRegion = dataRegion;
        DataOffset = dataOffset;
        _nodeExtractor = new NodeExtractor(detectOverlaps: true);
        _streamingInfoResolver = new StreamingInfoResolver();
    }

    /// <summary>
    /// Parses a UnityFS bundle from a stream using fail-fast semantics.
    /// </summary>
    /// <param name="stream">Stream positioned at bundle start</param>
    /// <returns>Parsed BundleFile</returns>
    /// <exception cref="InvalidBundleSignatureException">Invalid signature</exception>
    /// <exception cref="UnsupportedVersionException">Unsupported version</exception>
    /// <exception cref="HeaderParseException">Malformed header</exception>
    /// <exception cref="HashMismatchException">BlocksInfo hash verification failed</exception>
    /// <exception cref="DuplicateNodeException">Duplicate node paths</exception>
    /// <exception cref="BoundsException">Invalid node bounds</exception>
    /// <exception cref="NodeOverlapException">Overlapping nodes</exception>
    /// <exception cref="UnsupportedCompressionException">Unsupported compression</exception>
    public static BundleFile Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        var currentState = ParsingState.Start;

        try
        {
            // Step 1: Parse Header
            var headerParser = new UnityFSHeaderParser();
            var header = headerParser.Parse(stream);
            currentState = ParsingState.HeaderValid;

            // Step 2: Determine BlocksInfo location
            var blocksInfoLocation = headerParser.CalculateBlocksInfoLocation(header, stream.Length);
            long dataOffset = blocksInfoLocation.DataOffset;

            // Step 3: Read and decompress BlocksInfo
            stream.Position = blocksInfoLocation.BlocksInfoPosition;
            byte[] compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
            int bytesRead = stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
            if (bytesRead != compressedBlocksInfo.Length)
            {
                throw new BlocksInfoParseException(
                    $"Failed to read BlocksInfo: expected {compressedBlocksInfo.Length} bytes, got {bytesRead}");
            }
            currentState = ParsingState.BlocksInfoDecompressed;

            // Step 4: Parse and verify BlocksInfo
            var decompressor = new Decompressor();
            var blocksInfoParser = new BlocksInfoParser(decompressor);
            var blocksInfo = blocksInfoParser.Parse(
                compressedBlocksInfo,
                (int)header.UncompressedBlocksInfoSize,
                header.CompressionType,
                dataOffset);
            currentState = ParsingState.HashVerified;

            // Step 5: Validate nodes (bounds, uniqueness, overlaps)
            ValidateNodes(blocksInfo);
            currentState = ParsingState.NodesValidated;

            // Step 6: Reconstruct data region
            stream.Position = dataOffset;
            var dataRegionBuilder = new DataRegionBuilder(decompressor);
            var dataRegion = dataRegionBuilder.Build(stream, dataOffset, blocksInfo.Blocks);

            // Success
            currentState = ParsingState.Success;
            return new BundleFile(header, blocksInfo, dataRegion, dataOffset);
        }
        catch (Exception ex) when (ex is not BundleException)
        {
            // Wrap unexpected exceptions
            throw new BundleException(
                $"Bundle parsing failed in state {currentState}: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Attempts to parse a UnityFS bundle, collecting all errors.
    /// </summary>
    /// <param name="stream">Stream positioned at bundle start</param>
    /// <returns>ParseResult with bundle or error list</returns>
    public static ParseResult TryParse(Stream stream)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var bundle = Parse(stream);
            return new ParseResult
            {
                Bundle = bundle,
                Warnings = warnings,
                Errors = errors
            };
        }
        catch (InvalidBundleSignatureException ex)
        {
            errors.Add($"Invalid signature: {ex.Message}");
        }
        catch (UnsupportedVersionException ex)
        {
            errors.Add($"Unsupported version: {ex.Message}");
        }
        catch (HeaderParseException ex)
        {
            errors.Add($"Header parse error: {ex.Message}");
        }
        catch (HashMismatchException ex)
        {
            var expectedHash = ex.ExpectedHash ?? Array.Empty<byte>();
            var computedHash = ex.ComputedHash ?? Array.Empty<byte>();
            errors.Add(
                $"Hash mismatch: expected {BitConverter.ToString(expectedHash)}, " +
                $"got {BitConverter.ToString(computedHash)}");
        }
        catch (DuplicateNodeException ex)
        {
            errors.Add($"Duplicate node path: {ex.Message}");
        }
        catch (BoundsException ex)
        {
            errors.Add($"Node bounds error: {ex.Message}");
        }
        catch (NodeOverlapException ex)
        {
            errors.Add($"Node overlap: {ex.Message}");
        }
        catch (UnsupportedCompressionException ex)
        {
            errors.Add($"Unsupported compression: {ex.Message}");
        }
        catch (BlocksInfoParseException ex)
        {
            errors.Add($"BlocksInfo parse error: {ex.Message}");
        }
        catch (BundleException ex)
        {
            errors.Add($"Bundle error: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Unexpected error: {ex.GetType().Name}: {ex.Message}");
        }

        return new ParseResult
        {
            Bundle = null,
            Warnings = warnings,
            Errors = errors
        };
    }

    /// <summary>
    /// Gets a node by exact path match (case-sensitive).
    /// </summary>
    /// <param name="path">Node path to search for</param>
    /// <returns>NodeInfo if found, null otherwise</returns>
    public NodeInfo? GetNode(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Nodes.FirstOrDefault(n => n.Path == path);
    }

    /// <summary>
    /// Extracts a node's payload data.
    /// </summary>
    /// <param name="node">Node to extract</param>
    /// <returns>ReadOnlyMemory containing node data</returns>
    /// <exception cref="BoundsException">If node bounds are invalid</exception>
    public ReadOnlyMemory<byte> ExtractNode(NodeInfo node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _nodeExtractor.ReadNode(DataRegion, node);
    }

    /// <summary>
    /// Resolves a StreamingInfo reference to byte data.
    /// </summary>
    /// <param name="info">StreamingInfo reference</param>
    /// <returns>ReadOnlyMemory containing referenced data</returns>
    /// <exception cref="StreamingInfoException">If path not found or bounds invalid</exception>
    public ReadOnlyMemory<byte> ResolveStreamingInfo(StreamingInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _streamingInfoResolver.Resolve(Nodes, DataRegion, info);
    }

    /// <summary>
    /// Gets the metadata node (conventionally Node 0 in Unity bundles).
    /// </summary>
    /// <returns>First node if present, null otherwise</returns>
    public NodeInfo? GetMetadataNode()
    {
        return Nodes.Count > 0 ? Nodes[0] : null;
    }

    /// <summary>
    /// Converts this bundle to metadata for JSON serialization.
    /// </summary>
    /// <returns>BundleMetadata for validation</returns>
    public BundleMetadata ToMetadata()
    {
        return new BundleMetadata
        {
            Header = new HeaderMetadata
            {
                Signature = Header.Signature,
                Version = Header.Version,
                UnityVersion = Header.UnityVersion,
                UnityRevision = Header.UnityRevision,
                Size = Header.Size,
                CompressedBlocksInfoSize = Header.CompressedBlocksInfoSize,
                UncompressedBlocksInfoSize = Header.UncompressedBlocksInfoSize,
                Flags = Header.Flags
            },
            StorageBlocks = BlocksInfo.Blocks.Select(b => new StorageBlockMetadata
            {
                UncompressedSize = b.UncompressedSize,
                CompressedSize = b.CompressedSize,
                Flags = b.Flags
            }).ToList(),
            Nodes = Nodes.Select(n => new NodeMetadata
            {
                Offset = n.Offset,
                Size = n.Size,
                Flags = n.Flags,
                Path = n.Path
            }).ToList(),
            DataOffset = DataOffset
        };
    }

    /// <summary>
    /// Serializes this bundle to JSON for validation.
    /// </summary>
    /// <returns>JSON string</returns>
    public string ToJson()
    {
        var metadata = ToMetadata();
        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Validates nodes against spec requirements (§15.13).
    /// </summary>
    private static void ValidateNodes(BlocksInfo blocksInfo)
    {
        var nodes = blocksInfo.Nodes;
        var totalUncompressedSpan = blocksInfo.TotalUncompressedDataSize;

        // Check for empty node list (valid but unusual)
        if (nodes.Count == 0)
        {
            return;
        }

        // Validate each node's bounds
        foreach (var node in nodes)
        {
            if (node.Offset < 0)
            {
                throw new BoundsException($"Node '{node.Path}' has negative offset: {node.Offset}");
            }

            if (node.Size < 0)
            {
                throw new BoundsException($"Node '{node.Path}' has negative size: {node.Size}");
            }

            // Check for overflow
            if (node.Offset > long.MaxValue - node.Size)
            {
                throw new BoundsException(
                    $"Node '{node.Path}' offset + size would overflow: offset={node.Offset}, size={node.Size}");
            }

            long nodeEnd = node.Offset + node.Size;
            if (nodeEnd > totalUncompressedSpan)
            {
                throw new BoundsException(
                    $"Node '{node.Path}' bounds [{node.Offset}, {nodeEnd}) exceed total uncompressed span {totalUncompressedSpan}");
            }
        }

        // Check for duplicate paths
        var pathSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Where(n => !string.IsNullOrEmpty(n.Path)))
        {
            if (!pathSet.Add(node.Path))
            {
                throw new DuplicateNodeException($"Duplicate node path: '{node.Path}'");
            }
        }

        // Check for overlaps using validation logic (no instance state needed)
        ValidateNoNodeOverlaps(nodes);
    }

    /// <summary>
    /// Validates that nodes don't overlap.
    /// </summary>
    private static void ValidateNoNodeOverlaps(IReadOnlyList<NodeInfo> nodes)
    {
        if (nodes.Count <= 1)
        {
            return;
        }

        // Sort nodes by offset for overlap detection
        var sortedNodes = nodes.OrderBy(n => n.Offset).ToList();

        for (int i = 0; i < sortedNodes.Count - 1; i++)
        {
            var current = sortedNodes[i];
            var next = sortedNodes[i + 1];

            // Use overflow-safe end calculation
            if (current.Size > 0 && current.Offset > long.MaxValue - current.Size)
            {
                throw new NodeOverlapException(
                    $"Node '{current.Path}' offset + size would overflow: offset={current.Offset}, size={current.Size}");
            }

            long currentEnd = current.Offset + current.Size;
            
            if (currentEnd > next.Offset)
            {
                throw new NodeOverlapException(
                    $"Nodes '{current.Path}' [{current.Offset}, {currentEnd}) and '{next.Path}' [{next.Offset}, {next.Offset + next.Size}) overlap");
            }
        }
    }
}
