# Test Fixtures

This document describes the test fixtures used for integration and E2E testing.

## Policy

- **Location:** `packages/unity-ab-parser/fixtures/`
- **Primary approach:** **Synthetic fixtures** (self-created minimal Unity bundles) to avoid licensing issues.
- **Real mods:** Only include with explicit permission from mod authors; document approval below.
- **Size limit:** Individual bundles should be < 10 MB (compress with bzip2 if larger).
- **Naming:** Use descriptive names: `synthetic-{purpose}-{version}.hhh` or `{namespace}-{modname}-{version}.hhh`
- **Provenance:** Document creation method or source URL, license, and SHA256 hash below.
- **Golden files:** Stored in `fixtures/goldens/` with matching name + `.golden.json` suffix.

## Csynthetic-minimal-v1.hhh

- **Source:** Created with Unity 2022.3.x for testing
- **Version:** 1.0.0
- **License:** CC0 (public domain)
- **SHA256:** (to be filled when created in Phase 2)
- **Size:** ~5 KB
- **Compression:** LZ4
- **Description:** Minimal valid UnityFS bundle with one triangle mesh and one solid color material. Tests basic parser flow.
- **Expected parse output:**
  - Meshes: 1 (Triangle, 3 vertices)
  - Textures: 0
  - Materials: 1 (white solid material)

### synthetic-textured-v1.hhh

- **Source:** Created with Unity 2022.3.x for testing
- **Version:** 1.0.0
- **License:** CC0 (public domain)
- **SHA256:** (to be filled when created in Phase 2)
- **Size:** ~50 KB
- **Compression:** LZMA
- **Description:** Cube mesh with PNG base color texture and normal map. Tests texture deserialization and material mapping.
- **Expected parse output:**
  - Meshes: 1 (Cube, 24 vertices)
  - Textures: 2 (BaseColor 256x256 PNG, Normal 256x256 PNG)
  - Materials: 1 (Standard shader with maps)

### Real Mod Fixtures (Optional, with permission)

**If including real mods:**
1. Contact mod author via Thunderstore or GitHub.
2. Request permission to include in test suite with attribution.
3. Document approval (link to conversation, email, etc.).
4. Include proper attribution in fixture metadata.

## Adding New Fixtures

To add a new synthetic fixture:

1. Create the fixture in Unity 2022.3.x (see "Creating Synthetic Fixtures" above).
2. Export as `.hhh` AssetBundle.
3. Name following policy: `synthetic-{purpose}-{version}.hhh`
4. Calculate SHA256:
   ```bash
   sha256sum synthetic-{purpose}-{version}.hhh
   ```
5. Add entry to this file with all metadata.
6. Run parser integration test to generate golden file:
   ```bash
   pnpm test:integration -- synthetic-{purpose}-{version}
   pnpm test:update-goldens
   ```
7. Commit both `.hhh` (via Git LFS) and `.golden.json` to repo.

## Storage & Performance

**Binary storage:** Use Git LFS to avoid bloating the repository.

**Setup (one-time):**
```bash
git lfs install
cd packages/unity-ab-parser
git lfs track "fixtures/*.hhh"
git add .gitattributes
git commit -m "chore: enable git lfs for fixtures"
```

After setup, commit `.hhh` files normally; git automatically stores them in LFS.

**Large bundles:** If a fixture exceeds 100 MB, compress before committing:
```bash
bzip2 {file}.hhh
# Creates {file}.hhh.bz2; then decompress in test setup
```

Then reference in tests:
```typescript
const fixture = readFileSync(
  resolve(__dirname, '../fixtures/synthetic-large-v1.hhh.bz2')
);
const decompressed = bzip2Decompress(fixture);
```

## Accessing Fixtures in Tests

**Example (Vitest):**
```typescript
import { readFileSync } from 'fs';
import { resolve } from 'path';

test('parses synthetic-minimal-v1', () => {
  const fixture = readFileSync(
    resolve(__dirname, '../fixtures/synthetic-minimal-v1.hhh')
  );
  const result = parseBundle(new Uint8Array(fixture));
  expect(result.meshes.length).toBe(1);
  expect(result.meshes[0].vertices.length).toBe(9); // 3 vertices * 3 coords
});
```

**Golden file comparison:**
```typescript
const golden = readFileSync(
  resolve(__dirname, '../fixtures/goldens/synthetic-minimal-v1.golden.json'),
  'utf-8'
);
const expectedOutput = JSON.parse(golden);
expect(result).toEqual(expectedOutput);
```

## Updating Golden Files

If parser logic intentionally changes (e.g., refactoring deserializers), regenerate golden files:

```bash
cd packages/unity-ab-parser
pnpm test:update-goldens
```

This will re-parse all fixtures and update `.golden.json` files. **Review changes carefully before committing.**

