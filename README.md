# UnityPy Model Pipeline

Python-first UnityPy pipeline for generating GLB model artifacts from .hhh assets.

## Overview

This repository is being rebuilt around two components:

- `processor`: Reads Unity files and produces GLB models plus metadata.
- `viewer`: Static website that displays metadata cards and loads models in three.js on card click.

## Current MVP

- Reads a mod zip and discovers `.hhh` entries.
- Generates one deterministic GLB export attempt per `.hhh` under `data/outputs`.
- Uses UnityPy to inspect each `.hhh` and capture mesh diagnostics (name, vertices, triangles).
- Uses trimesh to export GLB with texture when available, or flat color fallback.
- Writes `data/outputs/metadata.json` with status, warnings, and per-asset mesh details.
- Optionally inspects a dependency `.unitypackage` and records a summary.
- Viewer renders a gallery of cards from metadata and loads a selected GLB in a 3D panel.

## Quickstart

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements-dev.txt
PYTHONPATH=processor/src python -m unity_processor.cli data/YOUR-MOD.zip --output data/outputs
pytest -q
```

## Development Skills

Repository-scoped Copilot skills and helper scripts are available for repeatable workflows.

### 1) Commits (Conventional Commits)

- Skill file: `.github/skills/commits/SKILL.md`
- Validate one message:

```bash
python3 scripts/validate_commit_message.py "feat(processor): add warning report"
```

- Run pre-push quality gate:

```bash
./scripts/commit_gate.sh
```

- Include mypy in strict mode:

```bash
./scripts/commit_gate.sh --strict-types
```

- Install git commit-msg hook:

```bash
./scripts/install_commit_msg_hook.sh
```

### 2) Run Both Apps

- Skill file: `.github/skills/run-both-apps/SKILL.md`
- Processor + viewer flow:

```bash
./scripts/run_both_apps.sh --mod-zip data/YOUR-MOD.zip --port 8000
```

Open the viewer at:

```text
http://localhost:8000/viewer/
```

### 3) Interact With Web Viewer (Observability-First)

- Skill file: `.github/skills/interact-web-viewer/SKILL.md`
- Uses Playwright CLI harness with console logs as primary evidence:

```bash
./scripts/viewer_observe_playwright.sh --url http://localhost:8000/viewer/ --report data/outputs/viewer_observability.json
```

If Playwright is missing, install it with npm in the workspace:

```bash
npm install --save-dev playwright
```

### 4) Detect Silent Processing Failures

- Skill file: `.github/skills/detect-silent-processing-failures/SKILL.md`
- Run warning-first checks on generated outputs:

```bash
python3 scripts/check_processed_outputs.py --metadata data/outputs/metadata.json
```

- Strict mode for CI or gated workflows:

```bash
python3 scripts/check_processed_outputs.py --metadata data/outputs/metadata.json --strict
```

## Notes

- Output metadata is schema version 2 and includes per-item `model`, `model_format`, `mesh_bounds`, and `export_status`.
- Viewer loads models on demand only (no startup preload): click a card to fetch and show its GLB.
