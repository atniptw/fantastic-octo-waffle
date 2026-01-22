using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BlazorApp;
using BlazorApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for ThunderstoreService
var workerUrl = builder.Configuration["WorkerBaseUrl"] ?? "http://localhost:8787";
builder.Services.AddHttpClient<IThunderstoreService, ThunderstoreService>(client =>
{
    client.BaseAddress = new Uri(workerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "RepoModViewer/0.1 (+https://atniptw.github.io)");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configure HttpClient for Cloudflare Worker API (used by other services)
builder.Services.AddHttpClient("WorkerAPI", client =>
{
    // TODO: Update with actual Worker URL when available (see docs/CloudflareWorker.md)
    // Note: ThunderstoreService has its own dedicated HttpClient configured above
    client.BaseAddress = new Uri("https://api.worker.dev");
    client.DefaultRequestHeaders.Add("User-Agent", "RepoModViewer/0.1 (+https://atniptw.github.io)");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Default HttpClient for components (falls back to app base address)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("WorkerAPI"));

// Register service interfaces with stub implementations
builder.Services.AddScoped<IZipDownloader, ZipDownloader>();
builder.Services.AddScoped<IZipIndexer, ZipIndexer>();
builder.Services.AddScoped<IAssetScanner, AssetScanner>();
builder.Services.AddScoped<IAssetRenderer, AssetRenderer>();
builder.Services.AddScoped<IViewerService, ViewerService>();

await builder.Build().RunAsync();
