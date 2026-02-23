# Roadmap

## Cross-Phase Hard Constraint
- All mod ingestion, parsing, composition, and preview generation remain local to the user's browser.
- No roadmap phase may introduce server-side handling, proxying, or redistribution of third-party mod files.

## Phase 0: Documentation Baseline
### Exit Criteria
- Core docs exist and are internally consistent.
- Parser source decision is explicit (primary + fallback).
- MVP compatibility matrix is documented.

## Phase 1: Ingest + Library MVP
### Scope
- Upload zip files
- Discover `.hhh` bundles
- Persist files and metadata in IndexedDB
- Display mod/cosmetic library

### Exit Criteria
- Representative zips import successfully.
- Data persists across refresh.
- Errors are visible and actionable.

## Phase 2: Parser Core Extraction
### Scope
- Extract minimal parser code from primary source
- Implement AssetBundle parse path for MoreHead-style inputs
- Implement required unitypackage parse path for avatar/base assets
- Integrate extracted code behind `src/RepoMod.Parser` abstractions
- Validate behavior in `tests/RepoMod.Parser.UnitTests`

### Exit Criteria
- Fixture corpus parses to stable DTOs.
- Parser core compiles without GUI/native dependencies.

## Phase 3: Correctness and Parity
### Scope
- Add parser fixture tests
- Add UnityPy-assisted parity checks for bug triage
- Track known deltas

### Exit Criteria
- MVP fixtures pass deterministic parse assertions.
- Known edge cases are documented.

## Phase 4: Composition + GLB
### Scope
- Merge avatar + selected cosmetics
- Generate GLB in-browser

### Exit Criteria
- Multiple cosmetics can be combined.
- GLB output loads in viewer reliably.

## Phase 5: Viewer Integration
### Scope
- three.js preview scene
- Resource lifecycle and replacement handling
- Basic controls and lighting defaults

### Exit Criteria
- Preview updates correctly on selection changes.
- No obvious memory/resource leaks during repeated previews.

## Phase 6: Deployment Readiness
### Scope
- Stabilize UX and error handling
- Final verification pass
- Publish deployment notes

### Exit Criteria
- `npm run verify` passes.
- App is deployed and core flow works end-to-end.
- Deployment architecture confirms no server receives or stores mod content.
- Deployment is validated on GitHub Pages with working app base path and static asset loading.
