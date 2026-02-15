---
name: commit
description: Generate Conventional Commits messages by inspecting all staged changes only. Keywords: commit message, conventional commits, git diff --staged, staged changes.
---
## Purpose
Use this skill when the user asks for a commit message or wants guidance on committing changes. The output must reflect all staged changes, not just the most recent edit.

## Scope and rules
- Only inspect staged changes. Ignore unstaged files completely.
- Summarize the full staged diff before writing the message.
- Use Conventional Commits format: `<type>[optional scope][!]: <description>`.
- Prefer the imperative mood in the description (for example, "add", "fix", "refactor").
- If breaking changes exist, include `!` and (optionally) a `BREAKING CHANGE:` footer.
- If there are no staged changes, ask the user to stage files before generating a message.

## Process
1. Determine staged files and the staged diff.
2. Identify the primary change type (`feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `build`, `ci`, `perf`, `style`, `revert`).
3. Choose an optional scope when it clarifies (for example, `ui`, `e2e`, `planner`, `services`).
4. Draft a concise summary that describes the net effect of all staged changes.

## Examples
- `feat(planner): add mod scan status to load wizard`
- `fix(services): handle non-zip uploads in decoration indexer`
- `test(e2e): cover base mod upload flow`