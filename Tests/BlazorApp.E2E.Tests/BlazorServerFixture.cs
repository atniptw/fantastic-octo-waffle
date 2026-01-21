using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace BlazorApp.E2E.Tests;

/// <summary>
/// Shared fixture for Blazor E2E tests. Builds the app once, starts a single server,
/// and initializes a browser instance shared across all tests.
/// </summary>
public sealed class BlazorServerFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private IPlaywright? _playwright;
    public IBrowser? Browser { get; private set; }
    public const string ServerUrl = "http://localhost:5000";
    private const int ServerStartTimeoutSeconds = 30;
    private readonly List<string> _outputLog = new();

    /// <summary>
    /// Setup: Build the app once, start the development server, and initialize browser.
    /// Called once before all tests in the collection.
    /// </summary>
    public async Task InitializeAsync()
    {
        Console.WriteLine("🏗️ [FIXTURE] Setting up shared Blazor server and browser...");

        // Step 1: Build the app (Debug configuration) - ONCE for all tests
        Console.WriteLine("🔨 [FIXTURE] Building Blazor app in Debug configuration...");
        var buildResult = await RunBuildAsync();
        if (buildResult != 0)
        {
            throw new InvalidOperationException("Build failed with errors. Check build output for details.");
        }
        Console.WriteLine("✅ [FIXTURE] Build completed successfully");

        // Step 2: Start development server - ONCE for all tests
        Console.WriteLine("🚀 [FIXTURE] Starting development server...");
        await StartDevelopmentServerAsync();
        Console.WriteLine("✅ [FIXTURE] Development server ready");

        // Step 3: Initialize Playwright browser - ONCE for all tests
        Console.WriteLine("🌐 [FIXTURE] Initializing browser (Chromium)...");
        await InitializeBrowserAsync();
        Console.WriteLine("✅ [FIXTURE] Browser initialized and ready");
    }

    /// <summary>
    /// Teardown: Stop the development server and dispose browser resources.
    /// Called once after all tests in the collection complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        Console.WriteLine("🧹 [FIXTURE] Cleaning up shared resources...");

        if (Browser != null)
        {
            await Browser.CloseAsync();
            await Browser.DisposeAsync();
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

        Console.WriteLine("✅ [FIXTURE] Cleanup completed");
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
            Console.WriteLine("❌ [FIXTURE] Build failed:");
            foreach (var line in outputLines.Concat(errorLines))
            {
                Console.WriteLine($"  {line}");
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

        _serverProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                _outputLog.Add($"[OUT] {e.Data}");
                Console.WriteLine($"  [SERVER] {e.Data}");
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
                _outputLog.Add($"[ERR] {e.Data}");
                if (!e.Data.Contains("xdg-open", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  [SERVER ERROR] {e.Data}");
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
            Console.WriteLine("❌ [FIXTURE] Server startup timeout. Full output:");
            foreach (var line in _outputLog)
            {
                Console.WriteLine($"  {line}");
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
                    Console.WriteLine($"  ✓ [FIXTURE] Server health check passed (attempt {attempt}/{maxAttempts})");
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
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
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
