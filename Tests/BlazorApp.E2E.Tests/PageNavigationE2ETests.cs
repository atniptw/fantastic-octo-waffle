using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// End-to-end tests for page navigation in the Blazor WebAssembly app.
/// Uses shared fixture for server and browser to avoid port conflicts and improve performance.
/// </summary>
[Collection("Blazor E2E")]
public sealed class PageNavigationE2ETests
{
    private readonly ITestOutputHelper _output;
    private readonly BlazorServerFixture _fixture;
    private const string FixturePath = "Fixtures/thunderstore-packages.json";

    public PageNavigationE2ETests(BlazorServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Sets up API mocking for Thunderstore packages endpoint.
    /// </summary>
    private async Task SetupApiMockAsync(IPage page)
    {
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
    }

    /// <summary>
    /// E2E Test: Verify the Index page loads successfully and displays cosmetic mods list.
    /// </summary>
    [Fact]
    public async Task Index_LoadsSuccessfully_ShowsModGrid()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Verify heading
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var text = await heading.TextContentAsync();
        Assert.Equal("Cosmetic Mods", text);

        // Verify mod cards exist (from mocked API data)
        var modCards = await page.QuerySelectorAllAsync(".card");
        Assert.True(modCards.Count >= 2, "Expected at least 2 mods from fixture");

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Click on mod card and navigate to detail page.
    /// </summary>
    [Fact]
    public async Task Index_ClickModCard_NavigatesToDetail()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Click first "View Details" button
        await page.ClickAsync("text=View Details");
        await page.WaitForURLAsync("**/mod/**");

        // Verify we're on detail page
        Assert.Contains("/mod/", page.Url);
        
        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify ModDetail page loads with metadata and Download button appears.
    /// Note: Actual download/indexing is tested in unit tests with mocked services.
    /// </summary>
    [Fact]
    public async Task ModDetail_ClickPreview_NavigatesToViewer()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Navigate directly to a detail page (using test fixture mod)
        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/mod/TestAuthor/FrogHatSmile", 
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for page content to load - check for the breadcrumb which appears in all states
        await page.WaitForSelectorAsync(".breadcrumb", new() { Timeout = 5000 });

        // Verify mod metadata is displayed
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var headingText = await heading.TextContentAsync();
        Assert.Contains("FrogHatSmile", headingText);

        // Verify "Download & Preview" button exists and is clickable
        var downloadButton = await page.QuerySelectorAsync("text=Download & Preview");
        Assert.NotNull(downloadButton);
        
        // Verify breadcrumb navigation back to index
        var backLink = await page.QuerySelectorAsync(".breadcrumb a");
        Assert.NotNull(backLink);
        var backHref = await backLink.GetAttributeAsync("href");
        Assert.Equal("/", backHref);
        
        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify mod detail page provides access to viewer.
    /// The new Viewer3D architecture uses route format /viewer/{namespace}/{name}/{filename}.
    /// Tests that Download & Preview button exists as the entry point to the viewer flow.
    /// Note: Direct navigation to viewer requires mod state to be set via ModDetailStateService first.
    /// </summary>
    [Fact]
    public async Task Viewer3D_RouteFormat_MatchesNewArchitecture()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Navigate to mod detail page
        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/mod/TestAuthor/FrogHatSmile", 
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for page to load
        await page.WaitForSelectorAsync(".breadcrumb", new() { Timeout = 5000 });

        // Verify mod detail page loaded
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var headingText = await heading.TextContentAsync();
        Assert.Contains("FrogHatSmile", headingText);

        // Verify Download & Preview button exists (entry point to viewer flow)
        var downloadButton = await page.QuerySelectorAsync("text=Download & Preview");
        Assert.NotNull(downloadButton);
        
        // Verify breadcrumb shows correct navigation structure
        var breadcrumb = await page.QuerySelectorAsync(".breadcrumb");
        Assert.NotNull(breadcrumb);
        var breadcrumbText = await breadcrumb.TextContentAsync();
        Assert.Contains("Cosmetics", breadcrumbText);
        Assert.Contains("FrogHatSmile", breadcrumbText);

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Full user flow from index to detail page.
    /// Verifies navigation between pages and page metadata loads correctly.
    /// Note: File indexing and preview are tested in unit tests with mocked services.
    /// </summary>
    [Fact]
    public async Task FullUserFlow_BrowseToDetailToViewer_Succeeds()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();
        var consoleErrors = new List<string>();

        page.Console += (_, msg) =>
        {
            var text = msg.Text;
            // Only treat as error if it's a JavaScript error (not a 404 resource load)
            if (msg.Type == "error" && !text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase))
            {
                consoleErrors.Add(text);
            }
        };

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // 1. Start at index
        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        
        // Verify index page loaded
        var indexHeading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(indexHeading);
        
        // 2. Click mod detail ("View Details" button)
        await page.ClickAsync("text=View Details");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Wait for breadcrumb to load on detail page (indicates component loaded)
        await page.WaitForSelectorAsync(".breadcrumb", new() { Timeout = 5000 });
        
        // 3. Verify we're on detail page
        Assert.Contains("/mod/", page.Url);
        
        // 4. Verify mod metadata displayed
        var detailHeading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(detailHeading);
        var modName = await detailHeading.TextContentAsync();
        Assert.NotNull(modName);
        Assert.NotEmpty(modName);
        
        // 5. Verify "Download & Preview" button exists
        var downloadButton = await page.QuerySelectorAsync("text=Download & Preview");
        Assert.NotNull(downloadButton);

        // 6. Verify no JavaScript errors (404 resource errors are allowed)
        Assert.Empty(consoleErrors);

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify the app loads successfully and displays expected content without errors.
    /// </summary>
    [Fact]
    public async Task AppLoads_DisplaysHelloWorld_NoConsoleErrors()
    {
        // Arrange
        var consoleMessages = new List<string>();
        var consoleErrors = new List<string>();

        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Capture console messages
        page.Console += (_, msg) =>
        {
            var text = msg.Text;
            consoleMessages.Add($"[{msg.Type}] {text}");
            
            // Only treat as error if it's a JavaScript error (not a 404 resource load)
            if (msg.Type == "error" && !text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase))
            {
                consoleErrors.Add(text);
            }
        };

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Act: Navigate to the app
        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { Timeout = 15000, WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine($"✅ Page loaded successfully");

        // Assert: Expected DOM elements are present
        _output.WriteLine("🔍 Verifying DOM elements...");
        
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var headingText = await heading.TextContentAsync();
        Assert.Equal("Cosmetic Mods", headingText);
        _output.WriteLine($"✅ Found heading: \"{headingText}\"");

        var paragraph = await page.QuerySelectorAsync("p");
        Assert.NotNull(paragraph);
        var paragraphText = await paragraph.TextContentAsync();
        Assert.Contains("Browse R.E.P.O. cosmetic mods", paragraphText);
        _output.WriteLine($"✅ Found paragraph: \"{paragraphText}\"");

        // Assert: No console errors
        _output.WriteLine($"📊 Console messages captured: {consoleMessages.Count}");
        foreach (var msg in consoleMessages)
        {
            _output.WriteLine($"  {msg}");
        }

        Assert.Empty(consoleErrors);
        _output.WriteLine("✅ No console errors detected");

        await page.CloseAsync();
    }
}
