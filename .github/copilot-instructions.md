# R.E.P.O. Mod Browser - AI Coding Agent Instructions

## Project Overview

Browser-based 3D viewer for R.E.P.O. cosmetic mods from Thunderstore. Built with **Blazor WebAssembly** (C#), **Cloudflare Worker** (JS proxy), and **Three.js** (rendering). Users browse mods, download Unity asset bundles (`.hhh` files), parse geometry client-side, and preview 3D meshes in-browser without launching the game.

**Key Principle**: Direct port from [UnityPy](https://github.com/K0lb3/UnityPy) Python implementation. Do NOT reverse-engineer Unity formats—copy proven parsing logic line-by-line.

## Architecture

```
User → Blazor WASM → Cloudflare Worker → Thunderstore API (repo community)
         ↓
    Download ZIP → Extract .hhh → Parse Unity bundle → Export Three.js → Render
```

### Components
- **Frontend**: Blazor WASM (C#/.NET 8) on GitHub Pages
- **Proxy**: Cloudflare Worker for CORS, caching (KV), API forwarding
- **Data Source**: Thunderstore API v1 (`/c/repo/api/v1/package/`) + experimental endpoints
- **Rendering**: Three.js via C# → JS interop (`wwwroot/js/meshRenderer.js`)

### Unity Asset Format
- **Bundle**: UnityFS (`.hhh` extension), multi-node structure
  - Node 0: SerializedFile (metadata, Mesh objects with ClassID 43)
  - Node 1: `.resS` resource (vertex/index binary data)
- **Mesh**: 20 fields, 4-byte aligned, references external `.resS` via `StreamingInfo`
- **Compression**: `PackedBitVector` for indices/attributes (bit-packed with scaling)

## Critical Porting Rules

### DO NOT
- ❌ Learn Unity format from scratch or experiment with parsing
- ❌ Deviate from UnityPy reference implementation
- ❌ Skip 4-byte alignment after byte arrays or bool triplets (causes data corruption)
- ❌ Treat `m_DataSize` as a length field (it's raw binary data)

### DO
- ✅ Port logic directly from UnityPy Python files (see [References](#key-references))
- ✅ Validate C# output against Python reference outputs (JSON diff)
- ✅ Handle external `.resS` resources (vertex data is NOT inline)
- ✅ Use `System.Text.Json` with `JsonPropertyName` for Thunderstore DTOs (field names match v1 API exactly)

### Example Port Mapping
| Python File | C# Target | Purpose |
|------------|-----------|---------|
| `UnityPy/files/BundleFile.py` | `UnityAssetParser/Bundle/BundleFile.cs` | Parse UnityFS header/nodes |
| `UnityPy/classes/Mesh.py` | `UnityAssetParser/Classes/Mesh.cs` | Read 20-field Mesh structure |
| `UnityPy/helpers/PackedBitVector.py` | `UnityAssetParser/Helpers/PackedBitVector.cs` | Decompress bit-packed data |
| `UnityPy/helpers/MeshHelper.py` | `UnityAssetParser/Helpers/MeshHelper.cs` | Extract positions/indices |

## Project Structure (Planned)

```
BlazorModViewer/
├── Client/                          # WASM app
│   ├── Pages/Index.razor            # Mod browser
│   ├── Services/
│   │   ├── ThunderstoreService.cs   # API client
│   │   └── MeshRenderService.cs     # JS interop
│   └── wwwroot/js/meshRenderer.js   # Three.js wrapper
├── Shared/Models/                   # DTOs
└── UnityAssetParser/                # Core parsing library
    ├── Bundle/                      # BundleFile, SerializedFile
    ├── Classes/Mesh.cs
    └── Helpers/                     # PackedBitVector, MeshHelper
```

## Thunderstore API Integration

### Base Endpoints (via Worker)
- Package list: `GET /api/packages` → proxy `/c/repo/api/v1/package/`
- Categories: `GET /api/categories` → proxy experimental categories API
- Readme/Changelog: `GET /api/package/{ns}/{name}/{ver}/(readme|changelog)`
- Download meta: `HEAD /api/download/{ns}/{name}/{ver}` → return `Content-Length` + filename

### Key Data Fields
- **ThunderstorePackage**: `name`, `owner`, `categories`, `icon` (Uri), `rating_score`, `versions[]`
- **PackageVersion**: `version_number`, `download_url` (Uri), `file_size`, `dependencies[]`
- **Categories** (16 total): Cosmetics, Valuables, Items, Weapons, Levels, Monsters, Drones, Upgrades, Audio, Server-side, Client-side, Misc, Libraries, Tools, Modpacks, Mods

### Caching Strategy
- Package list: Cloudflare KV, TTL 5m, key `repo:package_list`
- Categories: KV, TTL 1h, key `repo:categories`
- Parsed assets: Browser IndexedDB, key `{mod_full_name}:{asset_filename}`

### CORS & Headers
- Worker adds `Access-Control-Allow-Origin: *` (dev), locked origin (prod)
- Upstream requests: `User-Agent: RepoModViewer/0.1 (+https://atniptw.github.io)`, `Accept: application/json`

## Development Workflow

### Build & Test
- CI: Build Blazor, run tests, validate parsing vs. Python reference (see [docs/Workflow.md](docs/Workflow.md))
- Tests: Unit (binary reader, PackedBitVector, Mesh fields), Integration (ZIP → mesh → Three.js)

### Validation Approach
1. Parse `.hhh` with C# → serialize to JSON
2. Parse same `.hhh` with UnityPy Python → serialize to JSON
3. JSON diff → assert exact match for critical fields (tolerate float precision)
4. Test fixtures: [Cigar (220 verts), FrogHatSmile (500), BambooCopter (533), Glasses (~600)]

### Deployment
- Frontend: GitHub Pages (`docs/` folder), GitHub Actions on push to main
- Worker: Wrangler CLI, routes TBD (`*.workers.dev` for dev)

## Common Pitfalls

1. **Alignment**: 4-byte align after byte arrays and bool triplets (use padding calculation)
2. **External Data**: Vertex data is in `.resS` node, NOT inline—resolve `StreamingInfo.Path`
3. **PackedBitVector**: Apply scaling formula `value = int * (range / ((1 << bit_size) - 1)) + start`
4. **Index Format**: Check `IndexBuffer` size vs. vertex count to pick `Uint16` vs `Uint32`

## Three.js Interop Contract

### C# → JS Geometry Data
- **positions**: `Float32Array` (length `3 * vertexCount`, XYZ flat)
- **indices**: `Uint16Array | Uint32Array` (length `3 * triangleCount`, triangle list)
- **normals** (optional): `Float32Array` (length `3 * vertexCount`)
- **uvs** (optional): `Float32Array` (length `2 * vertexCount`)
- **groups** (optional): `{ start: int, count: int, materialIndex: int }[]` for submeshes

### JS API (`meshRenderer.js`)
- `init(canvasId, options)` → setup scene, camera, controls
- `loadMesh(geometry, groups?, materialOpts?)` → build BufferGeometry, return mesh ID
- `updateMaterial(meshId, { color, wireframe, metalness, roughness })`
- `clear()` / `dispose(meshId?)` → cleanup resources

## Key References

- **UnityPy (Python)**: https://github.com/K0lb3/UnityPy — Source of truth for all parsing logic
- **Your Fork**: https://github.com/atniptw/UnityPy/blob/master/PORTING_NOTES.md — Blazor-specific notes
- **Rust POC**: https://github.com/atniptw/UnityPy/tree/master/rust_wasm_port — Proof of concept (reference only)
- **Thunderstore API**: https://new.thunderstore.io/api/docs
- **R.E.P.O. Community**: https://new.thunderstore.io/c/repo/ (slug: `repo`)

## Documentation Map

Detailed design docs in [docs/](docs/):
- [Architecture.md](docs/Architecture.md) — System overview, data flow, key decisions
- [UnityParsing.md](docs/UnityParsing.md) — Bundle structure, Mesh parsing, renderable checks
- [BlazorUI.md](docs/BlazorUI.md) — Components, routing, services, file indexing
- [CloudflareWorker.md](docs/CloudflareWorker.md) — Endpoints, CORS, caching, skeleton code
- [DataModels.md](docs/DataModels.md) — DTOs, serialization, Three.js schema
- [ThunderstoreAPI.md](docs/ThunderstoreAPI.md) — Endpoints, categories, rate limits
- [TestingStrategy.md](docs/TestingStrategy.md) — Fixtures, unit/integration tests, validation
- [Deployment.md](docs/Deployment.md) — GitHub Pages, Cloudflare Worker, CDN
- [Workflow.md](docs/Workflow.md) — CI/CD, agent tasks, build commands

## Project Status

**Phase**: Planning → Implementation starting
- ✅ Modular design documentation established
- ✅ Rust POC validated entire pipeline (parsing → Three.js rendering works)
- 🔄 Blazor project structure TBD
- 🔄 C# porting from UnityPy in progress (priority: Bundle parser → Mesh parser → MeshHelper)

---

**When implementing**, always:
1. Reference the specific Python file/lines from UnityPy
2. Port logic verbatim (no "learning" or experimentation)
3. Validate output against Python reference (JSON diff)
4. Document any deviations or C#-specific adaptations
