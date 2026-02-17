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
- For parser ambiguity, use UnityPy oracle workflow from `docs/parser-oracle-workflow.md`.
- Only request manual user input after local fallback steps are exhausted.

## Command routing
- App runtime: `npm run app:run`
- App build: `npm run build:app`
- Unit tests: `npm run test:unit`
- E2E smoke: `npm run test:e2e:landing`
- E2E full: `npm run test:e2e`
- Parser bundle: `npm run verify:parser`

## Decision matrix
- UI-only change: run `npm run test:e2e:landing`; escalate to full `npm run test:e2e` for multi-step behavior.
- Parser-only change: run `npm run test:unit`; escalate to `npm run verify:parser` for broad parsing changes.
- Mixed UI + parser change: run both `npm run test:unit` and `npm run test:e2e`.

## Handoff requirements
Include:
1. Commands run.
2. Pass/fail status.
3. Key errors and fallback steps.
4. Parser oracle parity status where applicable.
