---
name: playwright-cli
description: Automate browser interactions against the Blazor app using playwright-cli. Use when you need to navigate the app, interact with the Load Mods modal, take snapshots, record test flows, or verify UI behavior. Keywords: playwright-cli, browser automation, snapshot, e2e, screenshot, mod wizard, upload.
---
## Purpose
Use this skill to drive the local Blazor WebAssembly app with `playwright-cli` for exploratory testing, screenshot capture, test-flow recording, and manual verification of UI changes.

## Prerequisites
- App must be running: `npm run app:run` (serves at `http://localhost:5075`).
- `playwright-cli` must be available: `npx playwright-cli --help` or `playwright-cli --help`.

## Scope and rules
- Always start the app before opening a browser session if it is not already running.
- Use `data-testid` attributes as the primary way to identify elements (check snapshots for refs).
- Prefer `playwright-cli snapshot` to discover element refs before interacting.
- Close the browser session when done: `playwright-cli close`.
- Do not commit `.playwright-cli/` snapshot artifacts unless they are part of a deliberate fixture update.

## Key entry points

| Goal | Starting URL |
|------|--------------|
| Home / Planner | `http://localhost:5075/` |
| Load Mods modal | Open home, then click the "Load Mods" button (`data-testid="open-mods-wizard"`) |

## Common workflows

### 1. Open the app and snapshot the landing page
```bash
playwright-cli open http://localhost:5075/
playwright-cli snapshot
```

### 2. Open the Load Mods modal
```bash
playwright-cli open http://localhost:5075/
playwright-cli snapshot
# Find the ref for the "Load Mods" button via the snapshot, e.g. e3
playwright-cli click e3
playwright-cli snapshot
```

### 3. Upload a mod zip and verify scanned results
```bash
playwright-cli open http://localhost:5075/
playwright-cli click e3
playwright-cli snapshot
# Locate the file upload input ref, e.g. e7
playwright-cli upload ./tests/e2e/fixtures/morehead-1.4.4.zip
playwright-cli snapshot
```

### 4. Take a screenshot for UI verification
```bash
playwright-cli open http://localhost:5075/
playwright-cli screenshot --filename=landing.png
playwright-cli click e3
playwright-cli screenshot --filename=mods-modal.png
playwright-cli close
```

### 5. Record a test flow for code generation
```bash
playwright-cli open http://localhost:5075/
playwright-cli snapshot
playwright-cli click e3          # open modal
playwright-cli snapshot
playwright-cli upload ./tests/e2e/fixtures/morehead-1.4.4.zip
playwright-cli snapshot
playwright-cli close
# Collect the generated Playwright TypeScript from each command output
# and paste into a new spec in tests/e2e/
```

## Session management
```bash
playwright-cli list              # see active browser sessions
playwright-cli close-all         # close all browsers when done
playwright-cli kill-all          # forcefully stop stale processes
```

## Debugging
```bash
playwright-cli console           # inspect console messages after an action
playwright-cli network           # inspect network requests
playwright-cli tracing-start
# ... perform actions ...
playwright-cli tracing-stop      # saves trace for Playwright Trace Viewer
```

## WebGL / Chromium workaround (containers)
In this dev container, headless Firefox blocks WebGL. Use Playwright's Chromium build with a CLI config that disables the sandbox and enables software WebGL.

Create `.playwright/cli.config.json`:
```json
{
	"browser": {
		"browserName": "chromium",
		"launchOptions": {
			"chromiumSandbox": false,
			"headless": true,
			"args": [
				"--no-sandbox",
				"--disable-dev-shm-usage",
				"--use-gl=swiftshader",
				"--enable-webgl",
				"--ignore-gpu-blocklist"
			]
		},
		"contextOptions": {
			"viewport": null
		}
	}
}
```

Open Chromium with the config:
```bash
playwright-cli open http://localhost:5075/ --config .playwright/cli.config.json
```

Quick WebGL check:
```bash
playwright-cli eval 'document.createElement("canvas").getContext("webgl2") !== null'
```

## Handoff requirements
Include:
1. Commands run and their output.
2. Screenshot or snapshot filenames created.
3. Any element refs and the actions taken on them.
4. Pass/fail status and notable errors.
