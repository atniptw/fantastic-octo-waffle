using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// End-to-end tests for the Blazor WebAssembly Hello World app.
/// Verifies the app builds successfully and loads without runtime errors.
/// </summary>
public sealed class HelloWorldE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private Process? _serverProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string ServerUrl = "http://localhost:5000";
    private const int ServerStartTimeoutSeconds = 30;
    private const int PageLoadTimeoutSeconds = 15;
    private readonly Stopwatch _stopwatch = new();

    public HelloWorldE2ETests(ITestOutputHelper output)
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

        // Step 2: Verify build artifacts exist
        _output.WriteLine("📦 Verifying build artifacts...");
        VerifyBuildArtifacts();
        _output.WriteLine($"✅ All build artifacts verified (elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");

        // Step 3: Start development server
        _output.WriteLine("🚀 Starting development server...");
        var serverStartTime = _stopwatch.Elapsed;
        await StartDevelopmentServerAsync();
        var serverReadyTime = _stopwatch.Elapsed - serverStartTime;
        _output.WriteLine($"✅ Development server ready in {serverReadyTime.TotalSeconds:F2}s (total elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");

        // Step 4: Initialize Playwright browser
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
    /// E2E Test: Verify the app loads successfully and displays expected content without errors.
    /// </summary>
    [Fact]
    public async Task AppLoads_DisplaysHelloWorld_NoConsoleErrors()
    {
        // Arrange
        var pageLoadStart = _stopwatch.Elapsed;
        var consoleMessages = new List<string>();
        var consoleErrors = new List<string>();

        Assert.NotNull(_browser);
        var page = await _browser.NewPageAsync();

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

        // Act: Navigate to the app
        _output.WriteLine($"🌐 Navigating to {ServerUrl}...");
        var response = await page.GotoAsync(ServerUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = PageLoadTimeoutSeconds * 1000
        });

        var pageLoadTime = _stopwatch.Elapsed - pageLoadStart;
        _output.WriteLine($"✅ Page loaded in {pageLoadTime.TotalSeconds:F2}s");

        // Assert: Response is successful
        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected HTTP 200, got {response.Status}");

        // Assert: Expected DOM elements are present
        _output.WriteLine("🔍 Verifying DOM elements...");
        
        var heading = await page.QuerySelectorAsync("h1");
        Assert.NotNull(heading);
        var headingText = await heading.TextContentAsync();
        Assert.Equal("Hello, world!", headingText);
        _output.WriteLine($"✅ Found heading: \"{headingText}\"");

        var paragraph = await page.QuerySelectorAsync("p");
        Assert.NotNull(paragraph);
        var paragraphText = await paragraph.TextContentAsync();
        Assert.Equal("Welcome to your new app.", paragraphText);
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
        _output.WriteLine($"✅ Test completed successfully (total elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s)");
    }

    /// <summary>
    /// Run dotnet build in Debug configuration and return exit code.
    /// </summary>
    private async Task<int> RunBuildAsync()
    {
        var projectPath = GetBlazorAppPath();
        var buildProcess = new Process
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
    /// Verify that all expected build artifacts exist.
    /// </summary>
    private void VerifyBuildArtifacts()
    {
        var projectPath = GetBlazorAppPath();
        var binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0");

        if (!Directory.Exists(binPath))
        {
            throw new DirectoryNotFoundException($"Build output directory not found: {binPath}");
        }

        // Check for compiled DLL
        var dllPath = Path.Combine(binPath, "BlazorApp.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"Compiled DLL not found: {dllPath}");
        }

        // Check for wwwroot output (contains JavaScript runtime, etc.)
        var wwwrootPath = Path.Combine(binPath, "wwwroot");
        if (!Directory.Exists(wwwrootPath))
        {
            throw new DirectoryNotFoundException($"wwwroot directory not found: {wwwrootPath}");
        }

        // Check for _framework directory (contains Blazor framework files)
        var frameworkPath = Path.Combine(wwwrootPath, "_framework");
        if (!Directory.Exists(frameworkPath))
        {
            throw new DirectoryNotFoundException($"_framework directory not found: {frameworkPath}");
        }

        // Note: In Debug builds, static content (index.html, CSS) is served from the source wwwroot,
        // not copied to bin/Debug. This is expected Blazor behavior.
        // Check WASM/JS runtime files instead
        var dotnetJsPath = Path.Combine(binPath, "dotnet.js");
        if (!File.Exists(dotnetJsPath))
        {
            throw new FileNotFoundException($"Blazor runtime (dotnet.js) not found: {dotnetJsPath}");
        }

        _output.WriteLine($"  ✓ DLL: {dllPath}");
        _output.WriteLine($"  ✓ wwwroot: {wwwrootPath}");
        _output.WriteLine($"  ✓ _framework: {frameworkPath}");
        _output.WriteLine($"  ✓ Runtime: {dotnetJsPath}");
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
                    ["DOTNET_LAUNCH_BROWSER"] = "false"  // Disable auto-launch of browser
                }
            }
        };

        var serverReady = new TaskCompletionSource<bool>();
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ServerStartTimeoutSeconds));
        timeout.Token.Register(() => serverReady.TrySetCanceled());

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
                // Only log non-xdg-open errors (browser launch errors are expected in headless)
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
            // Give server a moment to fully initialize
            await Task.Delay(2000);
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
        // Working directory during test execution: Tests/BlazorApp.E2E.Tests/bin/Debug/net10.0
        // Navigate to repo root, then to src/BlazorApp
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
