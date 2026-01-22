using Microsoft.Playwright;
using Xunit;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// End-to-end tests for Thunderstore service integration with mocked API responses.
/// Uses Playwright route interception for fast, deterministic, offline-capable testing.
/// </summary>
[Collection("Blazor E2E")]
public sealed class ThunderstoreIntegrationE2ETests
{
    private readonly BlazorServerFixture _fixture;
    private const string FixturePath = "Fixtures/thunderstore-packages.json";

    public ThunderstoreIntegrationE2ETests(BlazorServerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// E2E Test: Index page loads mods from Worker API and displays cosmetic mods.
    /// </summary>
    [Fact]
    public async Task Index_LoadsModsFromWorker_DisplaysCosmeticMods()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        try
        {
            // Intercept Worker API calls with fixture data
            var fixtureJson = await File.ReadAllTextAsync(FixturePath);
            await page.RouteAsync("**/api/packages", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = fixtureJson
                });
            });

            await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for loading to complete and mod cards to render
            await page.WaitForSelectorAsync(".card", new() { Timeout = 10000 });

            // Verify mod cards rendered
            var modCards = await page.QuerySelectorAllAsync(".card");
            Assert.Equal(2, modCards.Count); // MoreHead + FrogHatSmile

            // Verify specific mod data (MoreHead)
            var moreheadCard = await page.QuerySelectorAsync("text=MoreHead");
            Assert.NotNull(moreheadCard);

            // Verify owner displayed
            var ownerText = await page.QuerySelectorAsync("text=by YMC_MHZ");
            Assert.NotNull(ownerText);

            // Verify FrogHatSmile present
            var froghatCard = await page.QuerySelectorAsync("text=FrogHatSmile");
            Assert.NotNull(froghatCard);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// E2E Test: Worker returns 500 error, displays user-friendly error message.
    /// </summary>
    [Fact]
    public async Task Index_WorkerReturnsError_DisplaysErrorMessage()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        try
        {
            // Simulate Worker error
            await page.RouteAsync("**/api/packages", async route =>
            {
                await route.FulfillAsync(new() { Status = 500 });
            });

            await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for error message to display
            await page.WaitForSelectorAsync(".alert-danger", new() { Timeout = 5000 });

            // Verify error message displayed
            var errorAlert = await page.QuerySelectorAsync(".alert-danger");
            Assert.NotNull(errorAlert);

            var errorText = await errorAlert.TextContentAsync();
            Assert.Contains("Mod service temporarily unavailable", errorText);

            // Verify retry button exists
            var retryButton = await page.QuerySelectorAsync("button:has-text('Retry')");
            Assert.NotNull(retryButton);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// E2E Test: Click retry button after error, successfully loads data on second attempt.
    /// </summary>
    [Fact]
    public async Task Index_ClickRetry_RefetchesData()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        try
        {
            var callCount = 0;
            var fixtureJson = await File.ReadAllTextAsync(FixturePath);

            await page.RouteAsync("**/api/packages", async route =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call fails
                    await route.FulfillAsync(new() { Status = 500 });
                }
                else
                {
                    // Retry succeeds
                    await route.FulfillAsync(new()
                    {
                        Status = 200,
                        ContentType = "application/json",
                        Body = fixtureJson
                    });
                }
            });

            await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for error state
            await page.WaitForSelectorAsync(".alert-danger", new() { Timeout = 5000 });

            // Click retry button
            await page.ClickAsync("button:has-text('Retry')");

            // Wait for data to load after retry
            await page.WaitForSelectorAsync(".card", new() { Timeout = 10000 });

            // Verify data loaded successfully
            var modCards = await page.QuerySelectorAllAsync(".card");
            Assert.Equal(2, modCards.Count);

            // Verify no error message displayed
            var errorAlert = await page.QuerySelectorAsync(".alert-danger");
            Assert.Null(errorAlert);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// E2E Test: Worker returns empty array, displays "no mods found" message.
    /// </summary>
    [Fact]
    public async Task Index_WorkerReturnsEmptyArray_DisplaysNoModsMessage()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        try
        {
            // Return empty array
            await page.RouteAsync("**/api/packages", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "[]"
                });
            });

            await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for info alert
            await page.WaitForSelectorAsync(".alert-info", new() { Timeout = 5000 });

            // Verify "no mods" message
            var infoAlert = await page.QuerySelectorAsync(".alert-info");
            Assert.NotNull(infoAlert);

            var infoText = await infoAlert.TextContentAsync();
            Assert.Contains("No cosmetic mods found", infoText);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// E2E Test: Loading spinner displays before data loads.
    /// </summary>
    [Fact]
    public async Task Index_ShowsLoadingSpinner_BeforeDataLoads()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        try
        {
            var fixtureJson = await File.ReadAllTextAsync(FixturePath);
            var routeHandled = false;

            await page.RouteAsync("**/api/packages", async route =>
            {
                // Delay response significantly to reliably capture loading state
                await Task.Delay(3000);
                routeHandled = true;
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = fixtureJson
                });
            });

            // Navigate to page
            await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Wait for and verify loading spinner appears (deterministic assertion)
            var spinner = await page.WaitForSelectorAsync(".spinner-border", new() { Timeout = 5000 });
            Assert.NotNull(spinner);

            // Wait for data to load (spinner should disappear)
            await page.WaitForSelectorAsync(".card", new() { Timeout = 10000 });

            // Verify spinner is gone after data loads
            spinner = await page.QuerySelectorAsync(".spinner-border");
            Assert.Null(spinner);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
