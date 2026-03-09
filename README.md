# UnityPy Image Pipeline

Python-first UnityPy pipeline for generating preview images from .hhh assets.

## Overview

This repository is being rebuilt around two components:

- `processor`: Reads Unity files and produces rendered images plus metadata.
- `viewer`: Static website that displays generated images and metadata.

## Current MVP

- Reads a mod zip and discovers `.hhh` entries.
- Generates one deterministic PNG preview per `.hhh` under `data/outputs`.
- Uses UnityPy to inspect each `.hhh` and capture mesh diagnostics (name, vertices, triangles).
- Writes `data/outputs/metadata.json` with status, warnings, and per-asset mesh details.
- Optionally inspects a dependency `.unitypackage` and records a summary.
- Viewer renders a gallery of cards from metadata plus warning labels.

## Quickstart

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements-dev.txt
PYTHONPATH=processor/src python -m unity_processor.cli data/YOUR-MOD.zip --output data/outputs
pytest -q
```

## Next

Next milestone is real mesh extraction/rendering with UnityPy instead of placeholder cards.
