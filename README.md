# fantastic-octo-waffle

Blazor WebAssembly app workspace with a deterministic local/CI workflow for both developers and coding agents.

## Quick start (devcontainer-first)

1. Open this repository in VS Code.
2. Reopen in Container when prompted.
3. Run:

```bash
npm ci
npm run app:run
```

4. Open `http://localhost:5075`.

## Local command contract

Use these commands locally and in automation:

- `npm run app:run` - start the Blazor app on port 5075.
- `npm run build:app` - restore and build the app.
- `npm run test:unit` - run .NET unit tests.
- `npm run test:e2e` - run full Playwright end-to-end tests.
- `npm run lint` - run JavaScript lint and .NET format validation.
- `npm run verify` - full quality gate (lint, build, unit, e2e).

## CI expectations

Pull requests to `main` run strict quality gates:

1. Lint + analyzers
2. Build
3. Unit tests
4. End-to-end tests (Playwright)

Artifacts are uploaded for debugging test failures.
