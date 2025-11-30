# R.E.P.O. Cosmetic Catalog - AI Agent Instructions

## Project Overview
This is a Windows desktop tool that scans Thunderstore mod ZIP files for R.E.P.O. game cosmetic mods, extracts metadata from `.hhh` UnityFS bundles, stores them in SQLite, and provides searchable UI.

## Architecture Philosophy
Two-level implementation approach:
- **Level 1 (Required)**: ZIP scanning, metadata extraction, SQLite storage, search UI
- **Level 2 (Optional)**: UnityFS parsing, asset extraction, 3D preview rendering

## Core Data Flow
1. Import mod ZIPs → Scan for `manifest.json`, `icon.png`, `plugins/<plugin>/Decorations/*.hhh`
2. Extract metadata → Store in SQLite (`mods`, `cosmetics`, `assets` tables)
3. Present searchable catalog → Filter/search without requiring mod installation

## Technology Constraints
- **Platform**: Windows 10+ only
- **Framework Options**: Electron, Tauri, or Unity (choose one based on preview requirements)
- **Database**: SQLite with specific schema (see `design_specifications.md`)
- **Asset Format**: `.hhh` files are UnityFS asset bundles
- **Must work offline** and handle corrupt `.hhh` files gracefully

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
If implementing preview features:
- Compatible libraries: **UnityPy** (Python), **AssetRipper** (C#), uTinyRipper, AssetStudio
- Extract textures → Convert to PNG
- Extract meshes → Convert to GLTF (for Electron/Tauri) or use Unity native mesh format
- Rendering: Unity provides easiest path; Electron requires WebGL viewer

## Acceptance Criteria
- ZIPs import successfully without duplicates on rescan
- All cosmetics populate database with correct metadata
- Search works instantly with filters
- Icons display correctly
- Corrupt `.hhh` files do not crash the application
- (Level 2) At least one `.hhh` mesh/texture extracted and previewed

## Key Deliverables
- Windows desktop executable (packaged installer)
- SQLite schema + migration scripts
- README with setup/build/run instructions
- Automated tests for ZIP scanning and DB insertion
