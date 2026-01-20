using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// End-to-end tests for responsive layout in the Blazor WebAssembly app.
/// Verifies the app layout is responsive and works correctly at different viewport sizes.
/// </summary>
public sealed class ResponsiveLayoutE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private Process? _serverProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string ServerUrl = "http://localhost:5000";
    private const int ServerStartTimeoutSeconds = 30;
    private const int PageLoadTimeoutSeconds = 15;
    private readonly Stopwatch _stopwatch = new();

    public ResponsiveLayoutE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Setup: Build the app, start the development server, and initialize browser.
    /// </summary>
    public async Task InitializeAsync()
    {
        _stopwatch.Start();

        // Step 1: Build the app (Debug configuration)
        _output.WriteLine("🔨 Building Blazor app in Debug configuration...");
        var buildResult = await RunBuildAsync();
        if (buildResult != 0)
        {
            throw new InvalidOperationException("Build failed with errors. Check build output for details.");
        }
        _output.WriteLine($"✅ Build completed successfully (elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");

        // Step 2: Start development server
        _output.WriteLine("🚀 Starting development server...");
        var serverStartTime = _stopwatch.Elapsed;
        await StartDevelopmentServerAsync();
        var serverReadyTime = _stopwatch.Elapsed - serverStartTime;
        _output.WriteLine($"✅ Development server ready in {serverReadyTime.TotalSeconds:F2}s (total elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");

        // Step 3: Initialize Playwright browser
        _output.WriteLine("🌐 Initializing browser (Chromium)...");
        await InitializeBrowserAsync();
        _output.WriteLine($"✅ Browser initialized (elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");
    }

    /// <summary>
    /// Teardown: Stop the development server and dispose browser resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        _output.WriteLine("🧹 Cleaning up resources...");

        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }

        if (_playwright != null)
        {
            _playwright.Dispose();
        }

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            await _serverProcess.WaitForExitAsync();
            _serverProcess.Dispose();
        }

        _output.WriteLine($"✅ Cleanup completed (total test time: {_stopwatch.Elapsed.TotalSeconds:F2}s)");
        _stopwatch.Stop();
    }

    /// <summary>
    /// E2E Test: Verify layout is responsive on mobile (375px width) with no horizontal scroll.
    /// </summary>
    [Fact]
    public async Task Layout_ResponsiveOnMobile_NoHorizontalScroll()
    {
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        // Set viewport to mobile size (iPhone SE)
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync(ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

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
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        await page.GotoAsync(ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

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
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        // Set viewport to mobile size
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync(ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

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
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        await page.GotoAsync($"{ServerUrl}/viewer?mod=TestAuthor-Cigar", 
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
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        // Set viewport to mobile size
        await page.SetViewportSizeAsync(375, 667);
        await page.GotoAsync($"{ServerUrl}/viewer?mod=TestAuthor-Cigar",
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
        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

        await page.GotoAsync(ServerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

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

    /// <summary>
    /// Run dotnet build in Debug configuration and return exit code.
    /// </summary>
    private async Task<int> RunBuildAsync()
    {
        var projectPath = GetBlazorAppPath();
        using var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build -c Debug --no-restore",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        buildProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputLines.Add(e.Data);
        };

        buildProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorLines.Add(e.Data);
        };

        buildProcess.Start();
        buildProcess.BeginOutputReadLine();
        buildProcess.BeginErrorReadLine();

        await buildProcess.WaitForExitAsync();

        if (buildProcess.ExitCode != 0)
        {
            _output.WriteLine("❌ Build failed:");
            foreach (var line in outputLines.Concat(errorLines))
            {
                _output.WriteLine($"  {line}");
            }
        }

        return buildProcess.ExitCode;
    }

    /// <summary>
    /// Start the development server and wait for it to be ready.
    /// </summary>
    private async Task StartDevelopmentServerAsync()
    {
        var projectPath = GetBlazorAppPath();
        
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-build",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_URLS"] = ServerUrl,
                    ["DOTNET_LAUNCH_BROWSER"] = "false"
                }
            }
        };

        var serverReady = new TaskCompletionSource<bool>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ServerStartTimeoutSeconds));
        using var timeoutRegistration = timeout.Token.Register(() => serverReady.TrySetCanceled());

        var allServerOutput = new List<string>();

        _serverProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                allServerOutput.Add($"[OUT] {e.Data}");
                _output.WriteLine($"  [SERVER] {e.Data}");
                if (e.Data.Contains("Now listening on:", StringComparison.OrdinalIgnoreCase) ||
                    e.Data.Contains("Application started", StringComparison.OrdinalIgnoreCase) ||
                    e.Data.Contains("Content root path:", StringComparison.OrdinalIgnoreCase))
                {
                    serverReady.TrySetResult(true);
                }
            }
        };

        _serverProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                allServerOutput.Add($"[ERR] {e.Data}");
                if (!e.Data.Contains("xdg-open", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine($"  [SERVER ERROR] {e.Data}");
                }
            }
        };

        _serverProcess.Start();
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        try
        {
            await serverReady.Task;
            await WaitForServerHealthAsync();
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("❌ Server startup timeout. Full output:");
            foreach (var line in allServerOutput)
            {
                _output.WriteLine($"  {line}");
            }
            throw new TimeoutException($"Server failed to start within {ServerStartTimeoutSeconds} seconds");
        }

        if (_serverProcess.HasExited)
        {
            throw new InvalidOperationException($"Server process exited prematurely with code {_serverProcess.ExitCode}");
        }
    }

    /// <summary>
    /// Wait for server to be fully ready by polling the health endpoint.
    /// </summary>
    private async Task WaitForServerHealthAsync()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var maxAttempts = 10;
        var delayBetweenAttempts = TimeSpan.FromMilliseconds(500);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(ServerUrl);
                if (response.IsSuccessStatusCode)
                {
                    _output.WriteLine($"  ✓ Server health check passed (attempt {attempt}/{maxAttempts})");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Server not ready yet, continue polling
            }
            catch (TaskCanceledException)
            {
                // Request timeout, continue polling
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(delayBetweenAttempts);
            }
        }

        throw new TimeoutException($"Server failed health check after {maxAttempts} attempts");
    }

    /// <summary>
    /// Initialize Playwright and launch Chromium browser.
    /// </summary>
    private async Task InitializeBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    /// <summary>
    /// Get the absolute path to the BlazorApp project directory.
    /// </summary>
    private static string GetBlazorAppPath()
    {
        var testDir = Directory.GetCurrentDirectory();
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var projectPath = Path.Combine(repoRoot, "src", "BlazorApp");

        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"BlazorApp project not found at: {projectPath}");
        }

        return projectPath;
    }
}
