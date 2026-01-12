# CI Design

This document describes the continuous integration (CI) plan for the project, focused on fast feedback, dead-code detection, and reliable deployments to GitHub Pages and Cloudflare Workers.

## Goals

- Fast feedback on PRs (lint, type, unit/integration tests).
- Catch dead/unused code and unused dependencies early.
- Keep build reproducible (Node 24 LTS, pnpm).
- Automatic deploy to GitHub Pages on main; optional gated deploy to Cloudflare Worker.

## Pipelines

### 1) Test & Lint (per push/PR)

- **Node:** 24.x LTS
- **Steps:**
  - `pnpm install`
  - `pnpm format:check` (Prettier) — fail on unformatted code
  - `pnpm lint` (ESLint + TypeScript rules for unused vars)
  - `pnpm typecheck` (tsc --noEmit)
  - `pnpm test:unit`
  - `pnpm test:integration`
  - (Optional per PR) `pnpm test:e2e --trace on`
  - `pnpm ts-prune` (dead exports) or `pnpm knip` (unused files/exports/deps)
  - `pnpm build` (fail on warnings)

### 2) E2E (Playwright) (PRs to main or label `e2e`)

- Install Playwright browsers (cached).
- Run `pnpm test:e2e --trace on`.
- Upload Playwright report artifact on failure.

### 3) Build & Deploy Web (GitHub Pages)

- Trigger: merge to `main`.
- Steps: `pnpm install`, `pnpm build` in `apps/web`.
- Upload `dist/` as Pages artifact; deploy via `actions/deploy-pages`.

### 4) Deploy Worker (optional, manual or protected)

- Trigger: manual dispatch or tag.
- Steps: `pnpm install`, `pnpm build` in `apps/worker`, `wrangler deploy`.
- Secrets: `CF_API_TOKEN`, `CF_ACCOUNT_ID`.

## Dead Code & Dependency Hygiene

- **TypeScript:** `noUnusedLocals`, `noUnusedParameters`, `noFallthroughCasesInSwitch` in `tsconfig`.
- **ESLint:** `@typescript-eslint/no-unused-vars` (ignore args prefixed with `_`), `@typescript-eslint/no-unused-expressions`.
- **ts-prune:** Detect unused exports. CI fails if output is non-empty.
  - Example: `pnpm ts-prune | tee /tmp/tsprune && test ! -s /tmp/tsprune`
- **knip (optional):** Broader unused detector (files/exports/deps). Configure ignores for build-generated entrypoints.
- **depcheck (optional, periodic):** Unused/missing deps; run nightly/weekly to reduce PR noise.

## Caching

- Use `actions/setup-node` with `cache: 'pnpm'`.
- Cache Playwright browsers only on jobs that run E2E (saves ~1GB per run).

## Artifacts

- Playwright HTML report on E2E failures.
- (Optional) Coverage report (lcov) if coverage gate is added later.

## Branch Protection & Gates

- Require Test & Lint workflow to pass before merging to `main`.
- E2E workflow required on `main` (or label-based opt-in during early stages).
- Deploy workflows run only after required checks pass.

## Future Enhancements

- Coverage threshold gate (e.g., 80% on parser package) once stable.
- knip `--strict` mode when entrypoints are finalized.
- Nightly depcheck to keep package.json clean.
- SAST/secret scanning (e.g., `gitleaks`) if needed.
