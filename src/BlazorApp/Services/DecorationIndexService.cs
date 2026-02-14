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

    private readonly List<DecorationEntry> _entries = new();

    public IReadOnlyList<DecorationEntry> Entries => _entries;
    public bool LastScanSucceeded { get; private set; }
    public string? LastScanMessage { get; private set; }
    public DateTimeOffset? LastScanAt { get; private set; }

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

            foreach (var entry in archive.Entries)
            {
                if (!IsHhhEntry(entry))
                {
                    continue;
                }

                var item = await ReadEntryAsync(entry, cancellationToken);
                _entries.Add(item);
            }

            LastScanSucceeded = true;
            LastScanMessage = $"Scan complete: {_entries.Count} .hhh file(s) found.";
        }
        catch (Exception ex)
        {
            LastScanSucceeded = false;
            LastScanMessage = $"Scan failed: {ex.Message}";
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

    private static async Task<DecorationEntry> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        var bodyPart = InferBodyPart(entry.FullName, out var matchedToken);
        var sizeBytes = entry.Length;

        await using var entryStream = entry.Open();
        var sha256 = await ComputeSha256Async(entryStream, cancellationToken);

        return new DecorationEntry(entry.FullName, sizeBytes, sha256, bodyPart, matchedToken);
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

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record DecorationEntry(
    string FilePath,
    long SizeBytes,
    string Sha256,
    string BodyPart,
    string? MatchedToken
);
