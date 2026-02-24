using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AssetStudio;
using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public sealed class ArchiveScanner : IArchiveScanner
{
    private static readonly HashSet<string> UnityPackageExtensions = new(StringComparer.Ordinal)
    {
        ".asset",
        ".prefab",
        ".hhh"
    };

    public ScanModArchiveResult Scan(IReadOnlyList<ArchiveEntryDescriptor> entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return ScanModArchiveResult.Succeeded([]);
        }

        var bundles = entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => new DiscoveredBundle(
                entry.FullPath,
                entry.FileName,
                entry.SizeBytes,
                Path.GetExtension(entry.FileName).ToLowerInvariant()))
            .Where(bundle => bundle.Extension == ".hhh")
            .ToArray();

        return ScanModArchiveResult.Succeeded(bundles);
    }

    public ScanModArchiveResult ScanUnityPackage(string unityPackagePath)
    {
        if (string.IsNullOrWhiteSpace(unityPackagePath))
        {
            return ScanModArchiveResult.Failed("Unitypackage path is required.");
        }

        if (!File.Exists(unityPackagePath))
        {
            return ScanModArchiveResult.Failed($"Unitypackage not found: {unityPackagePath}");
        }

        try
        {
            var itemsByGuid = new Dictionary<string, UnityPackageItem>(StringComparer.Ordinal);

            using var packageStream = File.OpenRead(unityPackagePath);
            using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress, leaveOpen: false);
            using var tarReader = new TarReader(gzipStream, leaveOpen: false);

            TarEntry? entry;
            while ((entry = tarReader.GetNextEntry()) is not null)
            {
                if (entry.EntryType is TarEntryType.Directory || entry.DataStream is null || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                var separatorIndex = entry.Name.IndexOf('/');
                if (separatorIndex <= 0 || separatorIndex >= entry.Name.Length - 1)
                {
                    continue;
                }

                var guid = entry.Name[..separatorIndex];
                var memberName = entry.Name[(separatorIndex + 1)..];

                if (!itemsByGuid.TryGetValue(guid, out var item))
                {
                    item = new UnityPackageItem();
                    itemsByGuid[guid] = item;
                }

                switch (memberName)
                {
                    case "pathname":
                        item.Pathname = ReadUtf8(entry.DataStream);
                        break;
                    case "asset":
                        item.AssetBytes = ReadAllBytes(entry.DataStream);
                        break;
                }
            }

            var warnings = new List<string>();
            var bundles = new List<DiscoveredBundle>();

            foreach (var item in itemsByGuid.Values)
            {
                if (string.IsNullOrWhiteSpace(item.Pathname) || item.AssetBytes is null || item.AssetBytes.Length == 0)
                {
                    continue;
                }

                try
                {
                    var fileName = Path.GetFileName(item.Pathname);
                    var extension = Path.GetExtension(fileName).ToLowerInvariant();
                    if (!UnityPackageExtensions.Contains(extension))
                    {
                        continue;
                    }

                    using var assetStream = new MemoryStream(item.AssetBytes, writable: false);
                    using var fileReader = new FileReader(item.Pathname, assetStream);

                    if (fileReader.FileType is FileType.GZipFile or FileType.BrotliFile or FileType.ZipFile)
                    {
                        warnings.Add($"Skipping compressed nested asset '{item.Pathname}' detected as {fileReader.FileType}.");
                        continue;
                    }

                    bundles.Add(new DiscoveredBundle(item.Pathname, fileName, item.AssetBytes.LongLength, extension));
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to probe unitypackage asset '{item.Pathname}': {ex.Message}");
                }
            }

            return ScanModArchiveResult.Succeeded(bundles, warnings);
        }
        catch (Exception ex)
        {
            return ScanModArchiveResult.Failed($"Failed to scan unitypackage: {ex.Message}");
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string ReadUtf8(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd().Trim();
    }

    private sealed class UnityPackageItem
    {
        public string? Pathname { get; set; }
        public byte[]? AssetBytes { get; set; }
    }
}
