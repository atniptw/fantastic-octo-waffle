# R.E.P.O. Cosmetic Catalog Desktop Tool – Design Specifications

## 1. Project Overview
Players of **R.E.P.O.** use mods to add cosmetic heads and decorations. However:
- The game does not provide good mod search features.
- You must install mods before seeing cosmetics.
- Many cosmetic mods use the *MoreHead* format and store cosmetic files under:
  ```
  plugins/<plugin>/Decorations/*.hhh
  ```
- `.hhh` files are Unity asset bundles (UnityFS).

**Goal:** Build a Windows desktop tool that lets users browse all cosmetic mods, search them, and know which mods contain which cosmetics. Later, optionally support previewing cosmetics.

---

## 2. Target Platform
- Windows 10+
- Desktop application
- Framework options:
  - **Electron**
  - **Tauri**
  - **Unity**

---

## 3. Scope Levels

### Level 1 – Catalog & Search (Required)
The application will:
- Allow importing Thunderstore mod ZIP files.
- Scan each ZIP for:
  - `manifest.json`
  - `icon.png`
  - `.hhh` cosmetic files
- Parse metadata.
- Store results in a SQLite database.
- Present a searchable UI listing all cosmetics.

### Level 2 – Cosmetic Preview (Optional)
- Parse `.hhh` (UnityFS) bundles.
- Extract meshes and textures.
- Convert textures to PNG.
- Convert meshes to GLTF or display using Unity.
- Render previews in UI.

---

## 4. Data Model (SQLite)
### Table: mods
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK |
| mod_name | TEXT | From manifest |
| author | TEXT |
| version | TEXT |
| icon_path | TEXT |
| source_zip | TEXT |

### Table: cosmetics
| Column | Type |
|--------|------|
| id | INTEGER PK |
| mod_id | INTEGER FK |
| display_name | TEXT |
| filename | TEXT |
| hash | TEXT |
| type | TEXT |
| internal_path | TEXT |

### Table (Level 2): assets
| Column | Type |
|--------|------|
| cosmetic_id | INTEGER |
| mesh_path | TEXT |
| texture_path | TEXT |
| preview_image_path | TEXT |

---

## 5. Core Features
### Level 1
- Import multiple mod ZIPs
- Scan for `.hhh` files
- Extract metadata
- Store in SQLite
- Search/filter UI
- Display mod icons
- No rendering required

### Level 2
- Parse `.hhh` Unity files
- Extract textures and meshes
- Render in viewer
- Export assets

---

## 6. Technical Considerations
- `.hhh` uses UnityFS (Unity asset bundle format).
- Compatible libraries:
  - UnityPy (Python)
  - AssetRipper (C#)
  - uTinyRipper
  - AssetStudio

Rendering options:
- Unity app: easiest rendering path.
- Electron app: WebGL viewer requires GLTF export.

---

## 7. Future Enhancements
- Auto-download mods
- Deduplicate cosmetics
- Tagging
- Favorites
- Export cosmetic lists

---

## 8. Milestones
### Milestone A (Level 1)
- ZIP scanning
- Database creation
- Search UI

### Milestone B (Level 2)
- `.hhh` parsing
- Asset extraction

### Milestone C
- 3D preview system

---

## 9. Deliverables
- Source code
- Documentation
- Executable
- Tests

