---
name: interact-web-viewer
description: "Use for evidence-based viewer interaction checks using Playwright CLI and console diagnostics. Keywords: viewer interaction, playwright, console logs, web checks."
---

# Interact Web Viewer Skill

Use this skill to inspect viewer behavior without guesswork.

## Outcomes

- Interaction steps are executed deterministically.
- Browser console diagnostics are captured as primary evidence.
- A machine-readable report is generated for pass/warn/fail analysis.

## Core Command

```bash
./scripts/viewer_observe_playwright.sh --url http://localhost:8000/viewer/ --report data/outputs/viewer_observability.json
```

## Evidence Requirements

- Interaction timeline with step status.
- Console events including level and message.
- Selected card context and model status text.

## Policy

- Default: warning-first for anomalies.
- Optional strict mode may convert warnings into non-zero exit.

## Agent Guidance

- Do not infer viewer behavior from metadata alone.
- Require evidence artifact before concluding interaction quality.
- If Playwright CLI is unavailable, emit actionable setup guidance.
