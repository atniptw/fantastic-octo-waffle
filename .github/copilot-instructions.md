# Copilot instructions

## Purpose

Maintain deterministic local and CI workflows for Blazor WASM development.

## Required command contract

Use scripts from `package.json` for all verification:

- `npm run lint`
- `npm run build:app`
- `npm run test:unit`
- `npm run test:e2e`
- `npm run verify`

Do not replace these with ad-hoc commands in PR guidance unless updating the contract itself.

## PR quality expectations

- Keep changes focused and minimal.
- Add or update tests when behavior changes.
- Report exact verification commands and outcomes.
- Prefer root-cause fixes over cosmetic patches.

## Environment assumptions

- Primary workflow is VS Code devcontainer.
- App URL for local testing and E2E is `http://localhost:5075`.

