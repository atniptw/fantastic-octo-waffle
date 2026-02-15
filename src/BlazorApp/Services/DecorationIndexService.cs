using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorApp.Services;

public sealed class DecorationIndexService
{
    private const long MaxUploadBytes = 1024L * 1024L * 512L;
    private static readonly string[] TagPriority =
    [
        "head",
        "neck",
        "body",
        "hip",
        "leftarm",
        "rightarm",
        "leftleg",
        "rightleg",
        "world"
    ];

    private readonly AssetStoreService _assetStore;
    private readonly List<DecorationEntry> _entries = new();
    private Task? _loadTask;

    public DecorationIndexService(AssetStoreService assetStore)
    {
        _assetStore = assetStore;
    }

    public IReadOnlyList<DecorationEntry> Entries => _entries;
    public bool LastScanSucceeded { get; private set; }
    public string? LastScanMessage { get; private set; }
    public DateTimeOffset? LastScanAt { get; private set; }

    public Task LoadPersistedAsync(CancellationToken cancellationToken = default)
    {
        _loadTask ??= LoadPersistedInternalAsync(cancellationToken);
        return _loadTask;
    }

    public async Task ScanZipAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        LastScanSucceeded = false;
        LastScanMessage = null;
        LastScanAt = DateTimeOffset.UtcNow;

        if (!IsZipFile(file.Name))
        {
            LastScanSucceeded = true;
            LastScanMessage = "Scan skipped (not a .zip file).";
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(MaxUploadBytes, cancellationToken);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
            var modId = GetModId(file.Name);
            var storageWarnings = new List<string>();

            foreach (var entry in archive.Entries)
            {
                if (!IsHhhEntry(entry))
                {
                    continue;
                }

                var result = await ReadEntryAsync(entry, cancellationToken);
                _entries.Add(result.Entry);

                try
                {
                    await PersistEntryAsync(modId, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    storageWarnings.Add(ex.Message);
                }
            }

            LastScanSucceeded = true;
            LastScanMessage = storageWarnings.Count == 0
                ? "Scan complete."
                : $"Scan complete with {storageWarnings.Count} storage warning(s).";
        }
        catch (Exception ex)
        {
            LastScanSucceeded = false;
            LastScanMessage = $"Scan failed: {ex.Message}";
        }
    }

    private async Task LoadPersistedInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var assets = await _assetStore.GetAllMetadataAsync(cancellationToken);
            _entries.Clear();

            foreach (var asset in assets)
            {
                var bodyPart = InferBodyPart(asset.SourcePath, out var matchedToken);
                _entries.Add(new DecorationEntry(asset.SourcePath, asset.Size, asset.Id, bodyPart, matchedToken));
            }

            LastScanSucceeded = true;
            LastScanMessage = assets.Count > 0 ? "Loaded from library." : "Library is empty.";
            LastScanAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            LastScanSucceeded = false;
            LastScanMessage = $"Library load failed: {ex.Message}";
        }
    }

    private static bool IsZipFile(string fileName)
        => fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool IsHhhEntry(ZipArchiveEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return false;
        }

        return entry.FullName.EndsWith(".hhh", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ReadEntryResult> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        var bodyPart = InferBodyPart(entry.FullName, out var matchedToken);
        var bytes = await ReadEntryBytesAsync(entry, cancellationToken);
        var sha256 = ComputeSha256(bytes);
        var item = new DecorationEntry(entry.FullName, bytes.LongLength, sha256, bodyPart, matchedToken);

        return new ReadEntryResult(item, bytes);
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        await entryStream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static string InferBodyPart(string path, out string? matchedToken)
    {
        var tokens = Tokenize(path);

        foreach (var tag in TagPriority)
        {
            if (tokens.Contains(tag))
            {
                matchedToken = tag;
                return tag;
            }
        }

        matchedToken = null;
        return "unknown";
    }

    private static HashSet<string> Tokenize(string value)
    {
        var tokens = Regex.Split(value.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token));

        return new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetModId(string fileName)
    {
        var modId = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(modId) ? "unknown" : modId;
    }

    private async Task PersistEntryAsync(string modId, ReadEntryResult result, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var asset = new StoredAsset(
            result.Entry.Sha256,
            modId,
            DecorationNameFormatter.GetDisplayName(result.Entry.FilePath),
            result.Bytes,
            null,
            result.Entry.SizeBytes,
            now,
            now,
            null,
            result.Entry.FilePath);

        await _assetStore.UpsertAsync(asset, cancellationToken);
    }

    private sealed record ReadEntryResult(DecorationEntry Entry, byte[] Bytes);
}

public sealed record DecorationEntry(
    string FilePath,
    long SizeBytes,
    string Sha256,
    string BodyPart,
    string? MatchedToken
);
