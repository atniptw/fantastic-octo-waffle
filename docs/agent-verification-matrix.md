# Agent Verification Matrix (Local)

This document is the authoritative local verification policy for AI-agent issue triage and implementation validation in VS Code.

## Core workflow

1. Reproduce the issue locally.
2. Run the narrowest deterministic validation command(s).
3. Apply the minimal fix.
4. Re-run targeted checks.
5. Escalate to broader checks based on risk.
6. Report evidence (commands + pass/fail + key output).

Do not ask for manual user verification unless local fallback steps are exhausted.

## Command catalog

- App run: `npm run app:run`
- App build: `npm run build:app`
- Unit tests: `npm run test:unit`
- E2E smoke: `npm run test:e2e:landing`
- E2E full: `npm run test:e2e`
- Parser verify bundle: `npm run verify:parser`

## Change-type matrix

| Change / Issue Type | Required minimum checks | Escalate when | Escalation checks |
| --- | --- | --- | --- |
| UI layout/content only (`.razor`, `.razor.css`, page/layout components) | `npm run test:e2e:landing` | A multi-step wizard/modal/flow changed | `npm run test:e2e` |
| UI + mod wizard behavior | `npm run test:e2e` | Test instability or startup errors | `npm run app:run` to verify runtime logs, then re-run e2e |
| Parser internals (`src/UnityAssetParser/**`) | `npm run test:unit` | Touches decompression/container parsing/object metadata | `npm run verify:parser` + UnityPy oracle comparison |
| Shared service logic (`src/BlazorApp/Services/**`) | `npm run test:unit` if parser-facing, otherwise `npm run test:e2e:landing` | Crosses parser/UI boundary | Run both `npm run test:unit` and `npm run test:e2e` |
| Build/config/tooling changes | `npm run build:app` + closest relevant tests | Startup/test execution behavior changed | `npm run test:unit` and `npm run test:e2e` |

## Parser oracle checks (UnityPy reference)

When parser behavior is uncertain or fixtures fail unexpectedly:

1. Generate deterministic UnityPy oracle output for the same fixture.
2. Store/update the matching committed artifact under `tests/UnityAssetParser.Tests/fixtures/oracle`.
3. Compare expected/actual on:
   - fixture hash
   - top-level entry counts
   - container kind distribution
   - selected parser summary metadata
4. Only then decide whether to patch parser logic or fixture/test assumptions.

See [docs/parser-oracle-workflow.md](docs/parser-oracle-workflow.md).

## Required handoff evidence

Every implementation handoff should include:

- Commands executed.
- Which checks passed/failed.
- If failed: the first actionable error and what fallback step was attempted.
- If parser changes: whether UnityPy oracle comparison was run and the parity status.