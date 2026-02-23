# Verification workflow

This repository uses deterministic verification commands so human contributors and coding agents produce equivalent signals.

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

## Evidence for pull requests

Include:

- Commands executed
- Pass/fail outcome
- Any known flaky behavior and mitigation
- Playwright report/traces when E2E fails

## CI parity

GitHub Actions invokes the same `npm run ...` scripts used locally.
