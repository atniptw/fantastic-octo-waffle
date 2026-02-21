```skill
---
name: snapshots
description: Generate and verify UnityAsset snapshots, including UnityPy-enriched oracle diagnostics (schema v3). Keywords: snapshots, unitypy, oracle, schema v3, fixtures, parser gaps.
---
## Purpose
Use this skill when the user asks to create, refresh, or verify snapshot artifacts for parser behavior.
This skill focuses on UnityAsset snapshots in `tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityAssets`.

## Scope
- Update snapshot artifacts for selected fixtures listed in `index.json`.
- Enrich snapshots with UnityPy oracle mesh diagnostics (`oracle`, `parserGap`, schema v3).
- Verify deterministic contract assertions in `UnityAssetSnapshotContractTests`.
- Do not change parser logic unless explicitly requested.

## Inputs and outputs
- Input fixtures: `.hhh` files in `tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityAssets`.
- Snapshot index: `tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityAssets/index.json`.
- Snapshot files: `*-unityasset-v2.json` (schema can be v3).
- Enrichment script: `tests/UnityAssetParser.Tests/tools/enrich_unityasset_snapshots_with_unitypy.py`.

## Rules
- Keep snapshot output deterministic (stable ordering and formatting from script).
- Preserve fixture list/count in `index.json` unless user asks to add/remove fixtures.
- Keep `fixture.sha256` accurate and unchanged unless fixture bytes changed.
- For schema v3 snapshots, require top-level `oracle` and per-mesh `oracle` + `parserGap` blocks.
- Always run focused tests after regeneration.

## Environment setup (UnityPy)
Use an isolated venv for UnityPy:

```bash
python3 -m venv /tmp/unitypy-venv
/tmp/unitypy-venv/bin/pip install -e /workspaces/UnityPy-master
```

## Regeneration workflow
1. Ensure UnityPy venv is available.
2. Run enrichment generator:

```bash
cd /workspaces/fantastic-octo-waffle
/tmp/unitypy-venv/bin/python tests/UnityAssetParser.Tests/tools/enrich_unityasset_snapshots_with_unitypy.py
```

3. Validate contracts:

```bash
dotnet test tests/UnityAssetParser.Tests/UnityAssetParser.Tests.csproj --filter "UnityAssetSnapshotContractTests"
dotnet test tests/UnityAssetParser.Tests/UnityAssetParser.Tests.csproj --filter "UnityAssetSnapshotContractTests|HhhParserTests"
```

## Troubleshooting
- `ModuleNotFoundError` for UnityPy deps: recreate venv and reinstall editable UnityPy.
- Snapshot schema mismatch: ensure tests allow schema v3 and required v3 fields exist.
- Unexpected Unicode diffs in names: keep UTF-8 JSON output; do not force ASCII escaping.
- If only one fixture needs refresh, still run snapshot contract tests to ensure cross-fixture consistency.

## Handoff requirements
Include:
1. Which snapshots/index were updated.
2. Commands run.
3. Test pass/fail summary.
4. Notable oracle gap signals (for example `externalStreamLikelyCause: true`).
```
