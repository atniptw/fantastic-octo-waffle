using System.IO.Compression;
using System.Runtime.CompilerServices;
using BlazorApp.Models;
using Microsoft.Extensions.Logging;
using UnityAssetParser.Bundle;
using UnityAssetParser.Services;

namespace BlazorApp.Services;

/// <summary>
/// Indexes ZIP archives to identify Unity asset files and their renderability.
/// </summary>
public class ZipIndexer : IZipIndexer
{
    private readonly ILogger<ZipIndexer>? _logger;

    /// <summary>
    /// Number of bytes to read from file header for type detection and shallow parsing.
    /// </summary>
    private const int HeaderBufferSize = 256;

    /// <summary>
    /// Maximum file size to process for renderable detection (2 MB).
    /// Larger files are skipped to avoid memory issues in WebAssembly.
    /// </summary>
    private const int MaxRenderableDetectionSize = 2 * 1024 * 1024;

    public ZipIndexer(ILogger<ZipIndexer>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Minimum size in bytes for SerializedFile format detection.
    /// SerializedFiles have a complex header structure requiring at least 20 bytes.
    /// </summary>
    private const int MinSerializedFileSize = 20;

    // Unity format magic bytes
    private static readonly byte[] UnityFSMagic = "UnityFS"u8.ToArray();
    private static readonly byte[] UnityWebMagic = "UnityWeb"u8.ToArray();
    private static readonly byte[] UnityRawMagic = "UnityRaw"u8.ToArray();
    private static readonly byte[] UnityArchiveMagic = "UnityArchive"u8.ToArray();

    /// <inheritdoc/>
    public async IAsyncEnumerable<FileIndexItem> IndexAsync(
        IAsyncEnumerable<byte[]> zipStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ct.ThrowIfCancellationRequested();

        // Collect all chunks into a single buffer
        var chunks = new List<byte[]>();
        await foreach (var chunk in zipStream.WithCancellation(ct))
        {
            chunks.Add(chunk);
        }

        if (chunks.Count == 0)
        {
            yield break;
        }

        // Combine chunks into a single buffer
        var totalSize = chunks.Sum(c => c.Length);
        var buffer = new byte[totalSize];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Array.Copy(chunk, 0, buffer, offset, chunk.Length);
            offset += chunk.Length;
        }

        // Parse ZIP using standard library
        using var ms = new MemoryStream(buffer);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        _logger?.LogInformation("Starting ZIP indexing, found {EntryCount} entries", archive.Entries.Count);

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var fileType = FileType.Unknown;
            var renderable = false;

            // Skip directories (identified by trailing slash or empty name)
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
            {
                _logger?.LogDebug("Skipping directory entry: {FullName}", entry.FullName);
                continue;
            }

            // Read first bytes to detect file type
            using var entryStream = entry.Open();
            // Limit header size to avoid overflow and memory issues with large files
            var headerSize = (int)Math.Min(HeaderBufferSize, Math.Min(entry.Length, int.MaxValue));
            var header = new byte[headerSize];
            await ReadExactlyAsync(entryStream, header, headerSize, ct);

            // Always detect file type; DetectFileType handles short headers internally.
            fileType = DetectFileType(entry.FullName, header);
            _logger?.LogDebug("Detected file type for {FileName}: {FileType}, Size: {Size} bytes", 
                entry.FullName, fileType, entry.Length);

            // For UnityFS and SerializedFile types, check if renderable (contains Mesh objects).
            // This requires reading the full file and parsing it.
            if ((fileType == FileType.UnityFS || fileType == FileType.SerializedFile) &&
                entry.Length > 0 &&
                entry.Length <= MaxRenderableDetectionSize)
            {
                _logger?.LogInformation("Checking renderability for {FileName} (Type: {FileType}, Size: {Size} bytes)",
                    entry.FullName, fileType, entry.Length);
                try
                {
                    // Read entire file for renderable detection from a fresh stream
                    var fileLength = checked((int)entry.Length);
                    using var fullStream = entry.Open();

                    var fileBytes = new byte[fileLength];
                    var totalRead = await ReadExactlyAsync(fullStream, fileBytes, fileLength, ct);

                    if (totalRead == fileLength)
                    {
                        renderable = DetectRenderableFromAsset(fileBytes, fileType);
                        _logger?.LogInformation("Renderability check for {FileName}: {Renderable}",
                            entry.FullName, renderable);
                    }
                    else
                    {
                        _logger?.LogWarning("Could not read complete file {FileName}: expected {Expected} bytes, got {Actual}",
                            entry.FullName, fileLength, totalRead);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail: renderable detection is best-effort
                    _logger?.LogWarning(ex,
                        "Failed to detect renderability for {FileName}: {Message}",
                        entry.FullName, ex.Message);
                    renderable = false; // Conservative default
                }
            }
            else if (fileType == FileType.UnityFS || fileType == FileType.SerializedFile)
            {
                if (entry.Length > MaxRenderableDetectionSize)
                {
                    _logger?.LogWarning("Skipping renderability check for {FileName}: file too large ({Size} bytes > {Max} bytes)",
                        entry.FullName, entry.Length, MaxRenderableDetectionSize);
                }
                else if (entry.Length == 0)
                {
                    _logger?.LogWarning("Skipping renderability check for {FileName}: empty file",
                        entry.FullName);
                }
            }

            yield return new FileIndexItem(
                entry.FullName,
                entry.Length,
                fileType,
                renderable
            );
        }
    }

    /// <summary>
    /// Detects file type based on magic bytes and file extension.
    /// </summary>
    private static FileType DetectFileType(string fileName, byte[] header)
    {
        // Check for UnityFS magic bytes
        if (header.Length >= 7 &&
            (StartsWithMagic(header, UnityFSMagic) ||
             StartsWithMagic(header, UnityWebMagic) ||
             StartsWithMagic(header, UnityRawMagic) ||
             StartsWithMagic(header, UnityArchiveMagic)))
        {
            return FileType.UnityFS;
        }

        // Check file extension for .resS resource files
        if (fileName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
        {
            return FileType.Resource;
        }

        // Check for SerializedFile format.
        // Full detection would require parsing the SerializedFile header structure,
        // but many Unity asset metadata files use the .assets extension.
        if (header.Length >= MinSerializedFileSize &&
            fileName.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
        {
            return FileType.SerializedFile;
        }

        return FileType.Unknown;
    }

    /// <summary>
    /// Checks if file starts with the expected magic bytes.
    /// </summary>
    private static bool StartsWithMagic(byte[] data, byte[] magic)
    {
        if (data.Length < magic.Length)
        {
            return false;
        }

        for (int i = 0; i < magic.Length; i++)
        {
            if (data[i] != magic[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Detects if an asset file contains renderable geometry (Mesh objects).
    /// </summary>
    /// <param name="assetBytes">Full asset file bytes.</param>
    /// <param name="fileType">Type of the asset file.</param>
    /// <returns>true if asset contains Mesh objects; false otherwise.</returns>
    private bool DetectRenderableFromAsset(byte[] assetBytes, FileType fileType)
    {
        try
        {
            if (fileType == FileType.UnityFS)
            {
                // For UnityFS bundles, we can't safely parse the full bundle in WebAssembly
                // due to memory constraints. The DataRegionBuilder allocates a buffer for
                // the entire uncompressed data, which can easily exceed 2-4GB WASM limits.
                // Instead, assume all UnityFS bundles are potentially renderable.
                _logger?.LogInformation("Marking UnityFS bundle as potentially renderable (skipping full parse to avoid OOM)");
                return true; // Conservative: assume renderable
            }
            else if (fileType == FileType.SerializedFile)
            {
                // Direct SerializedFile - pass to detector
                _logger?.LogDebug("Checking SerializedFile directly for Mesh objects");
                var hasRenderable = RenderableDetector.DetectRenderable(assetBytes);
                _logger?.LogInformation("RenderableDetector result for SerializedFile: {HasRenderable}", hasRenderable);
                return hasRenderable;
            }

            _logger?.LogDebug("File type {FileType} is not supported for renderability detection", fileType);
            return false;
        }
        catch (OutOfMemoryException ex)
        {
            _logger?.LogWarning("Out of memory during renderable detection: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Exception during renderable detection");
            return false;
        }
    }

    /// <summary>
    /// Reads exactly the requested number of bytes from the stream, looping until
    /// the buffer is filled or EOF is reached.
    /// </summary>
    private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0)
            {
                break; // EOF reached
            }
            totalRead += read;
        }
        return totalRead;
    }
}
