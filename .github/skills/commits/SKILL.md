---
name: commits
description: "Use for commit prep, Conventional Commits v1.0.0 validation, and pre-push quality checks. Keywords: commit, conventional commits, pre-push, commit-msg, lint, mypy, pytest."
---

# Commits Skill

Use this skill when preparing changes for commit and push.

## Outcomes

- Commit messages conform to Conventional Commits v1.0.0.
- Repository quality checks pass before push.
- Commit intent is clear and searchable.

## Commit Format

Use the header:

```text
type(scope)?: description
```

Allowed types:

- feat
- fix
- docs
- style
- refactor
- perf
- test
- build
- ci
- chore
- revert

Breaking changes:

- Use `!` in the header, for example `feat(cli)!: change output schema`.
- Or include `BREAKING CHANGE: <details>` in footer.

## Core Commands

Run quality gate:

```bash
./scripts/commit_gate.sh
```

Run with strict type checks:

```bash
./scripts/commit_gate.sh --strict-types
```

Validate one commit message:

```bash
python3 scripts/validate_commit_message.py "feat(processor): export metadata warnings"
```

Install local commit-msg hook:

```bash
./scripts/install_commit_msg_hook.sh
```

## Agent Guidance

- Reject commit headers that do not match Conventional Commits format.
- Prefer a scope tied to a project area, for example `processor`, `viewer`, `tests`, `docs`, `ci`.
- Keep descriptions imperative and concise.
- Run quality gate before proposing commit or push.
