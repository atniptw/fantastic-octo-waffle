using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// End-to-end tests for responsive layout in the Blazor WebAssembly app.
/// Uses shared fixture for server and browser to avoid port conflicts and improve performance.
/// </summary>
[Collection("Blazor E2E")]
public sealed class ResponsiveLayoutE2ETests
{
    private readonly ITestOutputHelper _output;
    private readonly BlazorServerFixture _fixture;
    private const string FixturePath = "Fixtures/thunderstore-packages.json";

    public ResponsiveLayoutE2ETests(BlazorServerFixture fixture, ITestOutputHelper output)
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
    /// E2E Test: Verify layout is responsive on mobile (375px width) with no horizontal scroll.
    /// </summary>
    [Fact]
    public async Task Layout_ResponsiveOnMobile_NoHorizontalScroll()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Set viewport to mobile size (iPhone SE)
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("📱 Testing mobile layout (375px width)...");

        // Assert no horizontal scrollbar
        var bodyWidth = await page.EvaluateAsync<int>("() => document.body.scrollWidth");
        var windowWidth = await page.EvaluateAsync<int>("() => window.innerWidth");
        
        _output.WriteLine($"  Body scroll width: {bodyWidth}px");
        _output.WriteLine($"  Window inner width: {windowWidth}px");
        
        Assert.True(bodyWidth <= windowWidth + 1, $"Expected no horizontal scroll: body width {bodyWidth}px should be <= window width {windowWidth}px");

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify navbar brand text is correct.
    /// </summary>
    [Fact]
    public async Task Layout_NavbarBrand_DisplaysCorrectText()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("🔍 Testing navbar brand text...");

        var brandText = await page.TextContentAsync(".navbar-brand");
        
        _output.WriteLine($"  Brand text: {brandText}");
        
        Assert.Equal("R.E.P.O. Mod Browser", brandText);

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify mod cards stack properly on mobile.
    /// </summary>
    [Fact]
    public async Task Index_ModCards_StackOnMobile()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Set viewport to mobile size
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("📱 Testing mod card stacking on mobile...");

        // Wait for mod cards to render
        await page.WaitForSelectorAsync(".mod-card", new() { Timeout = 10000 });

        // Get all mod cards
        var cards = await page.QuerySelectorAllAsync(".mod-card");
        Assert.True(cards.Count >= 2, "Expected at least 2 mod cards");

        // Verify cards are stacked (each card takes full width)
        for (int i = 0; i < cards.Count; i++)
        {
            var box = await cards[i].BoundingBoxAsync();
            Assert.NotNull(box);
            
            _output.WriteLine($"  Card {i + 1}: width={box.Width}px, x={box.X}px");
            
            // On mobile, cards should be near full width (accounting for padding)
            Assert.True(box.Width >= 300, $"Card {i + 1} should be at least 300px wide on mobile");
        }

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify viewer is accessible through ModDetail page.
    /// The new Viewer3D architecture requires users to download a mod first,
    /// which sets up the necessary state before navigating to the viewer.
    /// This test verifies the Download & Preview button exists as the gateway to viewer.
    /// </summary>
    [Fact]
    public async Task Viewer_AccessibleViaModDetail()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        // Navigate to mod detail page (entry point to viewer)
        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/mod/TestAuthor/FrogHatSmile", 
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("🖼️ Testing viewer access via mod detail page...");

        // Wait for page to load
        await page.WaitForSelectorAsync(".breadcrumb", new() { Timeout = 5000 });

        // Verify breadcrumb navigation exists
        var breadcrumb = await page.QuerySelectorAsync(".breadcrumb");
        Assert.NotNull(breadcrumb);
        _output.WriteLine("  ✓ Breadcrumb navigation exists");

        // Verify Download & Preview button exists (gateway to viewer)
        var downloadButton = await page.QuerySelectorAsync("text=Download & Preview");
        Assert.NotNull(downloadButton);
        _output.WriteLine("  ✓ Download & Preview button available");

        // Verify mod heading
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var headingText = await heading.TextContentAsync();
        Assert.Contains("FrogHatSmile", headingText);
        _output.WriteLine($"  ✓ Mod heading: {headingText}");

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify focus indicators are visible for keyboard navigation.
    /// </summary>
    [Fact]
    public async Task Layout_FocusIndicators_VisibleOnKeyboardNav()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Mock API before navigation
        await SetupApiMockAsync(page);

        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("⌨️ Testing keyboard navigation focus indicators...");

        // Wait for mod cards to render
        await page.WaitForSelectorAsync(".btn-primary", new() { Timeout = 10000 });

        // Focus first button (View Details)
        var button = await page.QuerySelectorAsync(".btn-primary");
        Assert.NotNull(button);

        await button.FocusAsync();

        // Get computed outline style
        var outline = await page.EvaluateAsync<string>(@"() => {
            const btn = document.querySelector('.btn-primary:focus');
            if (!btn) return 'none';
            const style = window.getComputedStyle(btn);
            return style.outline;
        }");

        _output.WriteLine($"  Button focus outline: {outline}");

        // Verify outline is visible (not 'none' or empty)
        Assert.NotNull(outline);
        Assert.NotEmpty(outline);
        Assert.DoesNotContain("none", outline.ToLowerInvariant());

        await page.CloseAsync();
    }
}
