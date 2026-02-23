# Verification workflow

This repository uses deterministic verification commands so human contributors and coding agents produce equivalent signals.

This document also defines planning-to-implementation verification gates for the dress-up app roadmap.

## Required checks

Run in this order when possible:

1. `npm run lint`
2. `npm run build:app`
3. `npm run test:unit`
4. `npm run test:e2e`

Or run the combined gate:

```bash
npm run verify
```

## Milestone verification matrix

### Documentation baseline
- Planning docs are present and internally consistent.
- Parser source decision is explicit (primary and fallback).
- MVP support matrix is explicit for MoreHead-style `.hhh` bundles on Unity 2022.3.

### Ingest and storage MVP
- Upload flow imports representative zip samples.
- `.hhh` discovery results are persisted in IndexedDB.
- Data remains available after page refresh.

### Parser extraction
- Parser core builds without GUI/native dependencies.
- Fixture corpus parses to stable DTO outputs.

### Parser correctness and parity
- Unit tests cover fixture parse expectations.
- UnityPy comparisons are used for ambiguous parse outcomes and bug triage.
- Known parser deltas are documented.

### Composition and viewer
- Avatar + selected cosmetics produce valid GLB for preview.
- Repeated preview cycles do not leak obvious resources.

## Evidence for pull requests

Include:

- Commands executed
- Pass/fail outcome
- Any known flaky behavior and mitigation
- Playwright report/traces when E2E fails

For parser-related changes, additionally include:

- Fixture set used for validation
- Primary/fallback parser source in use
- Any UnityPy parity findings (if applicable)

## CI parity

GitHub Actions invokes the same `npm run ...` scripts used locally.
