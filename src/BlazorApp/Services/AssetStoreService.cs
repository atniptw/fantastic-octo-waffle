using Microsoft.JSInterop;

namespace BlazorApp.Services;

public sealed class AssetStoreService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public AssetStoreService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/assetStore.js").AsTask());
    }

    public async Task UpsertAsync(StoredAsset asset, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("putAsset", cancellationToken, asset);
    }

    public async Task<IReadOnlyList<StoredAssetMetadata>> GetAllMetadataAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        var assets = await module.InvokeAsync<StoredAssetMetadata[]>("getAllAssetMetadata", cancellationToken);
        return assets;
    }

    public async Task UpsertUnityPackageAsync(UnityPackageInventory inventory, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("putUnityPackage", cancellationToken, inventory);
    }

    public async Task<IReadOnlyList<UnityPackageInventoryMetadata>> GetAllUnityPackageMetadataAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        var items = await module.InvokeAsync<UnityPackageInventoryMetadata[]>("getAllUnityPackageMetadata", cancellationToken);
        return items;
    }

    public async Task<UnityPackageInventory?> GetUnityPackageByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<UnityPackageInventory?>("getUnityPackageById", cancellationToken, id);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearAssets", cancellationToken);
        await module.InvokeVoidAsync("clearUnityPackages", cancellationToken);
    }

    public async Task DeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteDatabase", cancellationToken);
    }

    public async Task<bool> TouchProcessedAsync(string id, long processedAt, long lastUsed, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("touchAsset", cancellationToken, id, processedAt, lastUsed);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_moduleTask.IsValueCreated)
        {
            return;
        }

        var module = await _moduleTask.Value;
        await module.DisposeAsync();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
        => await _moduleTask.Value;
}
