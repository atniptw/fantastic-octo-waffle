# Testing Strategy

## Overview

Testing is organized into three layers:
1. **Unit tests** for individual functions (binary readers, decompressors, deserializers).
2. **Integration tests** for parsing pipelines (real bundles, golden outputs).
3. **End-to-end tests** for user flows (browse → download → render).

**Tools:**
- **Vitest:** Unit + integration tests; Web Worker support; fast.
- **Playwright:** E2E browser tests; canvas rendering validation.
- **Golden files:** Serialized parse outputs for deterministic regression detection.

---

## Unit Testing

### Binary Readers

**File:** `packages/unity-ab-parser/src/__tests__/readers.test.ts`

Test cases for:
- Reading uint32 (big-endian, alignment boundaries).
- Reading float32 arrays with correct stride.
- String deserialization (null-terminated vs. length-prefixed).
- Bounds checking (reject reads past buffer end).

**Example:**
```typescript
it('reads big-endian uint32', () => {
  const data = new Uint8Array([0x12, 0x34, 0x56, 0x78]);
  expect(readUint32BE(data, 0)).toBe(0x12345678);
});
```

### Decompressors

**File:** `packages/unity-ab-parser/src/__tests__/decompressors.test.ts`

Test cases:
- LZ4 known vectors (from official LZ4 test suite).
- LZMA known vectors.
- Block boundary handling (ensure decoders reset state).
- Error cases (corrupt compressed data).

**Example:**
```typescript
it('decompresses LZ4 with known vector', () => {
  const compressed = new Uint8Array([0x04, 0x22, 0x4D, 0x18, 0x20, ...]);
  const expected = new Uint8Array([0x00, 0x01, 0x02, ...]);
  const result = decompressLZ4(compressed, expectedSize);
  expect(result).toEqual(expected);
});
```

### Object Deserializers

**File:** `packages/unity-ab-parser/src/__tests__/deserializers.test.ts`

Test cases for parsing small synthetic meshes/textures/materials:

```typescript
it('parses minimal mesh', () => {
  const data = createMinimalMeshBuffer();
  const mesh = parseMesh(data, 0);
  expect(mesh.vertices).toHaveLength(9); // 3 vertices * 3 coords
  expect(mesh.normals).toHaveLength(9);
});
```

---

## Integration Testing

### Synthetic Bundle Parsing

**File:** `packages/unity-ab-parser/src/__tests__/integration.test.ts`

Uses synthetic test fixtures; validates end-to-end parse pipeline.

**Fixtures location:** `packages/unity-ab-parser/fixtures/`

**Example fixtures:**
- `synthetic-minimal-v1.hhh` (5 KB, 1 triangle mesh; LZ4 compressed)
- `synthetic-textured-v1.hhh` (50 KB, 1 cube with textures; LZMA compressed)

**Test structure:**
```typescript
it('parses synthetic-minimal-v1', async () => {
  const bundle = await loadFixture('synthetic-minimal-v1.hhh');
  const result = await parseBundle(bundle);
  
  expect(result.meshes).toHaveLength(1);
  expect(result.meshes[0].name).toBe('Triangle');
  expect(result.meshes[0].vertices.length).toBe(9); // 3 vertices * 3 coords
  
  // Compare against golden output
  const golden = loadGolden('synthetic-minimal-v1.golden.json');
  expect(result).toEqual(golden);
});
```

**Golden file format** (`synthetic-minimal-v1.golden.json`):
```json
{
  "meshes": [
    {
      "name": "Triangle",
      "vertexCount": 3,
      "indexCount": 3,
      "submeshes": [{ "indexStart": 0, "indexCount": 3 }]
    }
  ],
  "textures": [],
  "materials": [
    {
      "name": "WhiteMaterial",
      "shader": "Standard"
    }
  ]
}
```

### Web Worker Integration

**File:** `packages/unity-ab-parser/src/__tests__/worker.test.ts`

Test parsing offloaded to Web Worker (via Vitest Web Worker environment):

```typescript
it('parses bundle in Web Worker', async () => {
  const worker = new Worker('./parser-worker.ts', { type: 'module' });
  
  const result = await new Promise((resolve) => {
    worker.onmessage = (e) => resolve(e.data);
    worker.postMessage({ action: 'parse', buffer: bundleArrayBuffer });
  });
  
  expect(result.meshes.length).toBeGreaterThan(0);
  worker.terminate();
});
```

---

## End-to-End Testing

### User Flow: Browse → Download → Render

**File:** `apps/web/e2e/smoke.test.ts` (Playwright)

```typescript
test('browse mod list and render cosmetic', async ({ page }) => {
  await page.goto('http://localhost:5173');
  
  // Wait for mod list to load
  await page.waitForSelector('[data-test=mod-card]', { timeout: 5000 });
  expect(page.locator('[data-test=mod-card]')).toBeDefined();
  
  // Click first mod
  await page.click('[data-test=mod-card]:first-child');
  
  // Wait for version selector
  await page.waitForSelector('[data-test=version-select]', { timeout: 5000 });
  
  // Click a version
  await page.click('[data-test=version-select] option:nth-child(1)');
  
  // Wait for canvas to render
  await page.waitForSelector('canvas', { timeout: 10000 });
  
  // Verify canvas is drawing
  const canvas = await page.locator('canvas').boundingBox();
  expect(canvas.width).toBeGreaterThan(100);
  expect(canvas.height).toBeGreaterThan(100);
});
```

### Worker Proxy Tests

**File:** `apps/worker/__tests__/proxy.test.ts` (Miniflare)

```typescript
import { env } from 'miniflare';

test('proxies API request to Thunderstore', async () => {
  const response = await env.fetch(
    'http://localhost:8787/api/mods?community=repo&page=1'
  );
  
  expect(response.status).toBe(200);
  expect(response.headers.get('access-control-allow-origin')).toBe('*');
  
  const json = await response.json();
  expect(json.results).toBeDefined();
  expect(json.results.length).toBeGreaterThan(0);
});

test('blocks disallowed hosts', async () => {
  const response = await env.fetch(
    'http://localhost:8787/proxy?url=https://evil.com/file.zip'
  );
  
  expect(response.status).toBe(400);
  const json = await response.json();
  expect(json.error).toBe('invalid_url');
});
```

---

## Test Fixtures

### Storage & Versioning

**Location:** `packages/unity-ab-parser/fixtures/`

**Policy:**
- Use **synthetic fixtures** created with Unity 2022.3.x to avoid licensing issues.
- Store bundles using **Git LFS** to avoid bloating the repository.
- Keep bundles < 10 MB each (compress with bzip2 if larger).
- Document creation method, compression type, and SHA256 in `FIXTURES.md`:
  ```markdown
  ## synthetic-minimal-v1.hhh
  - Source: Created with Unity 2022.3.x for testing
  - Version: 1.0.0
  - License: CC0 (public domain)
  - SHA256: abc123...
  - Size: 5 KB
  - Compression: LZ4
  - Description: Minimal triangle mesh for parser validation.
  ```

### Golden Files

**Location:** `packages/unity-ab-parser/fixtures/goldens/`

**Format:** JSON with stable, minimal schema:
```json
{
  "bundleName": "synthetic-minimal-v1",
  "meshes": [
    {
      "name": "Triangle",
      "vertexCount": 3,
      "indexCount": 3
    }
  ],
  "textures": [],
  "materials": [
    {
      "name": "WhiteMaterial",
      "shader": "Standard"
    }
  ]
}
```

**Regeneration:**
- If parser logic changes and tests fail, manually inspect the new parse output.
- If correct, update golden files:
  ```bash
  npm run test:update-goldens
  ```
- Commit golden file changes with description of parser improvements.

---

## CI/CD Pipeline

### GitHub Actions Workflow

**File:** `.github/workflows/test.yml`

```yaml
name: Test & Lint

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: pnpm/action-setup@v2
      - uses: actions/setup-node@v3
        with:
          node-version: '24'
          cache: 'pnpm'
      
      - run: pnpm install
      - run: pnpm lint
      - run: pnpm test:unit
      - run: pnpm test:integration
      - run: pnpm test:e2e --trace on
      
      - name: Upload Playwright report
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report
          path: playwright-report/
```

### Build & Deploy

**Web app:**
```bash
cd apps/web
pnpm build        # → dist/
pnpm preview      # test prod build locally
# Deploy dist/ to static hosting (e.g., Netlify, Vercel)
```

**Worker:**
```bash
cd apps/worker
wrangler deploy    # → deployed to Cloudflare
```

### Test Coverage

Target: **80%+ coverage** for parser logic.

**Report generation:**
```bash
pnpm test:cov
# → coverage/index.html
```

---

## Acceptance Criteria (Testing)

- [ ] All unit tests pass locally and in CI.
- [ ] Golden files for fixture bundles are stable across runs.
- [ ] E2E test verifies full flow (list → download → parse → render) in < 15s.
- [ ] Worker tests validate proxy routing, allowlist, and CORS headers.
- [ ] Coverage report available; parser packages > 80%.
- [ ] Playwright traces capture any failures for debugging.
- [ ] Synthetic fixtures documented with creation method/compression/hash in `FIXTURES.md`.
- [ ] Git LFS configured for `.hhh` files to avoid repository bloat.

---

## Running Tests Locally

```bash
# Install dependencies
pnpm install

# Run all tests
pnpm test

# Run specific suite
pnpm test:unit
pnpm test:integration
pnpm test:e2e

# Watch mode
pnpm test:watch

# Update golden files
pnpm test:update-goldens

# Generate coverage report
pnpm test:cov
```


