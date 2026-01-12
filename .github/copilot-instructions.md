# Copilot Instructions for Reviews

When reviewing code in this repo, focus on finding correctness issues, regressions, and anti-patterns. Favor concrete, actionable feedback.

## Priorities
- Correctness and safety first; style second.
- Flag potential performance or memory risks in the parser and rendering paths.
- Call out security risks in the Worker (allowlist, CORS, size limits).

## Anti-Patterns to Catch
- Unused code/exports, dead branches, and ignored promises.
- Floating promises or missing error handling in async code.
- Tight loops or large allocations on the main thread (parser must stay in a Web Worker).
- Bypassing host allowlist or size limits in the Cloudflare Worker proxy.
- Missing `AbortController` handling for fetch/parse flows.
- Direct DOM mutations in Preact components (prefer state-driven updates).
- Missing null/undefined guards on API responses.
- Using `any` without justification; prefer typed interfaces.
- Adding new dependencies without checking necessity or size impact.

If a finding could be prevented by static analysis (ESLint/TS rules, ts-prune/knip, etc.), recommend updating CI to enforce it so it doesn’t recur.

## Code Smells (Refactoring.Guru style)
- Long Function / Long Parameter List / Large Class — break into smaller helpers or objects.
- Divergent Change / Shotgun Surgery — if one module changes for many reasons, or one change touches many modules, reorganize by feature.
- Feature Envy — functions that mostly poke another object’s data; move behavior to where the data lives.
- Data Clumps / Primitive Obsession — repeated primitive bundles; introduce value objects/types.
- Repeated Switch/Type Checks — repeated branching on types; prefer polymorphism or lookup tables.
- Duplicate Code — extract and reuse shared logic.
- Temporary Field / Lazy Class — fields only sometimes used or classes that do almost nothing; inline or remove.
- Middle Man / Inappropriate Intimacy — over-forwarding or deep coupling; simplify or reduce coupling.
- Comments-as-deodorant — comments explaining complex code instead of simplifying it; refactor for clarity.

## Testing Expectations
- Unit tests for new parser utilities and decompression helpers.
- Integration tests updated when parser outputs change (golden files).
- E2E tests touched when user flows change (list → download → render).
- Keep CI green: lint, typecheck, tests, and build must pass.

## Performance Notes
- Keep parsing off the main thread; avoid synchronous heavy work in UI.
- Prefer streaming/chunked operations for large buffers.
- Watch for unnecessary copies of ArrayBuffers/TypedArrays.

## Security Notes
- Worker must validate URLs against allowlist and HTTPS only.
- Enforce max file size; handle Range requests correctly.
- Strip or block unexpected headers (e.g., Set-Cookie) in proxy responses.

## Style
- Preact + TypeScript; use functional components and hooks.
- Consistent error handling: typed errors, user-friendly messages.
- Prefer small, composable modules over large files.

## When Unsure
- Ask for clarification or add TODO with context; do not guess on protocol or binary formats.
