# Workflows (CI/CD)


## PR Code Quality Checks (pr-checks.yml) ✅ IMPLEMENTED

**Status**: Production-ready
**Trigger**: Pull requests to main/develop branches, manual dispatch

### Priority Checks (Blocking)

1. **Secret Scanning**: TruffleHog scans for API keys, tokens, credentials
2. **Bundle Size Analysis**: Tracks WASM/JS bundle changes vs base branch (10% threshold)
3. **Cyclomatic Complexity**: Roslyn analyzers (C#) + ESLint SonarJS (JavaScript)
4. **Build Verification**: Debug + Release builds with artifact validation

### Secondary Checks (Warnings)

5. **Code Coverage**: 70% overall, 80% new code (via Codecov)
6. **Integration Tests**: E2E test suite (placeholder, future implementation)
7. **Linting & Formatting**: dotnet-format (C#) + Prettier (JS)
8. **Dead Code Detection**: Unused variables, imports, code blocks

### Configuration

- `.editorconfig`: C# code style rules
- `src/BlazorApp/wwwroot/js/.eslintrc.json`: JavaScript complexity rules
- `codecov.yml`: Coverage thresholds and ignore patterns

### Usage

```bash
# Local testing before PR
cd src/BlazorApp
dotnet build
dotnet format --verify-no-changes

cd wwwroot/js
npm install && npm run lint
```

See `.github/workflows/README.md` for detailed documentation.

## Build & Test (build.yml)

**Status**: Planned
- Trigger on PR and main
- Build Blazor, run tests
- Validate parsing vs reference

## Deploy (deploy.yml)

**Status**: Planned
- Publish to GitHub Pages
- Update Cloudflare Worker

## Agent Tasks (agent-task.yml)

**Status**: Planned
- Label-based triggers
- Implement porting tasks with Python→C# references
- Create PR, run validations

## Agent Task Template

Include source Python file, lines, target C# file, validation steps.