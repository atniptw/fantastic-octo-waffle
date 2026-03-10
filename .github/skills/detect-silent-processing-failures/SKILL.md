---
name: detect-silent-processing-failures
description: "Use to detect suspicious processor outputs even when no exception is thrown. Keywords: silent failure, metadata validation, glb checks, anomaly detection."
---

# Detect Silent Processing Failures Skill

Use this skill after processor runs to identify suspicious outcomes.

## Outcomes

- Metadata accounting inconsistencies are reported.
- Referenced models are validated for existence and size.
- Suspicious mesh and bounds values are flagged.

## Core Command

```bash
python3 scripts/check_processed_outputs.py --metadata data/outputs/metadata.json
```

Strict mode:

```bash
python3 scripts/check_processed_outputs.py --metadata data/outputs/metadata.json --strict
```

## Warning Heuristics

- `hhh_count` differs from `hhh_inspected`.
- `model_count + export_failed_count` differs from `hhh_inspected`.
- Item references missing or empty model file.
- Invalid `mesh_bounds` structure.

## Agent Guidance

- Default to warnings only unless strict mode requested.
- Provide a compact summary and detailed per-item evidence.
- Keep checks deterministic and schema-version aware.
