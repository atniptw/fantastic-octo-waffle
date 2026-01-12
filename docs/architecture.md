# Architecture

## Overview

The REPO cosmetic viewer is a **static web application** with a minimal **Cloudflare Worker proxy backend**. All heavy lifting (mod parsing, 3D rendering) happens in the user's browser.

**Tech Stack:**

- **Frontend:** Vite + Preact + Signals + three.js + TypeScript
- **Worker:** Cloudflare Workers + TypeScript + Miniflare
- **Package & Compression:** fflate (zip), lz4js (LZ4), WASM LZMA (LZMA decompression)
- **Testing:** Vitest, Playwright, Web Workers + fixtures
- **Monorepo:** PNPM workspaces (web, worker, shared packages)

## User Flow

```
1. User opens app
   → Auto-select REPO community
   → Fetch mod list from Worker (/api/mods?community=repo)
   → Display grid of mods with search/sort

2. User clicks a mod
   → Show versions list
   → Click a version → fetch zip via Worker (/proxy?url=...)
   → Stream into browser, unzip with fflate

3. Parse & Render
   → Web Worker offloads UnityFS parsing
   → Extract meshes, textures, materials from .hhh bundles
   → Map Unity Standard → three.js MeshStandardMaterial
   → Build scene graph, render with orbit controls + lights

4. Inspect & Cache
   → User can toggle materials, inspect mesh names
   → Parsed data cached in IndexedDB per bundle version
```

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Browser / Web App                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐      ┌──────────────────┐                 │
│  │  Preact UI       │      │   three.js       │                 │
│  │  (List, Cards)   │──────│   (Canvas, Scene)│                 │
│  └────────┬─────────┘      └──────────────────┘                 │
│           │                                                      │
│  ┌────────▼──────────────────────────┐                          │
│  │  Thunderstore Client              │                          │
│  │  (query mods, download metadata)  │                          │
│  └────────┬──────────────────────────┘                          │
│           │                                                      │
│  ┌────────▼──────────────────────────┐                          │
│  │  HTTP Fetch via Worker Proxy      │                          │
│  │  (CORS-safe GET /api/*, /proxy)   │                          │
│  └────────┬──────────────────────────┘                          │
│           │                                                      │
└───────────┼──────────────────────────────────────────────────────┘
            │
    ┌───────▼──────────┐
    │ Cloudflare Worker│
    │ (Proxy + Headers)│
    └───────┬──────────┘
            │
    ┌───────▼──────────────────────┐
    │   Thunderstore API            │
    │   (mods, versions, download)  │
    └───────────────────────────────┘
```

**Browser-side Web Workers:**

```
Main Thread (UI)
    │
    ├─> Web Worker 1: UnityFS parser (input: ArrayBuffer; output: mesh/texture JSON)
    │   - UnityFS block decompression (LZ4, LZMA)
    │   - Object deserialization
    │
    ├─> Web Worker 2: Texture decode (input: raw texture data; output: ImageBitmap)
    │   - Canvas.createImageBitmap() for fast RGBA conversion
    │
    └─> Web Worker 3: (optional) glTF export/serialization
```

## Data Flow

### Mod Browsing

1. **Fetch mod list:** `GET /api/mods?community=repo&query=&page=1&sort=downloads`
2. **Worker proxies** to `new.thunderstore.io/api/cyberstacks/...`
3. **Response normalized** by Worker (CORS headers added; caching set to 5 min).
4. **UI displays cards** with mod name, author, version, description.

### Download & Unzip

1. **User clicks mod version.**
2. **Worker fetches** download URL (from Thunderstore API).
3. **Browser calls** `GET /proxy?url=https://cdn.thunderstore.io/file/{hash}.zip`
4. **Worker validates** URL against allowlist, forwards to CDN, streams response.
5. **Browser unzips** in chunks using fflate; streams to IndexedDB cache.

### Parsing

1. **Extract `.hhh` files** from zip.
2. **Post to Web Worker:** `{ action: 'parse', buffer: ArrayBuffer<.hhh> }`
3. **Worker:**
   - Reads UnityFS header (signature, version, flags).
   - Decompresses block sequence (LZ4/LZMA adapters).
   - Parses object directory.
   - Deserializes Mesh/Texture2D/Material objects.
   - Returns: `{ meshes: [...], textures: [...], materials: [...] }`
4. **Main thread:** Creates three.js Geometry + Materials + Mesh.

### Caching

- **HTTP:** Worker sets `Cache-Control: public, max-age=300` for API; downloads marked `immutable`.
- **In-browser:** IndexedDB stores parsed mesh/texture data per bundle SHA256 hash.
- **Local Storage:** Recently viewed mods (simple list of `{namespace}/{name}:{version}`).

## Error Handling

**Strategy:** Graceful degradation with user-facing toasts.

| Error                 | Handling                                                                    |
| --------------------- | --------------------------------------------------------------------------- |
| Network timeout       | Retry 3× with exponential backoff; fallback to cached version if available. |
| Invalid .hhh file     | Log error; display toast "This mod has an incompatible format."             |
| LZMA decompress fails | Suggest clearing browser cache; offer download of parser debug log.         |
| Texture decode fails  | Skip texture; render mesh with fallback color.                              |
| Memory limit exceeded | Halt parsing; suggest closing other tabs; display warning.                  |
| Worker crashes        | Fallback to main-thread parsing (slow); telemetry alert.                    |

**Telemetry:** Opt-in event logging for error types, parse duration, bundle size category.

## Security & Privacy

- **Worker allowlist:** Only proxy `thunderstore.io`, `new.thunderstore.io`, known CDN hosts. Block all others.
- **Input validation:** Validate URL scheme (HTTPS), reject redirects outside allowlist.
- **No PII:** Never log user mod preferences; metrics only include aggregate bundle sizes/types.
- **CSP:** Strict-dynamic, no unsafe-eval, same-origin frames.
- **Data flow:** Zips never touch server; all parsing happens in-browser.

## Performance Targets

- **Initial load:** < 2s (main app); < 300ms first paint.
- **Mod list:** < 500ms fetch + render.
- **Zip download:** Streamed; visual progress bar.
- **Parse time:** < 3s for typical cosmetic bundle.
- **Render:** 60 FPS with orbit controls + lights on mid-range laptop.
- **Memory:** Peak < 500MB during parse of large bundles.
