using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlazorApp;
using BlazorApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure logging levels for debugging
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("BlazorApp.Services.ZipIndexer", LogLevel.Debug);
builder.Logging.AddFilter("BlazorApp.Pages.ModDetail", LogLevel.Debug);

// Configure HttpClient for ThunderstoreService
var workerUrl = builder.Configuration["WorkerBaseUrl"] ?? "http://localhost:8787";
builder.Services.AddHttpClient<IThunderstoreService, ThunderstoreService>(client =>
{
    client.BaseAddress = new Uri(workerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    // Note: User-Agent cannot be set in Blazor WASM (Fetch API restriction)
    // Set User-Agent on the Worker's upstream requests instead
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configure HttpClient for ZipDownloader (uses same worker)
builder.Services.AddHttpClient<IZipDownloader, ZipDownloader>(client =>
{
    client.BaseAddress = new Uri(workerUrl);
    client.Timeout = TimeSpan.FromSeconds(300); // 5 min for large downloads
    client.DefaultRequestHeaders.Add("Accept", "application/zip, application/json");
});

// Configure HttpClient for Cloudflare Worker API (used by other services)
builder.Services.AddHttpClient("WorkerAPI", client =>
{
    client.BaseAddress = new Uri(workerUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Default HttpClient for components (falls back to worker base address)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("WorkerAPI"));

// Register service interfaces with implementations
builder.Services.AddScoped<IZipIndexer, ZipIndexer>();
builder.Services.AddScoped<IAssetScanner, AssetScanner>();
builder.Services.AddScoped<IAssetRenderer, AssetRenderer>();
builder.Services.AddScoped<IViewerService, ViewerService>();
builder.Services.AddScoped<IModDetailStateService, ModDetailStateService>();
builder.Services.AddScoped<IGltfExportService, GltfExportService>();
builder.Services.AddScoped<ICacheStorageService, CacheStorageService>();

var host = builder.Build();
ServiceLocator.Provider = host.Services;
await host.RunAsync();
