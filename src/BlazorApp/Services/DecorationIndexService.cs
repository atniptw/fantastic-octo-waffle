using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.AspNetCore.Components.Forms;
using UnityAssetParser;

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
    private readonly HhhParser _hhhParser = new();
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
    public UnityPackageInventory? LastUnityPackageInventory { get; private set; }
    public bool LastUnityPackageScanSucceeded { get; private set; }
    public string? LastUnityPackageScanMessage { get; private set; }
    public DateTimeOffset? LastUnityPackageScanAt { get; private set; }
    public AvatarInventory? LastAvatarInventory { get; private set; }
    public event Action? OnChange;

    public Task LoadPersistedAsync(CancellationToken cancellationToken = default)
    {
        _loadTask ??= LoadPersistedInternalAsync(cancellationToken);
        return _loadTask;
    }

    public async Task LoadPersistedAvatarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var avatars = await _assetStore.GetAllAvatarMetadataAsync(cancellationToken);
            if (avatars.Count > 0)
            {
                var lastAvatar = avatars[avatars.Count - 1]; // Get the most recently stored avatar
                LastAvatarInventory = new AvatarInventory(lastAvatar.Id);
                NotifyStateChanged();
            }
        }
        catch
        {
            // Silent fail - avatar loading is not critical
        }
    }

    public Task ReloadPersistedAsync(CancellationToken cancellationToken = default)
    {
        _loadTask = LoadPersistedInternalAsync(cancellationToken);
        return _loadTask;
    }

    public async Task ScanZipAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        LastScanSucceeded = false;
        LastScanMessage = null;
        LastScanAt = DateTimeOffset.UtcNow;
        NotifyStateChanged();

        try
        {
            using var buffer = new MemoryStream(fileBytes);
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
            var modId = GetModId(fileName);
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
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            LastScanSucceeded = false;
            LastScanMessage = $"Scan failed: {ex.Message}";
            NotifyStateChanged();
        }
    }

    public async Task ScanZipAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        const long maxBytes = 1024L * 1024L * 512L;
        await using var stream = file.OpenReadStream(maxBytes, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        await ScanZipAsync(buffer.ToArray(), file.Name, cancellationToken);
    }

    public async Task ScanUnityPackageAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        LastUnityPackageInventory = null;
        LastUnityPackageScanSucceeded = false;
        LastUnityPackageScanMessage = null;
        LastUnityPackageScanAt = DateTimeOffset.UtcNow;
        NotifyStateChanged();

        try
        {
            var sha256 = ComputeSha256(fileBytes);
            var parser = new UnityPackageParser();
            var context = parser.Parse(fileBytes);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var anchors = context.SemanticAnchorPoints
                .Select(anchor => new UnityPackageAnchor(
                    anchor.Tag,
                    anchor.Name,
                    anchor.GameObjectPathId,
                    GetAnchorX(context, anchor.GameObjectPathId),
                    GetAnchorY(context, anchor.GameObjectPathId),
                    GetAnchorZ(context, anchor.GameObjectPathId)))
                .ToList();

            var resolvedPaths = context.Inventory.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ResolvedPath))
                .Select(entry => entry.ResolvedPath!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var inventory = new UnityPackageInventory(
                sha256,
                Path.GetFileName(fileName),
                now,
                now,
                anchors,
                resolvedPaths);

            await _assetStore.UpsertUnityPackageAsync(inventory, cancellationToken);
            
            var avatarError = TryExtractAvatarGlb(context, out var avatarGlbBytes);
            if (avatarError is null && avatarGlbBytes is not null && avatarGlbBytes.Length > 0)
            {
                var avatar = new StoredAvatar(
                    sha256 + "_avatar",
                    Path.GetFileName(fileName),
                    avatarGlbBytes,
                    avatarGlbBytes.LongLength,
                    now,
                    now);
                await _assetStore.UpsertAvatarAsync(avatar, cancellationToken);
                LastAvatarInventory = new AvatarInventory(avatar.Id);
            }
            else
            {
                LastAvatarInventory = null;
            }

            LastUnityPackageInventory = inventory;
            LastUnityPackageScanSucceeded = true;
            LastUnityPackageScanMessage = "Scan complete.";
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            LastUnityPackageScanSucceeded = false;
            LastUnityPackageScanMessage = $"Scan failed: {ex.Message}";
            NotifyStateChanged();
        }
    }

    public async Task ScanUnityPackageAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        const long maxBytes = 1024L * 1024L * 512L;
        await using var stream = file.OpenReadStream(maxBytes, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        await ScanUnityPackageAsync(buffer.ToArray(), file.Name, cancellationToken);
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
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            LastScanSucceeded = false;
            LastScanMessage = $"Library load failed: {ex.Message}";
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
        => OnChange?.Invoke();

    private static bool IsZipFile(string fileName)
        => fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnityPackageFile(string fileName)
        => fileName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase);

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
        var conversionContext = new BaseAssetsContext();
        var glbBytes = _hhhParser.ConvertToGlb(result.Bytes, conversionContext);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var asset = new StoredAsset(
            result.Entry.Sha256,
            modId,
            DecorationNameFormatter.GetDisplayName(result.Entry.FilePath),
            glbBytes,
            null,
            glbBytes.LongLength,
            now,
            now,
            now,
            result.Entry.FilePath);

        await _assetStore.UpsertAsync(asset, cancellationToken);
    }

    private sealed record ReadEntryResult(DecorationEntry Entry, byte[] Bytes);

    private static string? TryExtractAvatarGlb(BaseAssetsContext context, out byte[]? glbBytes)
    {
        glbBytes = null;

        var avatarGameObject = FindAvatarGameObject(context);
        if (avatarGameObject is null)
        {
            return "No player avatar model found.";
        }

        var avatarMesh = FindGameObjectMesh(context, avatarGameObject.PathId);
        if (avatarMesh is null)
        {
            return "Avatar GameObject has no mesh.";
        }

        try
        {
            var glbBuilder = new HhhParser();
            glbBytes = glbBuilder.ConvertToGlb(new byte[0], context);
            return null;
        }
        catch (Exception ex)
        {
            return $"Avatar GLB conversion failed: {ex.Message}";
        }
    }

    private static SemanticGameObjectInfo? FindAvatarGameObject(BaseAssetsContext context)
    {
        var avatarPatterns = new[] { "player", "character", "armature", "avatar", "body", "mesh" };

        var byNameMatch = context.SemanticGameObjects
            .FirstOrDefault(go => avatarPatterns.Any(pattern =>
                go.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

        if (byNameMatch is not null)
        {
            return byNameMatch;
        }

        var largestMesh = context.SemanticGameObjects
            .Where(go => FindGameObjectMesh(context, go.PathId) is not null)
            .Select(go => new { GameObject = go, Mesh = FindGameObjectMesh(context, go.PathId) })
            .OrderByDescending(x => x.Mesh?.VertexCount ?? 0)
            .FirstOrDefault();

        return largestMesh?.GameObject;
    }

    private static SemanticMeshInfo? FindGameObjectMesh(BaseAssetsContext context, long gameObjectPathId)
    {
        var meshFilter = context.SemanticMeshFilters
            .FirstOrDefault(mf => mf.GameObjectPathId == gameObjectPathId);

        if (meshFilter is null)
        {
            return null;
        }

        return context.SemanticMeshes
            .FirstOrDefault(mesh => mesh.PathId == meshFilter.MeshPathId);
    }

    private static float GetAnchorX(BaseAssetsContext context, long gameObjectPathId)
        => TryGetTransform(context, gameObjectPathId)?.LocalPosition.X ?? 0f;

    private static float GetAnchorY(BaseAssetsContext context, long gameObjectPathId)
        => TryGetTransform(context, gameObjectPathId)?.LocalPosition.Y ?? 0f;

    private static float GetAnchorZ(BaseAssetsContext context, long gameObjectPathId)
        => TryGetTransform(context, gameObjectPathId)?.LocalPosition.Z ?? 0f;

    private static SemanticTransformInfo? TryGetTransform(BaseAssetsContext context, long gameObjectPathId)
        => context.SemanticTransforms.FirstOrDefault(transform => transform.GameObjectPathId == gameObjectPathId);
}

public sealed record DecorationEntry(
    string FilePath,
    long SizeBytes,
    string Sha256,
    string BodyPart,
    string? MatchedToken
);
