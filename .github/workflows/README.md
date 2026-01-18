# GitHub Actions Workflows

This directory contains automated CI/CD workflows for the R.E.P.O. Mod Browser project.

## Workflows

### PR Code Quality Checks (`pr-checks.yml`)

Comprehensive code quality workflow that runs on all pull requests to enforce strict standards.

#### Priority Checks (Must Pass)

1. **🔒 Secret & Credential Scanning** (Non-negotiable)
   - Tool: TruffleHog
   - Scans for API keys, tokens, and secrets in code
   - Blocks merge if secrets detected
   - Runs on full PR history

2. **📦 Bundle Size Analysis** (Performance Gate)
   - Tracks WASM, JS, and total payload changes
   - Fails if WASM bundle grows >10% (configurable via `BUNDLE_SIZE_THRESHOLD_PERCENT`)
   - Compares PR against base branch
   - Provides detailed size reports in PR summary

3. **🧮 Cyclomatic Complexity Check** (AI Maintainability)
   - C# Analysis: Roslyn analyzers + SonarAnalyzer
   - JavaScript Analysis: ESLint with sonarjs plugin
   - Max complexity per function: 10 (strict for AI-generated code)
   - Enforces code style and best practices

4. **🔨 Build Verification** (System Integrity)
   - Builds in both Debug and Release configurations
   - Verifies all critical build artifacts are present
   - Ensures project compiles successfully

#### Secondary Checks (Warnings Only)

5. **📊 Code Coverage Analysis**
   - Minimum 70% overall coverage (when tests exist)
   - Minimum 80% for new code only
   - Integrates with Codecov
   - Skips gracefully if no test projects found

6. **🔗 Integration & E2E Tests**
   - Tests critical flows:
     - Bundle parsing → Three.js render pipeline
     - Thunderstore API client with mock responses
     - Mesh geometry extraction and validation
   - Placeholder for future test implementation

7. **🎨 Linting & Formatting**
   - C#: dotnet-format
   - JavaScript: Prettier
   - Reports formatting issues as warnings

8. **🧹 Dead Code Detection**
   - Detects unused variables, imports, and code
   - Warns on CS0168, CS0219, CS8019

#### Configuration

Environment variables can be customized at the workflow level:

```yaml
env:
  DOTNET_VERSION: '10.0.x'
  NODE_VERSION: '20.x'
  BUNDLE_SIZE_THRESHOLD_PERCENT: 10
```

#### Required Secrets

- `CODECOV_TOKEN` (optional): For uploading coverage reports to Codecov

#### Manual Trigger

The workflow can be manually triggered from the GitHub Actions UI using the "Run workflow" button.

#### Status Checks

The `pr-checks-summary` job provides a comprehensive status report:
- ✅ All checks passed: PR ready for review
- ❌ Critical checks failed: Must fix before merging
- ⚠️ Warnings: Review recommended but not blocking

#### Local Testing

To test these checks locally before pushing:

```bash
# Build and test
cd src/BlazorApp
dotnet build
dotnet test

# Check formatting
dotnet format --verify-no-changes

# JavaScript linting
cd wwwroot/js
npm install
npm run lint
npm run format:check
```

## Future Workflows

Additional workflows planned (see docs/Workflow.md):
- `build.yml`: Main CI build and test workflow
- `deploy.yml`: GitHub Pages and Cloudflare Worker deployment
- `agent-task.yml`: Automated agent tasks for porting work

## Contributing

When adding new workflows:
1. Use meaningful job names with emojis for easy scanning
2. Add `continue-on-error: true` for non-critical checks
3. Write results to `$GITHUB_STEP_SUMMARY` for PR visibility
4. Update this README with workflow documentation
5. Test workflows on a feature branch before merging to main
