using System.Text;
using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Parses UnityFS bundle headers and calculates BlocksInfo locations.
/// Implements the parsing algorithm specified in UnityFS-BundleSpec.md § 13.4 and § 13.5.
/// </summary>
public class UnityFSHeaderParser : IUnityFSHeaderParser
{
    private const uint CompressionTypeMask = 0x3F;      // Bits 0-5
    private const uint BlocksInfoAtEndFlag = 0x80;      // Bit 7
    private const uint NeedsPaddingFlag = 0x200;        // Bit 9
    // Reserved bits: all bits except 0-5 (compression), 7 (blocks info at end), and 9 (padding)
    // If future Unity versions define new flags, update this mask accordingly
    private const uint ReservedBitsMask = ~(CompressionTypeMask | BlocksInfoAtEndFlag | NeedsPaddingFlag);

    /// <summary>
    /// Parses the UnityFS header from the beginning of a stream.
    /// </summary>
    /// <param name="stream">Binary stream positioned at start of bundle.</param>
    /// <returns>Parsed header with computed properties.</returns>
    /// <exception cref="InvalidBundleSignatureException">Signature is not "UnityFS".</exception>
    /// <exception cref="UnsupportedVersionException">Version not in {6, 7}.</exception>
    /// <exception cref="HeaderParseException">Malformed header data.</exception>
    public UnityFSHeader Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable", nameof(stream));
        }

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // 1. Read and validate signature
            var signature = reader.ReadUtf8NullTerminated();
            if (signature != "UnityFS")
            {
                throw new InvalidBundleSignatureException(
                    $"Expected 'UnityFS', got '{signature}'",
                    signature);
            }

            // 2. Read version (uint32, little-endian)
            var version = reader.ReadUInt32();
            if (version != 6 && version != 7)
            {
                throw new UnsupportedVersionException(
                    $"Version {version} not supported (only 6 and 7)",
                    version);
            }

            // 3. Read Unity version strings
            var unityVersion = reader.ReadUtf8NullTerminated();
            var unityRevision = reader.ReadUtf8NullTerminated();

            // 4. Read size and BlocksInfo metadata
            var size = reader.ReadInt64();
            var compressedBlocksInfoSize = reader.ReadUInt32();
            var uncompressedBlocksInfoSize = reader.ReadUInt32();
            var flags = reader.ReadUInt32();

            // 5. Record position after flags
            var headerEndPosition = stream.Position;

            // 6. Validate flags
            ValidateFlags(flags, version);

            return new UnityFSHeader
            {
                Signature = signature,
                Version = version,
                UnityVersion = unityVersion,
                UnityRevision = unityRevision,
                Size = size,
                CompressedBlocksInfoSize = compressedBlocksInfoSize,
                UncompressedBlocksInfoSize = uncompressedBlocksInfoSize,
                Flags = flags,
                HeaderEndPosition = headerEndPosition
            };
        }
        catch (InvalidBundleSignatureException)
        {
            throw; // Re-throw validation exceptions as-is
        }
        catch (UnsupportedVersionException)
        {
            throw; // Re-throw validation exceptions as-is
        }
        catch (HeaderParseException)
        {
            throw; // Re-throw our own parsing exceptions as-is
        }
        catch (Exception ex)
        {
            // Wrap any unexpected exceptions (I/O errors, encoding issues, etc.)
            throw new HeaderParseException("Failed to parse UnityFS header", ex);
        }
    }

    /// <summary>
    /// Calculates BlocksInfo position and data offset based on header.
    /// </summary>
    /// <param name="header">Parsed header.</param>
    /// <param name="fileLength">Total file size for streamed layout.</param>
    /// <returns>Location information for BlocksInfo and data region.</returns>
    public BlocksInfoLocation CalculateBlocksInfoLocation(UnityFSHeader header, long fileLength)
    {
        ArgumentNullException.ThrowIfNull(header);

        if (fileLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileLength), "File length cannot be negative");
        }

        // Apply pre-BlocksInfo alignment
        var alignedPosition = BinaryReaderExtensions.CalculateAlignedPosition(
            header.HeaderEndPosition,
            header.AlignmentSize);
        var alignmentPadding = (int)(alignedPosition - header.HeaderEndPosition);

        if (header.BlocksInfoAtEnd)
        {
            // Streamed layout: BlocksInfo at EOF, data starts after aligned header
            return new BlocksInfoLocation
            {
                BlocksInfoPosition = fileLength - header.CompressedBlocksInfoSize,
                DataOffset = alignedPosition,
                AlignmentPadding = alignmentPadding
            };
        }
        else
        {
            // Embedded layout: BlocksInfo after aligned header
            return new BlocksInfoLocation
            {
                BlocksInfoPosition = alignedPosition,
                DataOffset = alignedPosition + header.CompressedBlocksInfoSize,
                AlignmentPadding = alignmentPadding
            };
        }
    }

    /// <summary>
    /// Validates flag bits for correctness.
    /// </summary>
    /// <param name="flags">The flags value to validate.</param>
    /// <param name="version">The bundle version.</param>
    /// <exception cref="HeaderParseException">Thrown if flags are invalid.</exception>
    private static void ValidateFlags(uint flags, uint version)
    {
        // Validate compression type
        var compressionType = flags & CompressionTypeMask;
        if (compressionType > 4)
        {
            throw new HeaderParseException($"Invalid compression type: {compressionType}");
        }

        // Check reserved bits (should be zero)
        if ((flags & ReservedBitsMask) != 0)
        {
            throw new HeaderParseException($"Reserved flag bits are set: 0x{flags:X8}");
        }

        // Padding flag only valid for v7+
        if (version < 7 && (flags & NeedsPaddingFlag) != 0)
        {
            throw new HeaderParseException(
                $"Padding flag (bit 9) set for version {version} (only valid for v7+)");
        }
    }
}
