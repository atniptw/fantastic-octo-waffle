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
    /// E2E Test: Click preview button on detail page and navigate to viewer.
    /// </summary>
    [Fact]
    public async Task ModDetail_ClickPreview_NavigatesToViewer()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Navigate directly to a detail page
        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/mod/TestAuthor/Cigar", 
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Click preview button
        await page.ClickAsync("text=Preview 3D Assets");
        await page.WaitForURLAsync("**/viewer?mod=**");

        // Verify we're on viewer page
        Assert.Contains("/viewer", page.Url);
        
        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Viewer page shows file list and canvas.
    /// Canvas uses responsive aspect-ratio (16:9) with min-height of 300px on mobile.
    /// </summary>
    [Fact]
    public async Task Viewer3D_ShowsFileList_AndCanvas()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/viewer?mod=TestAuthor-Cigar",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Verify file list
        var fileItems = await page.QuerySelectorAllAsync("ul.list-group li");
        Assert.NotEmpty(fileItems);

        // Verify canvas exists and is visible
        var canvas = await page.QuerySelectorAsync("canvas#threeJsCanvas");
        Assert.NotNull(canvas);
        
        // Verify canvas dimensions (responsive 16:9 aspect ratio with min 300px height)
        var box = await canvas.BoundingBoxAsync();
        Assert.NotNull(box);
        _output.WriteLine($"Canvas dimensions: {box.Width}px × {box.Height}px (aspect ratio: {box.Width / box.Height:F2})");
        
        // Canvas should maintain minimum height (CSS sets min-height: 300px on mobile)
        Assert.True(box.Height >= 300, $"Canvas should be at least 300px tall (actual: {box.Height}px)");
        
        // Canvas should maintain reasonable aspect ratio (16:9 = 1.78)
        var aspectRatio = box.Width / box.Height;
        Assert.True(aspectRatio >= 1.3 && aspectRatio <= 2.0, 
            $"Canvas aspect ratio should be between 1.3 and 2.0 (actual: {aspectRatio:F2})");

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Full user flow from index to detail to viewer.
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
        
        // 2. Click mod detail
        await page.ClickAsync("text=View Details");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // 3. Click preview
        await page.ClickAsync("text=Preview 3D Assets");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 4. Verify no JavaScript errors (404 resource errors are allowed)
        Assert.Empty(consoleErrors);

        // 5. Verify canvas rendered
        var canvas = await page.QuerySelectorAsync("canvas#threeJsCanvas");
        Assert.NotNull(canvas);

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
