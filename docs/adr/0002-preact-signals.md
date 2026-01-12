# ADR 0002: Preact + Signals for UI Framework

## Status
Accepted

## Context
The project is largely static with islands of interactivity (list, selection, 3D viewer). We need a lightweight UI framework that doesn't bloat the bundle given the focus on fast load times.

## Decision
Use **Preact** with **Preact Signals** for reactive state and **Preact Router** for navigation.

## Rationale
- **Bundle size:** Preact (~3KB gzipped) vs. React (~40KB gzipped) saves ~37KB, critical for a static site.
- **API compatibility:** Preact has the same JSX and hooks API as React; easy to switch if needed.
- **Signals:** Preact Signals provide fine-grained reactivity without the overhead of virtual-DOM diffing for every state change.
- **Performance:** Minimal overhead allows the browser to focus on three.js rendering, the real CPU consumer.

## Alternatives Considered
1. **React:** Larger bundle, but more ecosystem support. Can revisit in Phase 5 if UX complexity demands it.
2. **Vanilla JS or vanilla Web Components:** Possible, but loses type safety and modern dev experience.
3. **Svelte:** Smaller bundle than React, but less API parity with React; would create friction.

## Consequences
- Smaller ecosystem (fewer third-party component libraries).
- All React libraries must be checked for Preact compatibility (usually a `preact/compat` wrapper).
- Signals require a mindset shift from React hooks for global state; very clean for this use case.

## Follow-up
- If complex form/interactive UX emerges in Phase 5, consider upgrading to React for larger ecosystem.
- Document Preact patterns and gotchas for new contributors in [docs/dev-guide.md](../dev-guide.md).