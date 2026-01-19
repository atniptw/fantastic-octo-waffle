# Blazor App E2E Tests

End-to-end tests for the R.E.P.O. Mod Browser Blazor WebAssembly application using xUnit and Playwright.

## Purpose

These tests serve as a **health check** to ensure the Blazor app is always in a runnable state. Before adding new features, we need confidence that the baseline application builds successfully and loads without errors.

## What These Tests Verify

### Build Verification (Debug Configuration)
- `dotnet build` completes with zero errors
- Compiled DLL exists (`BlazorApp.dll`)
- wwwroot directory created with Blazor framework files
- Blazor runtime JavaScript (`dotnet.js`) present

### Runtime Verification
- Development server starts successfully (`dotnet run`)
- Server listens on `http://localhost:5000`
- Server responds to HTTP requests (200 OK)
- Page loads within 15 seconds

### DOM Verification
- Heading with text "Hello, world!" is present
- Paragraph with text "Welcome to your new app." is present

### Console Error Detection
- No JavaScript errors in browser console
- Ignores 404 errors for missing resources (e.g., favicon)

### Cleanup
- Server process terminated cleanly
- Browser resources disposed
- No port conflicts after test

## Prerequisites

### One-Time Setup
```bash
# Install Playwright browsers (required for first run)
cd Tests/BlazorApp.E2E.Tests
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## Running Tests

### From This Directory
```bash
# Standard run
dotnet test

# With detailed output (recommended for debugging)
dotnet test --logger "console;verbosity=detailed"
```

### From Repository Root
```bash
# Run E2E tests only
dotnet test Tests/BlazorApp.E2E.Tests/

# Run all tests in solution
dotnet test

# Filter by name
dotnet test --filter "FullyQualifiedName~E2E"
```

## Test Output Example

```
🔨 Building Blazor app in Debug configuration...
✅ Build completed successfully (elapsed: 1.85s)
📦 Verifying build artifacts...
  ✓ DLL: .../BlazorApp.dll
  ✓ wwwroot: .../wwwroot
  ✓ _framework: .../_framework
  ✓ Runtime: .../dotnet.js
✅ All build artifacts verified (elapsed: 1.86s)
🚀 Starting development server...
  [SERVER] Now listening on: http://localhost:5000
✅ Development server ready in 2.88s (total elapsed: 4.74s)
🌐 Initializing browser (Chromium)...
✅ Browser initialized (elapsed: 5.16s)
🌐 Navigating to http://localhost:5000...
✅ Page loaded in 1.35s
🔍 Verifying DOM elements...
✅ Found heading: "Hello, world!"
✅ Found paragraph: "Welcome to your new app."
📊 Console messages captured: 2
  [info] Debugging hotkey: Shift+Alt+D (when application has focus)
✅ No console errors detected
✅ Test completed successfully (total elapsed: 6.58s)
🧹 Cleaning up resources...
✅ Cleanup completed (total test time: 6.71s)

Test Run Successful.
Total tests: 1
     Passed: 1
 Total time: 7.2956 Seconds
```

## Timeouts

- **Individual test timeout**: 60 seconds
- **Server startup timeout**: 30 seconds
- **Page load timeout**: 15 seconds

## Troubleshooting

### Port Already in Use

**Symptom**: `Failed to bind to address http://127.0.0.1:5000: address already in use`

**Solution**:
```bash
# Linux/macOS
lsof -ti :5000 | xargs kill -9

# Windows PowerShell
Get-Process -Id (Get-NetTCPConnection -LocalPort 5000).OwningProcess | Stop-Process -Force
```

### Server Timeout

**Symptom**: `Server failed to start within 30 seconds`

**Possible Causes**:
- Build errors (check test output for compiler errors)
- Port conflict (see above)
- Missing dependencies

**Solution**:
```bash
# Verify build works manually
cd ../../src/BlazorApp
dotnet restore
dotnet build
dotnet run  # Should start without errors (Ctrl+C to stop)
```

### Browser Not Found

**Symptom**: `Executable doesn't exist at /path/to/chromium`

**Solution**:
```bash
# Re-install Playwright browsers
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### Test Hangs on CI

- Verify Playwright installation step runs in CI workflow
- Check for port conflicts (previous test didn't clean up)
- Increase timeouts if CI is slow

## CI Integration

These tests run automatically on every PR via `.github/workflows/pr-checks.yml` in the `integration-tests` job:

```yaml
- name: Install Playwright browsers
  run: pwsh bin/Debug/net10.0/playwright.ps1 install chromium

- name: Run E2E Tests
  run: dotnet test --no-build --logger "console;verbosity=detailed"
```

## Architecture

### Test Structure
- **Test Framework**: xUnit
- **Browser Automation**: Playwright (Chromium)
- **Test Lifecycle**: `IAsyncLifetime` for setup/teardown
- **Server**: `dotnet run` (not `dotnet watch` to avoid file watcher issues)

### Key Components

1. **`InitializeAsync()`**: Runs once before test
   - Builds the Blazor app
   - Verifies build artifacts
   - Starts development server
   - Initializes Playwright browser

2. **`AppLoads_DisplaysHelloWorld_NoConsoleErrors()`**: The test method
   - Navigates to app URL
   - Waits for page load
   - Verifies DOM elements
   - Checks for console errors

3. **`DisposeAsync()`**: Runs once after test
   - Closes browser
   - Stops server process
   - Cleans up resources

## Future Enhancements

As features are added to the app, create new test files/methods:
- `UnityAssetParsingE2ETests.cs` - Test asset parsing flow
- `ThunderstoreIntegrationE2ETests.cs` - Test API integration
- `MeshRenderingE2ETests.cs` - Test Three.js rendering

Keep this test file focused on the **Hello World baseline only**.

## Notes

- **Do NOT modify** the Hello World app just to make tests more complex
- **Do NOT add features** to the app within this test scope
- **Focus**: Verify existing app runs, nothing more
- Test is **deterministic** - no flakiness, race conditions, or random failures
- Test output includes **timing metrics** for performance monitoring
