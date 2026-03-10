---
name: run-both-apps
description: "Use to run processor outputs and launch the web viewer with health checks. Keywords: run both apps, processor, viewer, local server, metadata."
---

# Run Both Apps Skill

Use this skill to run the Python processor and web viewer together.

## Outcomes

- Processor output is generated in data/outputs.
- Viewer is served on a local port.
- Basic metadata health check passes.

## Core Command

```bash
./scripts/run_both_apps.sh --mod-zip data/YOUR-MOD.zip --port 8000
```

Optional dependency unitypackage:

```bash
./scripts/run_both_apps.sh --mod-zip data/YOUR-MOD.zip --dependency-unitypackage data/dependency.unitypackage
```

## Success Signals

- `data/outputs/metadata.json` exists.
- Metadata includes non-negative `hhh_count` and `model_count`.
- Viewer available at `http://localhost:<port>/viewer/`.

## Agent Guidance

- Run processor first, then start the static server.
- Fail early for missing input archive.
- Print exact next step URL for viewer interaction skill.
