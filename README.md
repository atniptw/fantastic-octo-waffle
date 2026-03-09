# UnityPy Image Pipeline

Phase 1 reboot scaffold for a Python-first project.

## Overview

This repository is being rebuilt around two components:

- `processor`: Reads Unity files and produces rendered images plus metadata.
- `viewer`: Static website that displays generated images and metadata.

## Phase 1 Scope

- Hard reset legacy codebase.
- Minimal Python 3.11 + UnityPy environment.
- Placeholder processor and viewer modules.
- Smoke tests and CI baseline.

## Quickstart

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements-dev.txt
python -m unity_processor.cli --help
pytest -q
```

## Next

Phase 2 will implement real parsing and rendering.
