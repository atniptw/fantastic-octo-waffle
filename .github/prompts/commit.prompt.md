---
agent: agent
---

# Copilot Commit Message Prompt

Write a high-quality Conventional Commit message for the staged changes.
Follow these rules strictly:

## Header (required)
- Format: `type(scope)!: concise, imperative summary`
- Keep summary <= 72 characters
- Types: feat, fix, docs, test, build, ci, refactor, perf, style, chore, revert
- Scopes (use one when obvious): blazor, worker, parser, tests, docs, ci
- Add `!` only for breaking changes

## Body (recommended)
- Explain what and why, not how
- Group notable changes as bullets when helpful
- Mention user impact and rationale
- Wrap at ~72 characters per line

## Issue Links / Footers (when applicable)
- Close issues: `Closes #123` (one per line)
- Breaking changes footer:
  - `BREAKING CHANGE: describe the migration or impact`
- Co-authors:
  - `Co-authored-by: Name <email@domain>`

## Do
- Use precise language and the imperative mood ("add", "fix")
- Reflect the dominant change if multiple files changed
- Prefer `refactor:` for behavior-preserving restructure
- Prefer `test:` for new/updated tests only
- Prefer `docs:` for documentation-only changes

## Don't
- Don't include build artifacts or generated diffs
- Don't restate filenames or code diffs verbatim
- Don't use vague summaries like "updates" or "misc"

## Examples
- `feat(parser): add Mesh StreamingInfo .resS resolution`
- `fix(blazor): handle CORS errors from Worker proxy gracefully`
- `refactor(tests): extract shared mesh fixtures`
- `ci: add dotnet test with coverage reporting`
- `docs: add Unity parsing pitfalls to README`

---

Use this template to compose the final message:

<type>(<scope>)<bang>: <summary up to 72 chars>

<body explaining what and why, wrapped at ~72 cols>

<optional footers: Closes #123 | BREAKING CHANGE: ... | Co-authored-by: ...>