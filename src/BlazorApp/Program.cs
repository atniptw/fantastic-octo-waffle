using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp;
using RepoMod.Glb.Abstractions;
using RepoMod.Glb.Implementation;
using RepoMod.Parser.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddRepoModParser();
builder.Services.AddScoped<IGlbCompositionPlanner, GlbCompositionPlanner>();
builder.Services.AddScoped<IGlbSerializer, GlbSerializer>();

await builder.Build().RunAsync();
