# Agent Troubleshooting Playbook (Local)

Use this playbook when issues are reported during planning or implementation.

## Default triage sequence

1. Reproduce with the narrowest relevant command.
2. Capture terminal output and test failure details.
3. Apply a minimal fix.
4. Re-run targeted checks.
5. Escalate to broader checks if needed.

## Common failure modes

### App startup fails for Playwright

Symptoms:

- `webServer` timeout in Playwright.
- `ECONNREFUSED` against `127.0.0.1:5075`.

Recovery steps:

1. Run `npm run app:run` and inspect startup output.
2. Resolve project build/runtime errors.
3. Re-run `npm run test:e2e:landing`.

### Port already in use (`5075`)

Symptoms:

- Kestrel fails to bind.

Recovery steps:

1. Identify process using port and stop it.
2. Re-run `npm run test:e2e:landing`.
3. If conflict is persistent, temporarily use a different URL and matching `PLAYWRIGHT_BASE_URL`.

### Missing e2e fixture

Symptoms:

- Playwright fails on upload step due to missing zip fixture.

Recovery steps:

1. Verify fixture path in test output.
2. Confirm fixture exists under `tests/e2e/fixtures`.
3. Re-run targeted e2e test.

### Parser/unit test failures

Symptoms:

- `dotnet test` fails in `UnityAssetParser.Tests`.

Recovery steps:

1. Re-run `npm run test:unit` to confirm deterministic failure.
2. Isolate failing parser path (tar/gzip, LZ4, serialized parsing).
3. If behavior is ambiguous, run UnityPy oracle workflow and compare outputs.

### Parser mismatch versus expected fixture behavior

Symptoms:

- Tests pass syntactically but parsed values differ from expected semantics.

Recovery steps:

1. Generate oracle output with UnityPy for the same fixture.
2. Refresh the committed oracle artifact in `tests/UnityAssetParser.Tests/fixtures/MoreHead-Snapshots/UnityPackage` if expected behavior changed.
3. Compare metadata deltas and update parser logic or expectations based on reference parity.

## Escalation rule

Only request manual user logs/input after:

- Local reproduction was attempted.
- At least one fallback command path was executed.
- Captured output is insufficient to continue.