# Copilot Instructions

AI agents helping with this codebase should understand the architecture, development workflow, and project-specific constraints.

## Architecture Essentials

This is a **browser-first cosmetic viewer** for REPO game mods. Key architecture:

- **Frontend**: Vite + Preact + three.js; **Worker**: Cloudflare Workers proxy; **Parser**: UnityFS reader offloaded to Web Worker
- **Data flow**: User downloads mod from Thunderstore → Worker validates & proxies URL → Browser unzips with fflate → Web Worker parses `.hhh` AssetBundle → three.js renders 3D meshes
- **Monorepo structure** (pnpm workspaces): `apps/web`, `apps/worker`, `packages/{unity-ab-parser, thunderstore-client, renderer-three, utils}`
- **Critical design constraint**: All heavy parsing and decompression (LZ4, LZMA) happens in Web Worker, NOT main thread

## Development Workflow

**Common tasks:**

- `pnpm dev` — runs web app (port 5173) + worker (port 8787) in parallel
- `pnpm test` — runs vitest across all packages; golden files in `__tests__/fixtures/`
- `pnpm build` — TypeScript + Vite build; worker deploys via wrangler
- `cd apps/web && pnpm build && pnpm preview` — test production build locally

**Key files**:

- Worker routes: `apps/worker/src/index.ts` — implement `/api/mods` and `/proxy` endpoints
- Parser entry: `packages/unity-ab-parser/src/index.ts` — UnityFS reader, block decompression, object deserialization
- Renderer: `packages/renderer-three/src/index.ts` — maps Unity Standard → MeshStandardMaterial

## Priorities When Reviewing/Coding

- Correctness and safety first; style second.
- Flag potential performance or memory risks in the parser and rendering paths.
- Call out security risks in the Worker (allowlist, CORS, size limits).

## Monorepo & Package Responsibilities

- **unity-ab-parser**: Core binary parsing (UnityFS header, block decompression via lz4js/LZMA WASM, object deserialization). Must run in Web Worker, handle ArrayBuffer only.
- **thunderstore-client**: Typed API client for Thunderstore (mod list, download metadata). Used by Worker and web app.
- **renderer-three**: Converts parsed mesh/texture data to three.js Scene. Mesh vertices/normals → BufferGeometry; Material properties → MeshStandardMaterial.
- **utils**: Shared types (e.g., `MeshData`, `TextureData`), error handling, logging.
- **worker**: Cloudflare Workers endpoints; validates URLs against allowlist (Thunderstore, CDN only); proxies with CORS headers; sets Cache-Control.
- **web**: Vite + Preact app; fetches mod list from Worker, handles zip downloads, spawns Web Worker for parsing, renders with three.js.

## Anti-Patterns to Catch

- Unused code/exports, dead branches, and ignored promises.
- Floating promises or missing error handling in async code.
- **Synchronous heavy work on main thread**: Parser + decompression MUST run in Web Worker; never call parsing directly from Preact.
- Bypassing host allowlist or size limits in the Cloudflare Worker proxy; always validate URL scheme (HTTPS), reject redirects outside allowlist.
- Missing `AbortController` handling for fetch/parse flows; cancel in-flight work when user navigates away.
- Direct DOM mutations in Preact components (prefer state-driven updates); use Preact Signals for reactive state.
- Missing null/undefined guards on Thunderstore API responses (mod metadata may be incomplete).
- Using `any` without justification; prefer typed interfaces (e.g., `MeshData`, `TextureData` from utils package).
- Adding new dependencies without checking necessity or size impact; this is a static site—every byte counts.

## Parser & Binary Format Patterns

- **UnityFS header**: Bytes 0–31, big-endian (signature "UnityFS", version 6, block/directory offsets).
- **Block decompression**: Read `BlockInfo` (uncompressed size, compressed size, flags), decompress payload (LZ4 flag=0x40, LZMA flag=0xC0).
- **Object directory**: List of (class ID, object ID, offset, size, type index); deserialize only requested types (Mesh, Texture2D, Material).
- **Mesh**: vertices (Vector3[]), normals (Vector3[]), tangents (Vector4[]), triangles (uint32[]).
- **Texture2D**: raw pixel data (RGBA32, BC1/BC3, PNG/JPEG embedded); size in (width, height).
- **Material**: shader references, texture bindings, float properties (metallic, roughness).
- When adding support for new Unity object types, add unit tests with synthetic buffers and integration tests with real bundles.

## Web Worker Communication Pattern

```typescript
// Main thread: spawn and post work
const worker = new Worker('parser.js', { type: 'module' });
worker.postMessage({ action: 'parse', buffer: arrayBuffer }, [arrayBuffer]);
worker.onmessage = ({ data: result }) => {
  if (result.error) handleError(result.error);
  else updateScene(result.meshes, result.textures);
};
worker.onerror = (err) => telemetry.logError(err);

// Worker: listen and respond
self.onmessage = ({ data: { action, buffer } }) => {
  try {
    const result = parseMod(buffer);
    self.postMessage({ success: true, ...result });
  } catch (err) {
    self.postMessage({ success: false, error: err.message });
  }
};
```

Always transfer ArrayBuffers (`[arrayBuffer]` second arg) to avoid copying. Catch and report errors back to main thread; don't throw silently.

## Code Smells in Context

- **Long Function in parser**: If a binary reader (e.g., `parseUnityFSHeader`, `deserializeMesh`) exceeds 100 lines, extract common patterns into helper functions (e.g., `readBEUint32`, `readString`).
- **Repeated decompression logic**: LZ4 and LZMA block handling similar; extract common `decompressBlock(flags, compData, expectedSize)`.
- **Divergent change in renderer**: If 3D asset changes affect both mesh creation and material setup, ensure both live in `renderer-three` package, not split between web and renderer.
- **Primitive obsession for binary offsets**: Don't pass raw `(offset, size)` tuples; create typed `BufferView { buffer, offset, size }`.
- **Type guards in multiple places**: If code checks `if (obj.type === 'Mesh')` in web, worker, _and_ renderer, use a discriminated union instead.
- **Comments explaining binary format**: If comments are parsing UnityFS structs, extract to `doc/specs/parser-pipeline.md` and link instead.

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
