---
name: verification
description: Run autonomous local reproduction and verification for UI and parser changes, including UnityPy oracle parity checks. Keywords: verify, reproduce, tests, playwright, dotnet test, UnityPy oracle.
---
## Purpose
Use this skill when implementing fixes/features or triaging reported issues in this repository. The goal is to verify locally and provide evidence before asking the user for manual checks.

## Rules
- Reproduce first using local commands.
- Run the narrowest deterministic check first, then escalate only as needed.
- Prefer automated logs and test outputs over user-provided logs.
- Only request manual user input after local fallback steps are exhausted.

## Command routing
- App runtime: `npm run app:run`
- Lint + analyzers: `npm run lint`
- App build: `npm run build:app`
- Unit tests: `npm run test:unit`
- E2E full: `npm run test:e2e`
- Full gate: `npm run verify`

## Decision matrix
- Documentation-only change: run targeted checks as needed and state rationale.
- App code change: run `npm run lint`, `npm run build:app`, and `npm run test:unit`.
- UI behavior change: add `npm run test:e2e`.
- Cross-cutting or release-critical change: run `npm run verify`.

## Handoff requirements
Include:
1. Commands run.
2. Pass/fail status.
3. Key errors and fallback steps.
4. Any known flake indicators and artifact references where applicable.
