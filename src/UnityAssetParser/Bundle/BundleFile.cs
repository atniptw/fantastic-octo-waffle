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
            // Step 1: Parse Header (UnityPy: BundleFile.py line 27-50)
            var headerParser = new UnityFSHeaderParser();
            var header = headerParser.Parse(stream);
            currentState = ParsingState.HeaderValid;

            // Step 2: Apply alignment BEFORE reading BlocksInfo (UnityPy: BundleFile.py lines 118-130)
            // CRITICAL: Alignment must happen BEFORE reading the compressed BlocksInfo bytes
            long startPos = stream.Position;
            
            if (header.Version >= 7)
            {
                // Align to 16 bytes unconditionally for v7+
                long currentPos = stream.Position;
                long remainder = currentPos % 16;
                if (remainder != 0)
                {
                    stream.Position = currentPos + (16 - remainder);
                }
            }
            
            byte[] compressedBlocksInfo;

            // Step 3: Read BlocksInfo from correct location (UnityPy: BundleFile.py lines 130-141)
            if (header.BlocksInfoAtEnd)  // BlocksInfoAtTheEnd = 0x80
            {
                // Seek to: file_length - compressed_size
                stream.Position = stream.Length - header.CompressedBlocksInfoSize;
                compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
                int bytesRead = stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
                if (bytesRead != compressedBlocksInfo.Length)
                {
                    throw new BlocksInfoParseException(
                        $"Failed to read BlocksInfo from end: expected {compressedBlocksInfo.Length} bytes, got {bytesRead}");
                }
                // Seek back to aligned position for data region
                stream.Position = startPos;
                if (header.Version >= 7)
                {
                    long currentPos = stream.Position;
                    long remainder = currentPos % 16;
                    if (remainder != 0)
                    {
                        stream.Position = currentPos + (16 - remainder);
                    }
                }
            }
            else  // BlocksAndDirectoryInfoCombined = 0x40
            {
                // Read from current position (already aligned)
                compressedBlocksInfo = new byte[header.CompressedBlocksInfoSize];
                int bytesRead = stream.Read(compressedBlocksInfo, 0, compressedBlocksInfo.Length);
                if (bytesRead != compressedBlocksInfo.Length)
                {
                    throw new BlocksInfoParseException(
                        $"Failed to read BlocksInfo: expected {compressedBlocksInfo.Length} bytes, got {bytesRead}");
                }
            }
            currentState = ParsingState.BlocksInfoDecompressed;

            long dataOffset = stream.Position;

            // Step 4: Parse BlocksInfo
            var decompressor = new Decompressor();
            var blocksInfoParser = new BlocksInfoParser(decompressor);
            var blocksInfo = blocksInfoParser.Parse(
                compressedBlocksInfo,
                (int)header.UncompressedBlocksInfoSize,
                header.CompressionType,
                dataOffset);
            currentState = ParsingState.HashVerified;

            // Step 5: Reconstruct data region
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

}
