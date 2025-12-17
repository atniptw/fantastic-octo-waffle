# Contributing to R.E.P.O. Cosmetic Catalog

Note: This project is volunteer‑maintained. Response times and outcomes may vary and are not guaranteed.

Thanks for your interest in contributing! We welcome issues, pull requests, docs, tests, and ideas.

By participating in this project, you agree to abide by our Code of Conduct. See CODE_OF_CONDUCT.md.

## Getting Started

- Requirements: Node.js 18+ and npm
- Install dependencies: `npm ci`
- Start dev server: `npm run dev` then open http://localhost:5173
- Run unit tests: `npm test`
- Run e2e tests: `npm run test:e2e` (installs Playwright browsers on first run)
- Lint: `npm run lint` | Format: `npm run format`
- Build: `npm run build`

## Development Guidelines

- Write tests for new features and bug fixes (Vitest for unit, Playwright for e2e)
- Keep changes focused and small; include relevant docs updates
- Follow Conventional Commits for messages (feat, fix, docs, chore, test, refactor)
- Prefer TypeScript, keep types strict, avoid one-letter variable names
- Handle corrupt `.hhh` files gracefully; never crash the UI

## Pull Requests

- Link issues when applicable (e.g., "Fixes #123")
- Include a short summary and screenshots/GIFs if UI changes
- Ensure CI passes: lint, unit tests, e2e (for UI changes), and build
- Add or update tests and docs
- Request review when ready; drafts are welcome for early feedback

## Issue Triage

- Use labels: `bug`, `enhancement`, `good first issue`, `help wanted`, `question`
- Provide clear reproduction steps for bugs; attach sample ZIPs if possible
- For features, describe motivation, scope, and acceptance criteria

## Architecture Notes

- Client-only React + Vite app; no backend
- ZIPs handled with JSZip; storage via IndexedDB
- 3D preview via Three.js; UnityFS parsing is client-side (Level 2)

## Local E2E Test Tips

- First run: `npx playwright install --with-deps`
- Headed debug UI: `npm run test:e2e:ui`

## Release & Versioning

- Semantic Versioning (MAJOR.MINOR.PATCH)
- Maintain CHANGELOG.md; propose entries in PRs

## Questions

For help or questions, open an issue with the `question` label or start a Discussion (if enabled).