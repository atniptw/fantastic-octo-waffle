# Project Roadmap

## Overview

Phased delivery from MVP (Worker proxy + mod list) to a fully featured cosmetic viewer with multi-game support, caching, and rich UI.

Each phase includes acceptance criteria and estimated effort.

---

## Phase 0: Project Setup (P0)

**Goal:** Establish baseline tooling, structure, and automation so later phases move fast without rework.

**Deliverables:**
- Monorepo scaffold (apps/web, apps/worker, packages/*) with pnpm workspaces.
- Tooling baseline: TypeScript strict, ESLint + Prettier, Vitest, Playwright, Vite config for Preact, Wrangler/Miniflare config for Worker.
- GitHub Pages wiring plan (gh-pages branch or Pages action) and Cloudflare deploy plan (wrangler config + secrets list).
- CI skeleton: install, format check, lint, typecheck, unit/integration tests, build steps defined (even if tests are minimal now).
- Documentation refreshed: README, docs/README, architecture, parser/worker specs, CI design, fixture policy.

**Acceptance Criteria:**
- [ ] Repo installs cleanly on Node 24 with `pnpm install`.
- [ ] `pnpm format:check`, `pnpm lint`, `pnpm typecheck`, `pnpm test` run (tests can be placeholder but pipelines exist).
- [ ] Vite dev server runs (`pnpm dev` in apps/web) and shows stub UI.
- [ ] Miniflare dev server runs (`pnpm dev` in apps/worker) and responds 200 on a health endpoint.
- [ ] CI workflow file(s) exist covering format, lint, typecheck, unit/integration, build; E2E step planned/flagged.
- [ ] Deploy paths documented for GitHub Pages (web) and Cloudflare Worker (proxy).
- [ ] Copilot instructions and issue/PR templates present.

**Dependencies:** None.

**Effort:** ~1 week (1 dev).

**Risks:** Minimal; risk of tool churn if not decided upfront. Mitigation: lock Node 24, pnpm, strict TS/ESLint config now.

---

## Phase 1: Worker Proxy & Mod List (MVP)

**Goal:** Validate the tech stack; deliver a working mod browser backed by Thunderstore API.

**Deliverables:**
- Cloudflare Worker: allowlisted proxy for `/api/mods` and `/api/mod/.../versions`.
- Web app: Vite + Preact scaffold; list mods for REPO community; basic search/sort UI.
- Local dev: Miniflare for Worker; Vite dev server; simple GitHub Actions CI.

**Acceptance Criteria:**
- [ ] Worker deploys to Cloudflare without errors.
- [ ] `/api/mods?community=repo` proxies Thunderstore and returns normalized JSON.
- [ ] Web app loads in < 2s; displays 20+ mods on load.
- [ ] Search and sort work; pagination implemented.
- [ ] CORS headers present on all Worker responses.
- [ ] Rate limiting (100/min API) enforced.
- [ ] Miniflare local dev works; GitHub Actions CI builds and tests Worker.
- [ ] TypeScript strict mode enabled; ESLint + Prettier passing.

**Dependencies:** None external; leverages existing Thunderstore API.

**Effort:** ~2 weeks (1 dev).

**Risks:** Thunderstore API schema changes; rate limiting strategy.

---

## Phase 2: UnityFS Parsing & Basic Rendering

**Goal:** Download and parse real `.hhh` bundles; render meshes with placeholder materials.

**Deliverables:**
- Zip download via Worker `/proxy?url=...`; stream to browser with progress UI.
- Web Worker offload for UnityFS parsing.
- Binary readers, block decompression (LZ4 + LZMA), object directory parsing.
- Minimal Mesh deserializer; three.js scene assembly.
- Golden test fixtures; integration tests for real small bundles.

**Acceptance Criteria:**
- [ ] `/proxy?url=https://cdn.thunderstore.io/...` downloads zip; CORS + Range support.
- [ ] Zip unzipped in browser via fflate; `.hhh` files extracted.
- [ ] Web Worker parses UnityFS header + blocks for real bundle (e.g., Masaicker/MoreHead).
- [ ] Both LZ4 and LZMA decompression work; no memory leaks.
- [ ] Parse time < 3s for typical 20 MB bundle on mid-range laptop.
- [ ] Mesh meshes render in three.js with solid color material.
- [ ] Integration test passes for fixture bundle; golden file matches.
- [ ] Error handling: friendly toasts for unsupported bundles, network errors.
- [ ] UI shows parse progress (0–100%).

**Dependencies:** lz4js (npm), WASM LZMA module (pre-built or compiled).

**Effort:** ~4 weeks (1–2 devs).

**Risks:** LZMA WASM build/compatibility; memory limits on large bundles; parser correctness edge cases.

---

## Phase 3: Texture & Material Support

**Goal:** Render textured meshes with PBR-ish materials; improve visual fidelity.

**Deliverables:**
- Texture2D deserializer; RGBA32/DXT format support; PNG/JPEG decode in Web Worker.
- Material deserializer; Unity Standard → three.js MeshStandardMaterial mapping.
- Metallic/smoothness maps; normal map support.
- Lighting setup (HDRI or IBL); orbit controls with mouse/touch.
- Performance optimization: buffer geometry batching, texture atlasing (optional Phase 4).

**Acceptance Criteria:**
- [ ] Texture2D objects parsed; PNG/JPEG formats decoded without blocking main thread.
- [ ] DXT textures recognized; GPU decompression via three.js CompressedTexture.
- [ ] Materials mapped correctly; base color, normal, metallic, roughness applied.
- [ ] Lighting is flattering (e.g., soft directional + HDRI).
- [ ] Orbit controls responsive; 60 FPS on target devices (browser profiler confirms).
- [ ] Memory peak for typical bundle < 500 MB.
- [ ] E2E test for full flow (list → download → parse → render) passes.
- [ ] Crunch textures detected and shown as "not yet supported" (graceful degradation).

**Dependencies:** Three.js examples (TangentSpaceNormalMapGenerator if needed).

**Effort:** ~3 weeks (1–2 devs).

**Risks:** Texture format edge cases; three.js material shaders correctness; performance tuning.

---

## Phase 4: Performance & Error Handling

**Goal:** Optimize parse speed, caching, and resilience; polish error UX.

**Deliverables:**
- IndexedDB caching of parsed bundles (keyed by mod version hash).
- Local Storage for recent mods.
- Local-only debug log exporter (opt-in): capture parse time, bundle size, error types; user can download JSON when filing GitHub issues. No remote telemetry.
- Error diagnostics: export parser logs, retry logic with backoff.
- Browser memory profiler integration (debugging aid).

**Acceptance Criteria:**
- [ ] Parsed bundle cached in IndexedDB; re-visit < 100ms (skip parse).
- [ ] Recent mods listed; deep linking to mod/version works.
- [ ] Local debug log export available (JSON), capturing parse durations, bundle sizes, error types; no data leaves the browser unless user attaches it to an issue.
- [ ] Network errors trigger retry (3× backoff); user can manually retry.
- [ ] OOM errors show helpful message: "Close other tabs; clear cache."
- [ ] Parser can be cancelled mid-stream (AbortController).
- [ ] Debug mode available (Dev Tools → export logs).

**Dependencies:** None new.

**Effort:** ~2 weeks (1 dev).

**Risks:** IndexedDB quota limits; service worker complexity.

---

## Phase 5: Community & UI Polish

**Goal:** Multi-community support; rich UI; cosmetic mod inspector.

**Deliverables:**
- Community selector dropdown (v2, ROUNDS, etc.).
- Mod detail page: description, author, changelog, dependencies.
- Cosmetic inspector: toggle visibility of submeshes, inspect material names, download model as glTF.
- Improved styling + responsive design.
- Accessibility audit (WCAG 2.1 AA).

**Acceptance Criteria:**
- [ ] User can select different communities (v2, ROUNDS, etc.); list refreshes.
- [ ] Each game community uses correct asset parser (Unity version + format may vary).
- [ ] Mod detail page loads; displays README (markdown rendered), download stats.
- [ ] Inspector sidebar: toggle submeshes on/off; list materials.
- [ ] Export glTF for external model editors.
- [ ] Responsive design: works on tablet + mobile (touch controls).
- [ ] WCAG 2.1 AA audit passes (Lighthouse).

**Dependencies:** Markdown renderer (e.g., `marked`); glTF exporter (three.js examples).

**Effort:** ~3 weeks (1–2 devs, design review).

**Risks:** Multi-game parser complexity; glTF export correctness.

---

## Success Criteria (Overall)

- [ ] Static web app loads in < 2s; renders at 60 FPS.
- [ ] Typical cosmetic mod (20–50 MB) parses and displays in < 3s.
- [ ] Worker CORS proxy reliable; 99.5% uptime.
- [ ] No customer-reported security issues.
- [ ] > 80% test coverage; all tests pass in CI.
- [ ] Documentation complete (architecture, API, parser spec, dev guide).
- [ ] Community feedback incorporated (Discord/GitHub discussions).

---

## Timeline Estimate

| Phase | Duration | Cumulative | Dependencies |
|-------|----------|------------|-|
| 1 | 2 weeks | 2 weeks | None |
| 2 | 4 weeks | 6 weeks | Phase 1 |
| 3 | 3 weeks | 9 weeks | Phase 2 |
| 4 | 2 weeks | 11 weeks | Phase 3 |
| 5 | 3 weeks | 14 weeks | Phase 4 |

**Concurrent work:** Phases 1 & 2 can overlap (API + parser in parallel).

---

## Future Enhancements (Post-MVP)

- **Crunch texture support:** Add crunch decoder for legacy mods.
- **Animation support:** Parse AnimationClip; preview cosmetic animations.
- **Mod dependencies:** Show which mods depend on which; validate compatibility.
- **Server-side caching:** Cache parsed glTF on CDN to skip parse on popular mods.
- **AR preview:** ARCore/ARKit integration for previewing cosmetics in real-world context.
- **Community contributions:** Accept user-submitted cosmetic reviews/screenshots.


