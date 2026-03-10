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

## Notes

- Output metadata is schema version 2 and includes per-item `model`, `model_format`, `mesh_bounds`, and `export_status`.
- Viewer loads models on demand only (no startup preload): click a card to fetch and show its GLB.
