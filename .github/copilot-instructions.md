# R.E.P.O. Cosmetic Catalog - AI Agent Instructions

## Project Overview
This is a browser-based web application hosted on GitHub Pages that fetches Thunderstore mod ZIP files for R.E.P.O. via the Thunderstore API, extracts cosmetic metadata from `.hhh` UnityFS bundles, and previews cosmetics with 3D rendering - all client-side in the browser.

## Architecture Philosophy
Two-level implementation approach:
- **Level 1 (Required)**: Thunderstore API integration, client-side ZIP extraction, IndexedDB storage, search UI
- **Level 2 (Primary Goal)**: In-browser UnityFS parsing, asset extraction, 3D preview with Three.js, GIF generation

## Core Data Flow
1. User browses/searches Thunderstore mods → App fetches mod ZIPs from Thunderstore API → JSZip extracts in browser using Web Workers
2. Scan for `manifest.json`, `icon.png`, `plugins/<plugin>/Decorations/*.hhh`
3. Extract metadata → Store in IndexedDB (`mods`, `cosmetics`, `assets` stores)
4. Parse `.hhh` files → Extract meshes/textures → Render with Three.js
5. Generate preview images/GIFs → Download or share

## Technology Constraints
- **Platform**: Modern browsers (Chrome, Firefox, Edge, Safari)
- **Framework**: React + TypeScript + Vite
- **ZIP Handling**: JSZip (client-side, no Node.js required)
- **Storage**: IndexedDB (browser-native)
- **3D Rendering**: Three.js + WebGL
- **Deployment**: GitHub Pages (static site only)
- **Asset Format**: `.hhh` files are UnityFS asset bundles
- **Must work entirely client-side** and handle corrupt `.hhh` files gracefully

## Critical File Patterns
- Cosmetic files: `plugins/<plugin>/Decorations/*.hhh`
- Mod metadata: `manifest.json` (contains mod_name, author, version)
- Mod icon: `icon.png` (root of ZIP)
- Internal structure: Each ZIP is a complete Thunderstore mod package

## Database Schema
See `design_specifications.md` Section 4 for complete schema. Key relationships:
- `mods` table: mod-level metadata (name, author, version, icon, source_zip)
- `cosmetics` table: per-cosmetic data (display_name, filename, SHA256 hash, type, internal_path, FK to mods)
- `assets` table (Level 2): extracted mesh/texture paths

## Development Workflow
Follow execution steps in `agentic_prompt.md`:
1. Create folder structure first
2. Implement ZIP scanning (core feature)
3. Build SQLite wrapper + schema with migrations
4. Implement importer with duplicate detection
5. Build UI shell with import button
6. Implement search + data grid with filters
7. Test with sample `.hhh` files
8. (Optional) Parse UnityFS with UnityPy/AssetRipper/AssetStudio
9. Package Windows executable

## UnityFS Parsing (Level 2)
For browser-based preview implementation:
- Compatible libraries: **unity-asset-parser** (JavaScript), WASM-compiled UnityPy (if available)
- Custom JavaScript UnityFS parser for `.hhh` files
- Extract textures → Convert to PNG using Canvas API
- Extract meshes → Convert to GLTF JSON format
- Rendering: Three.js provides excellent WebGL rendering with GLTF support
- GIF generation: Canvas capture + GIF encoder library

## Acceptance Criteria
- Users can browse and search mods from Thunderstore
- App successfully fetches mod ZIPs from Thunderstore API
- ZIPs extract in browser without freezing UI (Web Workers)
- All cosmetics populate IndexedDB with correct metadata
- Search works instantly with filters
- Icons display correctly from fetched mod data
- Duplicate mod imports don't create duplicate entries
- Corrupt `.hhh` files do not crash the application
- (Level 2) At least one `.hhh` mesh/texture extracted and previewed
- (Level 2) Preview images and GIFs can be generated and downloaded

## Key Deliverables
- Live demo on GitHub Pages
- IndexedDB schema implementation
- README with user guide and developer setup
- Automated tests for ZIP scanning and IndexedDB operations
- Browser compatibility documentation
