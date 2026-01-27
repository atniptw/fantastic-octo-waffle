using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Bundle;

/// <summary>
/// Parses UnityFS bundle headers and calculates BlocksInfo locations.
/// Implements the parsing algorithm specified in UnityFS-BundleSpec.md § 15.3 and § 15.4.
/// </summary>
public class UnityFSHeaderParser : IUnityFSHeaderParser
{
    private const uint CompressionTypeMask = 0x3F;                // Bits 0-5
    private const uint BlocksAndDirectoryInfoCombinedFlag = 0x40; // Bit 6
    private const uint BlocksInfoAtEndFlag = 0x80;                // Bit 7
    private const uint OldWebPluginCompatibility = 0x100;         // Bit 8
    private const uint NeedsPaddingFlag = 0x200;                  // Bit 9
    private const uint UsesAssetBundleEncryptionOld = 0x400;      // Bit 10 (old)
    private const uint UsesAssetBundleEncryptionNew = 0x1000;     // Bit 12 (new)
    // Reserved bits: allow known flags used by Unity / UnityPy
    private const uint ReservedBitsMask = ~(CompressionTypeMask 
                                            | BlocksAndDirectoryInfoCombinedFlag 
                                            | BlocksInfoAtEndFlag 
                                            | OldWebPluginCompatibility 
                                            | NeedsPaddingFlag 
                                            | UsesAssetBundleEncryptionOld 
                                            | UsesAssetBundleEncryptionNew);

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
            // Per UnityPy BundleFile.py and UnityFS-BundleSpec: UnityFS header uses BIG-ENDIAN
            using var reader = new EndianBinaryReader(stream, isBigEndian: true);

            // 1. Read and validate signature
            var signature = reader.ReadUtf8NullTerminated();
            if (signature != "UnityFS")
            {
                throw new InvalidBundleSignatureException(
                    $"Expected 'UnityFS', got '{signature}'",
                    signature);
            }

            // 2. Read version (uint32, big-endian)
            var version = reader.ReadUInt32();

            // 3. Read Unity version strings
            var unityVersion = reader.ReadUtf8NullTerminated();
            var unityRevision = reader.ReadUtf8NullTerminated();

            // 4. Read size and BlocksInfo metadata (big-endian)
            var size = reader.ReadInt64();
            var compressedBlocksInfoSize = reader.ReadUInt32();
            var uncompressedBlocksInfoSize = reader.ReadUInt32();
            var flags = reader.ReadUInt32();

            // 5. Record position after flags (before any post-header alignment)
            var headerEndPosition = stream.Position;

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

        // Apply pre-BlocksInfo alignment (UnityPy version-based alignment)
        var alignedPosition = BinaryReaderExtensions.CalculateAlignedPosition(
            header.HeaderEndPosition,
            header.AlignmentSize);
        var alignmentPadding = (int)(alignedPosition - header.HeaderEndPosition);

        var blocksInfoPosition = header.BlocksInfoAtEnd
            ? fileLength - header.CompressedBlocksInfoSize
            : alignedPosition;

        return new BlocksInfoLocation
        {
            BlocksInfoPosition = blocksInfoPosition,
            AlignedHeaderPosition = alignedPosition,
            AlignmentPadding = alignmentPadding
        };
    }

    /// <summary>
    /// Validates flag bits for correctness.
    /// </summary>
    /// <param name="flags">The flags value to validate.</param>
    /// <param name="version">The bundle version.</param>
    /// <exception cref="HeaderParseException">Thrown if flags are invalid.</exception>
    private static void ValidateFlags(uint flags, uint version)
    {
        // UnityPy does not enforce reserved/padding flag validation; no-op here to mirror behavior.
    }
}
