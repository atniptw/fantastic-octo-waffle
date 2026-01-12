# CI/CD Pipeline Documentation

## Overview

This project uses GitHub Actions for continuous integration. The CI pipeline ensures code quality, runs tests, and validates builds on every push and pull request to the `main` branch.

## Goals

- Fast feedback on PRs (lint, type, unit/integration tests)
- Catch dead/unused code early with ts-prune
- Keep build reproducible (Node 24 LTS, pnpm)
- Fail on TypeScript and Vite warnings to maintain code quality
- Automated quality checks before merge

## Current Implementation (Phase 0)

### Workflow: Test & Lint

**File:** `.github/workflows/ci.yml`

**Triggers:**
- Push to `main` branch
- Pull requests targeting `main` branch

**Node Version:** 24.x

**Steps:**

1. **Checkout code** - Clone the repository
2. **Setup pnpm** - Install pnpm 10.28.0
3. **Setup Node.js** - Install Node.js 24.x with pnpm caching
4. **Install dependencies** - Run `pnpm install --frozen-lockfile`
5. **Format check** - Run `pnpm format:check` (Prettier validation)
6. **Lint** - Run `pnpm lint` (ESLint)
7. **Type check** - Run `pnpm typecheck` (TypeScript compilation check)
8. **Dead code detection** - Run `pnpm deadcode` (ts-prune with --error flag)
9. **Unit tests** - Run `pnpm test:unit` (Vitest)
10. **Integration tests** - Run `pnpm test:integration` (continues on error if not implemented)
11. **Build** - Run `pnpm build` (continues on error during Phase 0 setup)
12. **E2E placeholder** - Skipped step with Phase 1 reference comment

## Build Configuration

### TypeScript (Warnings as Errors)

All `tsconfig.json` files include `noEmitOnError: true`, which fails the build on any TypeScript errors or warnings. Combined with strict mode settings:

```json
{
  "compilerOptions": {
    "strict": true,
    "noEmitOnError": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true
  }
}
```

### Vite (Warnings as Errors)

`apps/web/vite.config.ts` is configured to treat Rollup warnings as errors:

```typescript
export default defineConfig({
  build: {
    rollupOptions: {
      onwarn(warning, warn) {
        // Treat warnings as errors in CI
        throw new Error(warning.message);
      },
    },
  },
});
```

## Dead Code Detection Strategy

### Phase 0 (Current): ts-prune

**Tool:** `ts-prune` v0.10.3

**Usage:**
```bash
pnpm deadcode  # Runs: ts-prune --error
```

**Features:**
- Detects unused exports across the codebase
- Fast execution with minimal configuration
- Fails CI on any unused exports (--error flag)
- Simple setup, perfect for Phase 0

**Ignoring False Positives:**
Add `// ts-prune-ignore-next` comment above exports that are intentionally unused (e.g., public API exports).

### Phase 1+ (Future): knip

**Tool:** `knip` (to be added)

**Planned Features:**
- Unused files detection
- Unused dependencies in package.json
- Unused imports (not just exports)
- Duplicate exports
- More comprehensive analysis

**Why Later:**
- Requires tuning for monorepo entrypoints
- More complex configuration needed
- Best added once build structure is stable

## Caching Strategy

The workflow uses GitHub Actions caching:
- **Cache Type:** pnpm store (via `setup-node` action)
- **Cache Key:** Based on `pnpm-lock.yaml`
- **Benefits:**
  - Faster dependency installation (30-60s → 10-20s)
  - Reduced network usage
  - More reliable builds

## Branch Protection (Manual Setup Required)

⚠️ **Post-Merge Action Required:**

After this PR is merged, configure branch protection in GitHub repository settings:

1. Navigate to: **Settings → Branches → Add rule**
2. Branch name pattern: `main`
3. Enable: **"Require status checks to pass before merging"**
4. Select required check: **"Test & Lint"**
5. Recommended settings:
   - ✅ Require pull request reviews (1 reviewer)
   - ✅ Require linear history
   - ✅ Include administrators

**Note:** Branch protection rules cannot be automated via workflow files and must be manually configured.

## Local Development

Run the same checks locally before pushing:

```bash
# Format code
pnpm format

# Check formatting
pnpm format:check

# Lint code
pnpm lint

# Fix linting issues
pnpm lint:fix

# Type check
pnpm typecheck

# Check for dead code
pnpm deadcode

# Run unit tests
pnpm test:unit

# Run integration tests
pnpm test:integration

# Build
pnpm build
```

## Troubleshooting

### Format Check Failures
```bash
pnpm format  # Auto-fix all formatting issues
```

### Lint Failures
```bash
pnpm lint:fix  # Auto-fix simple issues
# Review and manually fix remaining issues
```

### Type Check Failures
- Fix TypeScript errors in the reported files
- Ensure all types are properly imported/exported
- Check for typos in property names

### Dead Code Detection
```bash
pnpm deadcode  # See unused exports
# Remove unused exports OR add ts-prune-ignore comment if intentional
```

### Build Failures
- Ensure all entry points exist (e.g., index.html for web app)
- Check that all dependencies are installed
- Review error messages for missing files or type errors

## Status Badge

Add this badge to your README to show CI status:

```markdown
[![Test & Lint](https://github.com/atniptw/fantastic-octo-waffle/actions/workflows/ci.yml/badge.svg)](https://github.com/atniptw/fantastic-octo-waffle/actions/workflows/ci.yml)
```

## Out of Scope for Phase 0

The following items are **deferred to Phase 1** or later:

- ❌ GitHub Pages auto-deploy
- ❌ Cloudflare Worker deployment
- ❌ E2E testing implementation (Playwright)
- ❌ knip dead code detection
- ❌ Code coverage reporting
- ❌ Performance benchmarking
- ❌ Visual regression testing

## Future Enhancements (Phase 1+)

### Deploy Workflows

**GitHub Pages (apps/web):**
- Trigger: Push to `main`
- Build web app and deploy to GitHub Pages
- Use `actions/deploy-pages` action

**Cloudflare Worker (apps/worker):**
- Trigger: Manual dispatch or tag
- Deploy worker with wrangler
- Requires secrets: `CF_API_TOKEN`, `CF_ACCOUNT_ID`

### E2E Testing (Playwright)

- Install Playwright browsers (cached)
- Run `pnpm test:e2e`
- Upload trace artifacts on failure
- Required check for main branch merges

### Advanced Dead Code Detection

- Add `knip` for comprehensive analysis
- Configure for monorepo structure
- Detect unused files and dependencies

### Coverage & Quality Gates

- Generate coverage reports with Vitest
- Enforce minimum coverage thresholds
- Upload coverage to external services (Codecov, Coveralls)

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [pnpm CI Guide](https://pnpm.io/continuous-integration)
- [TypeScript Compiler Options](https://www.typescriptlang.org/tsconfig)
- [Vite Build Configuration](https://vitejs.dev/config/build-options.html)
- [ts-prune Documentation](https://github.com/nadeesha/ts-prune)
- [Branch Protection Rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
