---
name: uv
description: "Use when working with Python dependency and environment management using uv: project bootstrap, lock/sync, running scripts, managing tools, exporting requirements, and CI setup. Keywords: uv init, uv add, uv lock, uv sync, uv run, uv tool, pyproject, virtualenv."
---

# uv Workflow Skill

Use this skill when the task involves Python environment or dependency workflows and you want fast, reproducible commands using `uv`.

## Outcomes

- Create or update Python projects with deterministic dependencies.
- Keep environments in sync with `pyproject.toml` and lockfiles.
- Run scripts/tests inside the managed environment.
- Set up CI-friendly dependency restore steps.

## Assumptions

- `uv` is installed and on PATH.
- Project uses `pyproject.toml` as the source of truth.
- Commands are run from the repository root unless specified.

## Core Commands

### 1) Initialize a project

```bash
uv init
```

For package-style layout:

```bash
uv init --package
```

### 2) Create/sync environment from lock

```bash
uv sync
```

Sync with dev dependencies too:

```bash
uv sync --dev
```

### 3) Add/remove dependencies

Runtime dependency:

```bash
uv add <package>
```

Dev dependency:

```bash
uv add --dev <package>
```

Remove dependency:

```bash
uv remove <package>
```

### 4) Run commands in the managed environment

```bash
uv run python -m pytest
uv run ruff check .
uv run mypy .
```

### 5) Lock and upgrade strategy

Update lockfile:

```bash
uv lock
```

Upgrade all:

```bash
uv lock --upgrade
```

Upgrade one package:

```bash
uv lock --upgrade-package <package>
```

### 6) Tool management (global/user tools)

Install CLI tools:

```bash
uv tool install ruff
uv tool install mypy
```

Run ad-hoc tool without permanent install:

```bash
uvx ruff --version
```

## Project Patterns

### Existing repo bootstrap

```bash
uv sync --dev
uv run pytest -q
```

### Add dependency + verify

```bash
uv add pillow
uv run python -c "import PIL; print(PIL.__version__)"
```

### Export requirements for external systems

If a downstream system requires `requirements.txt`:

```bash
uv export --format requirements-txt -o requirements.txt
```

For dev requirements:

```bash
uv export --format requirements-txt --dev -o requirements-dev.txt
```

## CI Recipe

Use `uv` in CI to avoid drift:

```bash
uv sync --dev --frozen
uv run pytest -q
```

Recommended flags:

- `--frozen`: fail if lockfile and project metadata are out of sync.
- `--dev`: include development dependencies for lint/test jobs.

## Agent Guidance

- Prefer `uv add`/`uv remove` over manual edits to dependency lists.
- Prefer `uv run` over invoking bare `python` for project commands.
- Prefer `uv sync --frozen` in CI-focused changes.
- If a repo is intentionally pip-only, ask before migrating workflows to uv.

## Quick Decision Matrix

- Need to run tests with correct env: `uv run pytest`.
- Need new package: `uv add <pkg>`.
- Need reproducible env refresh: `uv sync`.
- Need lock refresh: `uv lock`.
- Need external requirements file: `uv export ...`.
