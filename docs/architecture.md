# Architecture

## System Boundaries
The application is browser-only and runs as Blazor WebAssembly with JavaScript interop for viewer and binary-heavy operations.

## Hard Constraints
- No server-side ingest, parsing, storage, or preview pipeline.
- No backend API that receives, proxies, or redistributes mod files.
- All mod bytes remain local to the user's browser/device.

## Hosting Model
- The app is hosted on GitHub Pages as a static site.
- Deployment must remain compatible with static hosting only.
- Client routing and static asset paths must work under the GitHub Pages site base path.

## Project Structure
- UI/orchestration runtime: [src/BlazorApp/BlazorApp.csproj](../src/BlazorApp/BlazorApp.csproj)
- Parser runtime library: [src/RepoMod.Parser/RepoMod.Parser.csproj](../src/RepoMod.Parser/RepoMod.Parser.csproj)
- Parser unit tests: [tests/RepoMod.Parser.UnitTests/RepoMod.Parser.UnitTests.csproj](../tests/RepoMod.Parser.UnitTests/RepoMod.Parser.UnitTests.csproj)

## Subsystems

### 1) Ingest UI
- Accept zip uploads
- Show progress, validation, and parse status
- Trigger indexing and parse jobs

### 2) Archive Scanner
- Read zip entries
- Locate `.hhh` files
- Extract basic metadata from naming conventions and available bundle/prefab info

### 3) Storage (IndexedDB)
- Persist mod records, file blobs, parse artifacts, and schema version
- Support migration and cleanup routines

### 4) Parser Core (.NET library)
- Parse Unity AssetBundle payloads used by MoreHead-style `.hhh` assets
- Parse required `unitypackage` inputs for base avatar/textures
- Emit stable DTOs for composition (independent of UI)

### 5) Composition + GLB Export
- Resolve avatar + N cosmetic selections
- Build merged scene representation
- Generate GLB in-browser for rendering

### 6) Viewer (three.js)
- Load generated GLB
- Provide camera, lighting defaults, and model replacement lifecycle
- Dispose resources between previews

## Interop Contract Principles
- Keep parser/composition domain models in .NET
- Keep viewer-specific logic in JS/three.js
- Minimize large binary round-trips across interop boundaries
- Keep all binary processing client-side only (no network transfer of mod content)
- Keep vendor-imported parser code isolated behind parser library abstractions

## Execution Flow (MVP)
1. User uploads zip
2. Scanner finds `.hhh` bundles
3. Records + blobs stored in IndexedDB
4. Parser extracts required assets
5. User selects cosmetics
6. Composer builds final model
7. GLB rendered in three.js
