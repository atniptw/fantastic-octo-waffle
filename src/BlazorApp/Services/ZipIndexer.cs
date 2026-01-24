using System.IO.Compression;
using System.Runtime.CompilerServices;
using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Indexes ZIP archives to identify Unity asset files and their renderability.
/// </summary>
public class ZipIndexer : IZipIndexer
{
    /// <summary>
    /// Number of bytes to read from file header for type detection and shallow parsing.
    /// </summary>
    private const int HeaderBufferSize = 256;

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

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var fileType = FileType.Unknown;
            var renderable = false;

            // Skip directories (identified by trailing slash or empty name)
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            // Read first bytes to detect file type
            using var entryStream = entry.Open();
            // Limit header size to avoid overflow and memory issues with large files
            var headerSize = (int)Math.Min(HeaderBufferSize, Math.Min(entry.Length, int.MaxValue));
            var header = new byte[headerSize];
            var bytesRead = await ReadExactlyAsync(entryStream, header, headerSize, ct);

            // Always detect file type; DetectFileType handles short headers internally.
            fileType = DetectFileType(entry.FullName, header);

            // For UnityFS files, check if renderable (contains Mesh objects).
            // Require at least the UnityFS magic length to safely inspect the header.
            if (fileType == FileType.UnityFS && bytesRead >= UnityFSMagic.Length)
            {
                // Pass header buffer instead of stream (which doesn't support seeking)
                renderable = IsRenderable(header, bytesRead);
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
    /// Performs shallow check to determine if UnityFS bundle might contain Mesh objects.
    /// This is a heuristic check based on header data.
    /// </summary>
    private static bool IsRenderable(byte[] header, int length)
    {
        try
        {
            if (length < 7)
            {
                return false;
            }

            // Check for any Unity bundle signature variant
            if (!StartsWithMagic(header, UnityFSMagic) &&
                !StartsWithMagic(header, UnityWebMagic) &&
                !StartsWithMagic(header, UnityRawMagic) &&
                !StartsWithMagic(header, UnityArchiveMagic))
            {
                return false;
            }

            // For a full implementation, we would:
            // 1. Parse the block storage info
            // 2. Decompress the data blocks
            // 3. Parse the SerializedFile structure
            // 4. Read the object type table
            // 5. Look for ClassID 43 (Mesh)
            //
            // However, this is a shallow check with limited header data.
            // As a heuristic: UnityFS bundles in .hhh files typically contain meshes
            // A proper implementation would require full bundle parsing
            
            // Conservative: assume UnityFS bundles may contain meshes
            return true;
        }
        catch
        {
            // If we can't parse it, it's not renderable
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
