# Testing Strategy and Conventions

This document outlines the testing approach, conventions, and best practices for the fantastic-octo-waffle project.

## Table of Contents

1. [Testing Philosophy](#testing-philosophy)
2. [Test Types](#test-types)
3. [Directory Structure](#directory-structure)
4. [Running Tests](#running-tests)
5. [Writing Tests](#writing-tests)
6. [Coverage Requirements](#coverage-requirements)
7. [CI/CD Integration](#cicd-integration)

---

## Testing Philosophy

Our testing strategy follows the testing pyramid:

- **Unit Tests**: Most numerous, fast, test individual functions/modules in isolation
- **Integration Tests**: Moderate number, test interactions between components
- **E2E Tests**: Fewer, slower, test complete user flows through the application

All code changes should include appropriate tests. Tests serve as documentation and prevent regressions.

---

## Test Types

### Unit Tests

**Purpose**: Test individual functions, classes, or modules in isolation.

**Framework**: [Vitest](https://vitest.dev/)

**Location**: Co-located with source files (e.g., `src/utils.ts` → `src/utils.test.ts`)

**Scope**:
- Pure functions and business logic
- Utility functions
- Type transformations
- Error handling

**Example**:
```typescript
import { describe, it, expect } from 'vitest';
import { parseUnityFSHeader } from './parser';

describe('parseUnityFSHeader', () => {
  it('should parse valid UnityFS header', () => {
    const buffer = createMockHeader();
    const result = parseUnityFSHeader(buffer);
    expect(result.signature).toBe('UnityFS');
    expect(result.version).toBe(6);
  });
  
  it('should throw on invalid signature', () => {
    const buffer = createInvalidHeader();
    expect(() => parseUnityFSHeader(buffer)).toThrow('Invalid signature');
  });
});
```

### Integration Tests

**Purpose**: Test interactions between multiple components or modules.

**Framework**: Vitest

**Location**: `src/**/*.integration.test.ts` or `__tests__/integration/`

**Scope**:
- API endpoint handlers (Worker)
- Parser pipeline (multiple decompression stages)
- Renderer pipeline (mesh → three.js conversion)
- External API interactions (with mocking)

**Example**:
```typescript
import { describe, it, expect, vi } from 'vitest';

describe('Worker Proxy Endpoint', () => {
  it('should proxy allowed URLs with correct CORS headers', async () => {
    const request = new Request('https://example.com/proxy?url=...');
    const response = await handleProxyRequest(request);
    
    expect(response.status).toBe(200);
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
  });
  
  it('should reject non-allowlisted URLs', async () => {
    const request = new Request('https://example.com/proxy?url=evil.com');
    const response = await handleProxyRequest(request);
    
    expect(response.status).toBe(403);
  });
});
```

### E2E (End-to-End) Tests

**Purpose**: Test complete user workflows from start to finish.

**Framework**: [Playwright](https://playwright.dev/)

**Location**: `apps/web/e2e/`

**Scope**:
- User flows (browse mods → select → view 3D)
- UI interactions
- Cross-browser compatibility
- Performance and accessibility

**Example**:
```typescript
import { test, expect } from '@playwright/test';

test('user can browse and view cosmetic', async ({ page }) => {
  await page.goto('/');
  
  // Wait for mod list to load
  await page.waitForSelector('[data-testid="mod-list"]');
  
  // Select first cosmetic mod
  await page.click('[data-testid="mod-item"]:first-child');
  
  // Verify 3D viewer loads
  await expect(page.locator('[data-testid="three-canvas"]')).toBeVisible();
  
  // Verify cosmetic renders
  await page.waitForSelector('[data-testid="mesh-loaded"]');
});
```

---

## Directory Structure

```
fantastic-octo-waffle/
├── apps/
│   ├── web/
│   │   ├── src/
│   │   │   ├── components/
│   │   │   │   ├── ModList.tsx
│   │   │   │   └── ModList.test.ts          # Unit tests
│   │   │   └── index.ts
│   │   ├── e2e/                              # E2E tests
│   │   │   ├── app.spec.ts
│   │   │   └── viewer.spec.ts
│   │   ├── playwright.config.ts
│   │   └── vitest.config.ts
│   └── worker/
│       ├── src/
│       │   ├── index.ts
│       │   ├── index.test.ts                 # Unit tests
│       │   └── index.integration.test.ts     # Integration tests
│       ├── vitest.config.ts
│       └── vitest.integration.config.ts
├── packages/
│   ├── unity-ab-parser/
│   │   ├── src/
│   │   │   ├── parser.ts
│   │   │   ├── parser.test.ts                # Unit tests
│   │   │   └── __tests__/
│   │   │       ├── fixtures/                 # Golden files
│   │   │       └── integration/              # Integration tests
│   │   └── vitest.config.ts
│   └── ...
├── docs/
│   └── testing-strategy.md                   # This file
└── vitest.config.ts                          # Root config
```

---

## Running Tests

### All Tests

```bash
# Run all test types across all packages
pnpm test

# Run all unit tests
pnpm test:unit

# Run all integration tests
pnpm test:integration

# Run E2E tests
pnpm test:e2e
```

### Specific Package

```bash
# Run tests in a specific package
pnpm --filter @fantastic-octo-waffle/web test

# Run tests in watch mode
pnpm --filter @fantastic-octo-waffle/unity-ab-parser test:watch
```

### Coverage

```bash
# Generate coverage report
pnpm test:cov

# Coverage reports are output to coverage/ directory
```

### Playwright-Specific Commands

```bash
# Run E2E tests
cd apps/web
pnpm test:e2e

# Run E2E tests with UI (interactive mode)
pnpm test:e2e:ui

# Run E2E tests in headed mode (see browser)
pnpm test:e2e:headed

# Run specific test file
pnpm playwright test e2e/viewer.spec.ts

# Run tests on specific browser
pnpm playwright test --project=chromium

# Debug tests
pnpm playwright test --debug
```

---

## Writing Tests

### Best Practices

#### General

1. **Arrange-Act-Assert (AAA)**: Structure tests clearly
   ```typescript
   it('should do something', () => {
     // Arrange: Set up test data
     const input = createTestData();
     
     // Act: Execute the code under test
     const result = functionToTest(input);
     
     // Assert: Verify expectations
     expect(result).toEqual(expectedOutput);
   });
   ```

2. **One assertion per test** (when possible): Makes failures easier to diagnose

3. **Descriptive test names**: Use "should" statements
   ```typescript
   // Good
   it('should throw error when buffer is too small', () => { ... });
   
   // Bad
   it('test parser', () => { ... });
   ```

4. **Test edge cases**: Empty inputs, null, undefined, boundary values

5. **Don't test implementation details**: Test behavior, not internals

#### Unit Tests

- **Mock external dependencies**: Use `vi.mock()` from Vitest
- **Keep tests fast**: Avoid I/O, network calls, heavy computation
- **Test pure functions**: Easier to test and reason about
- **Use fixtures for binary data**: Store golden files in `__tests__/fixtures/`

```typescript
import { describe, it, expect, vi } from 'vitest';

describe('decompressBlock', () => {
  it('should decompress LZ4 block', () => {
    const compressed = loadFixture('lz4-block.bin');
    const result = decompressBlock(0x40, compressed);
    expect(result.byteLength).toBe(expectedSize);
  });
});
```

#### Integration Tests

- **Test component interactions**: How modules work together
- **Mock external services**: Thunderstore API, file downloads
- **Use realistic test data**: Representative of production
- **Verify error handling**: Network failures, invalid responses

```typescript
describe('Parser Pipeline Integration', () => {
  it('should parse complete AssetBundle', async () => {
    const bundle = await loadTestBundle('cosmetic.hhh');
    const result = await parseAssetBundle(bundle);
    
    expect(result.meshes).toHaveLength(1);
    expect(result.textures).toHaveLength(2);
    expect(result.materials).toHaveLength(1);
  });
});
```

#### E2E Tests

- **Test user flows**: Complete scenarios
- **Use data-testid attributes**: For reliable selectors
- **Wait for async operations**: Use `waitForSelector`, `waitForLoadState`
- **Test on multiple browsers**: Chromium, Firefox, WebKit
- **Keep tests independent**: Each test should run in isolation
- **Use Page Object Model (POM)**: For maintainability

```typescript
// Good: Use data-testid
await page.click('[data-testid="view-cosmetic-btn"]');

// Bad: Brittle selectors
await page.click('.btn.primary:nth-child(2)');
```

### Mocking

#### Vitest Mocks

```typescript
// Mock module
vi.mock('@fantastic-octo-waffle/thunderstore-client', () => ({
  fetchModList: vi.fn().mockResolvedValue(mockMods),
}));

// Mock function
const mockFetch = vi.fn().mockResolvedValue({
  ok: true,
  json: async () => mockData,
});
global.fetch = mockFetch;

// Restore
vi.restoreAllMocks();
```

#### Playwright Network Mocking

```typescript
test('should handle API failure gracefully', async ({ page }) => {
  // Mock API failure
  await page.route('**/api/mods', route => {
    route.fulfill({
      status: 500,
      body: 'Internal Server Error',
    });
  });
  
  await page.goto('/');
  await expect(page.locator('[data-testid="error-message"]')).toBeVisible();
});
```

### Test Data and Fixtures

- **Golden files**: Store binary test data in `__tests__/fixtures/`
- **Mock data**: Create realistic but minimal test data
- **Factories**: Use factory functions for complex objects

```typescript
// Factory pattern
function createMockMesh(overrides = {}) {
  return {
    vertices: new Float32Array([0, 0, 0]),
    normals: new Float32Array([0, 1, 0]),
    triangles: new Uint32Array([0, 1, 2]),
    ...overrides,
  };
}
```

---

## Coverage Requirements

### Targets

- **Overall coverage**: 80% minimum
- **Critical paths**: 95% minimum (parser, security, data handling)
- **New code**: 90% minimum

### Measuring Coverage

```bash
pnpm test:cov
```

Coverage reports are generated in `coverage/` directories:
- HTML report: `coverage/index.html`
- JSON report: `coverage/coverage-final.json`

### Coverage Configuration

Coverage settings are in `vitest.config.ts`:

```typescript
coverage: {
  provider: 'v8',
  reporter: ['text', 'json', 'html'],
  exclude: [
    'node_modules/',
    'dist/',
    'coverage/',
    '**/*.config.{ts,js}',
  ],
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

Tests run automatically on:
- Pull requests to `main`
- Pushes to `main`

See `.github/workflows/ci.yml` for configuration.

### CI Test Steps

1. **Unit tests**: `pnpm test:unit`
2. **Integration tests**: `pnpm test:integration`
3. **E2E tests**: `pnpm test:e2e` (Playwright)
4. **Coverage report**: Generated and uploaded as artifact

### Local CI Simulation

```bash
# Simulate CI locally
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test:unit
pnpm test:integration
pnpm test:e2e
```

### Playwright CI Setup

Playwright browsers are installed in CI:

```yaml
- name: Install Playwright browsers
  run: pnpm --filter @fantastic-octo-waffle/web exec playwright install --with-deps
```

---

## Troubleshooting

### Common Issues

#### Playwright browser not found

```bash
# Install browsers
cd apps/web
pnpm exec playwright install
```

#### Tests timing out

- Increase timeout in config
- Check for unresolved promises
- Ensure cleanup in `afterEach`

#### Flaky E2E tests

- Add explicit waits (`waitForSelector`)
- Use `toHaveText()` instead of `toBe()` for async content
- Avoid hardcoded delays (`page.waitForTimeout()`)
- Run tests multiple times: `pnpm playwright test --repeat-each=3`

#### Coverage not matching expectations

- Check `exclude` patterns in `vitest.config.ts`
- Ensure all code paths are tested
- Use `// istanbul ignore next` sparingly

---

## Resources

- [Vitest Documentation](https://vitest.dev/)
- [Playwright Documentation](https://playwright.dev/)
- [Testing Best Practices](https://testingjavascript.com/)
- [Testing Library Principles](https://testing-library.com/docs/guiding-principles)

---

## Maintenance

This document should be updated when:
- New test types are added
- Testing tools or frameworks change
- Coverage requirements change
- CI/CD pipeline changes
