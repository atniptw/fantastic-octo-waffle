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

    public ResponsiveLayoutE2ETests(BlazorServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// E2E Test: Verify layout is responsive on mobile (375px width) with no horizontal scroll.
    /// </summary>
    [Fact]
    public async Task Layout_ResponsiveOnMobile_NoHorizontalScroll()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

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

        // Set viewport to mobile size
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("📱 Testing mod card stacking on mobile...");

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
    /// E2E Test: Verify canvas uses responsive aspect ratio, not fixed height.
    /// </summary>
    [Fact]
    public async Task Viewer_Canvas_ResponsiveAspectRatio()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/viewer?mod=TestAuthor-Cigar", 
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("🖼️ Testing canvas responsive behavior...");

        var canvas = await page.QuerySelectorAsync("canvas#threeJsCanvas");
        Assert.NotNull(canvas);

        // Verify canvas has the viewer-canvas class
        var className = await canvas.GetAttributeAsync("class");
        Assert.Contains("viewer-canvas", className);

        // Test at desktop width
        await page.SetViewportSizeAsync(1024, 768);
        await Task.Delay(100); // Allow layout to settle

        var box1 = await canvas.BoundingBoxAsync();
        Assert.NotNull(box1);
        _output.WriteLine($"  Desktop (1024px): canvas width={box1.Width}px, height={box1.Height}px");

        // Test at tablet width
        await page.SetViewportSizeAsync(768, 1024);
        await Task.Delay(100);

        var box2 = await canvas.BoundingBoxAsync();
        Assert.NotNull(box2);
        _output.WriteLine($"  Tablet (768px): canvas width={box2.Width}px, height={box2.Height}px");

        // Test at mobile width
        await page.SetViewportSizeAsync(375, 667);
        await Task.Delay(100);

        var box3 = await canvas.BoundingBoxAsync();
        Assert.NotNull(box3);
        _output.WriteLine($"  Mobile (375px): canvas width={box3.Width}px, height={box3.Height}px");

        // Verify canvas maintains reasonable aspect ratio on all sizes
        var ratio1 = box1.Width / box1.Height;
        var ratio2 = box2.Width / box2.Height;
        var ratio3 = box3.Width / box3.Height;

        _output.WriteLine($"  Aspect ratios: desktop={ratio1:F2}, tablet={ratio2:F2}, mobile={ratio3:F2}");

        Assert.True(ratio1 >= 1.0, "Desktop canvas should have landscape aspect ratio");
        Assert.True(ratio3 >= 1.0, "Mobile canvas should maintain aspect ratio");

        await page.CloseAsync();
    }

    /// <summary>
    /// E2E Test: Verify file list stacks on mobile (full width).
    /// </summary>
    [Fact]
    public async Task Viewer_FileList_FullWidthOnMobile()
    {
        Assert.NotNull(_fixture.Browser);
        var page = await _fixture.Browser.NewPageAsync();

        // Set viewport to mobile size
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync($"{BlazorServerFixture.ServerUrl}/viewer?mod=TestAuthor-Cigar",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("📱 Testing file list width on mobile...");

        var fileListColumn = await page.QuerySelectorAsync(".file-list-column");
        Assert.NotNull(fileListColumn);

        var box = await fileListColumn.BoundingBoxAsync();
        Assert.NotNull(box);

        _output.WriteLine($"  File list column width: {box.Width}px");

        // On mobile, file list should take near full width (accounting for padding)
        Assert.True(box.Width >= 300, "File list should be near full width on mobile");

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

        await page.GotoAsync(BlazorServerFixture.ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        _output.WriteLine("⌨️ Testing keyboard navigation focus indicators...");

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
