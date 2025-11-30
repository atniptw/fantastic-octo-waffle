# Agentic Coding Agent Prompt

## PROJECT TITLE
R.E.P.O. Cosmetic Mod Catalog + Optional Preview Tool

## SUMMARY
Build a Windows desktop application that scans Thunderstore mod ZIP files for R.E.P.O., extracts cosmetic metadata from `.hhh` UnityFS bundles, stores them in SQLite, and provides a searchable UI. Level 1 is required. Level 2 (preview rendering) is optional.

---

## LEVEL 1 – CORE REQUIREMENTS (MANDATORY)
### 1. ZIP Input
Scan ZIPs for:
- `manifest.json`
- `icon.png`
- `/plugins/<plugin>/Decorations/*.hhh`

### 2. Data Extraction
Extract mod info:
- mod name
- author
- version
- icon
- ZIP path

Extract cosmetic info:
- filename
- inferred type (from filename)
- SHA256 hash
- internal ZIP path
- display name

### 3. Storage – SQLite Schema
Tables required:
- **mods**: id, mod_name, author, version, icon_path, source_zip
- **cosmetics**: id, mod_id, display_name, filename, hash, type, internal_path

### 4. UI Requirements
- Desktop app (Electron, Tauri, or Unity)
- Import ZIP(s) button
- Search bar
- Data grid listing cosmetics with filters

---

## LEVEL 2 – OPTIONAL FEATURES
- Parse `.hhh` UnityFS bundles
- Extract textures (PNG)
- Extract meshes (GLTF or native Unity mesh)
- Render previews via Unity or WebGL

---

## CONSTRAINTS
- Windows-only
- Must work offline
- Must gracefully handle corrupt `.hhh` files
- No assumptions beyond documented folder structure

---

## ACCEPTANCE CRITERIA
The job is complete when:
- ZIPs import successfully
- All cosmetics and mod info populate database
- Search works instantly
- Icons display correctly
- Duplicate scans do not create duplicates
- Corrupt `.hhh` files do not crash the tool

Optional Level 2 acceptance:
- At least one `.hhh` mesh and texture extracted successfully
- Preview image or 3D viewer rendered

---

## DELIVERABLES
- Source repository
- Windows desktop executable
- SQLite schema + migrations
- README with setup/build/run instructions
- Automated tests for ZIP scanning and DB insertion

---

## EXECUTION STEPS FOR THE AGENT
1. Create folder structure
2. Implement ZIP scanning
3. Build SQLite wrapper + schema
4. Implement importer
5. Build UI shell
6. Implement search + data grid
7. Test with sample `.hhh` files
8. (Optional) Add UnityFS parsing + preview system
9. Package Windows executable

