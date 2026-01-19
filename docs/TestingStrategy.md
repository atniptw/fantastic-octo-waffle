# Testing Strategy

## Overview
This document outlines the testing approach for the R.E.P.O. Mod Browser project. Tests are organized by type (unit, integration, E2E) and purpose, with clear execution instructions for developers and CI.

## Test Structure

### Unit Tests
- **Purpose**: Test individual components and parsing logic in isolation
- **Location**: TBD (future implementation)
- **Coverage**:
  - Binary reader
  - PackedBitVector
  - Mesh field parsing
  - Vertex/index extraction

### Integration Tests
- **Purpose**: Test interactions between components
- **Location**: TBD (future implementation)
- **Coverage**:
  - Bundle → mesh extraction
  - ZIP → asset discovery
  - Three.js export

### E2E Tests
- **Purpose**: Verify the entire application works end-to-end in a real browser
- **Location**: `Tests/BlazorApp.E2E.Tests/`
- **Framework**: xUnit + Playwright (Chromium)
- **Current Tests**:
  - `HelloWorldE2ETests.AppLoads_DisplaysHelloWorld_NoConsoleErrors` - Baseline health check

## E2E Test Execution

### Prerequisites
```bash
# Install .NET 10.0 SDK (if not already installed)
# Install Playwright browsers (one-time setup)
cd Tests/BlazorApp.E2E.Tests
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### Running E2E Tests Locally
```bash
# From repository root
dotnet test Tests/BlazorApp.E2E.Tests/

# With detailed output (shows timing metrics)
dotnet test Tests/BlazorApp.E2E.Tests/ --logger "console;verbosity=detailed"

# Run from solution
dotnet test --filter "Category=E2E"
```

### Test Output
The E2E test produces detailed output with timing metrics:
```
🔨 Building Blazor app in Debug configuration...
✅ Build completed successfully (elapsed: 1.94s)
📦 Verifying build artifacts...
  ✓ DLL: /path/to/BlazorApp.dll
  ✓ wwwroot: /path/to/wwwroot
  ✓ _framework: /path/to/_framework
  ✓ Runtime: /path/to/dotnet.js
✅ All build artifacts verified (elapsed: 1.95s)
🚀 Starting development server...
  [SERVER] Now listening on: http://localhost:5000
✅ Development server ready in 2.91s (total elapsed: 4.86s)
🌐 Initializing browser (Chromium)...
✅ Browser initialized (elapsed: 5.33s)
🌐 Navigating to http://localhost:5000...
✅ Page loaded in 2.14s
🔍 Verifying DOM elements...
✅ Found heading: "Hello, world!"
✅ Found paragraph: "Welcome to your new app."
📊 Console messages captured: 2
  [info] Debugging hotkey: Shift+Alt+D (when application has focus)
✅ No console errors detected
✅ Test completed successfully (total elapsed: 7.67s)
```

### What the E2E Test Validates
1. **Build Verification** (Debug configuration)
   - `dotnet build` completes with zero errors
   - Compiled DLL exists (`BlazorApp.dll`)
   - wwwroot directory created
   - _framework directory with Blazor runtime files
   - Blazor runtime JavaScript (`dotnet.js`)

2. **Runtime Verification**
   - Development server starts successfully (`dotnet run`)
   - Server listens on `http://localhost:5000`
   - Server responds to HTTP requests (returns 200 OK)
   - Page loads without timeout (< 15 seconds)

3. **DOM Verification**
   - Heading with text "Hello, world!" is present
   - Paragraph with text "Welcome to your new app." is present

4. **Console Error Detection**
   - No JavaScript errors in browser console
   - Ignores 404 errors for missing resources (e.g., favicon)

5. **Cleanup**
   - Server process terminated cleanly
   - Browser resources disposed
   - No port conflicts after test

## Troubleshooting E2E Tests

### Common Failures

#### Port Already in Use
**Symptom**: `Failed to bind to address http://127.0.0.1:5000: address already in use`

**Solution**:
```bash
# Find and kill process using port 5000
lsof -ti :5000 | xargs kill -9
# Or on Windows: netstat -ano | findstr :5000
```

#### Server Timeout
**Symptom**: `Server failed to start within 30 seconds`

**Possible Causes**:
- Build errors (check build output in test logs)
- Port conflict (see above)
- Missing dependencies (`dotnet restore`)

**Solution**:
```bash
# Verify build works manually
cd src/BlazorApp
dotnet restore
dotnet build
dotnet run  # Should start without errors
```

#### Browser Not Found
**Symptom**: `Executable doesn't exist at /path/to/chromium`

**Solution**:
```bash
# Re-install Playwright browsers
cd Tests/BlazorApp.E2E.Tests
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

#### Certificate Errors (HTTPS)
**Note**: Current tests use HTTP (`http://localhost:5000`) to avoid certificate issues in CI. If switching to HTTPS:
- Update `ServerUrl` in test to `https://localhost:5001`
- Add `--ignore-certificate-errors` to browser launch args

### CI-Specific Issues

#### Headless Browser Failures
- Playwright is configured for headless mode by default
- Check CI logs for browser console errors
- Verify Playwright installation step in CI workflow

#### Build Artifacts Not Found
- Ensure `dotnet restore` and `dotnet build` run before test
- Check working directory in CI (should be repository root)

## Fixtures
- Sample .hhh files (TBD)
- Reference JSON from UnityPy (TBD)
- Known vertex counts/bounds (TBD)

## Validation Approach
Compare C# outputs to Python UnityPy; JSON diff for critical fields. 

## CI Integration
E2E tests run automatically on every PR via `.github/workflows/pr-checks.yml` in the `integration-tests` job. Browser binaries are installed as part of the CI workflow.

### Test Timeout
- Individual test timeout: 60 seconds
- Server startup timeout: 30 seconds
- Page load timeout: 15 seconds

### Running Tests in CI
Tests run with:
```bash
dotnet test --filter "FullyQualifiedName~E2E" --logger "console;verbosity=detailed"
```

## Future Enhancements
- [ ] Add E2E tests for Unity asset parsing (once implemented)
- [ ] Add E2E tests for Three.js rendering (once implemented)
- [ ] Add E2E tests for Thunderstore API integration (once implemented)
- [ ] Performance benchmarking (bundle parse time, render time)
- [ ] Cross-browser testing (Firefox, WebKit)