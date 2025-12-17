# Agentic Coding Agent Prompt

## PROJECT TITLE
R.E.P.O. Cosmetic Mod Preview Tool (Browser-Based)

## SUMMARY
Build a browser-based web application hosted on GitHub Pages that allows users to upload Thunderstore mod ZIP files for R.E.P.O., extracts cosmetic metadata from `.hhh` UnityFS bundles, stores them in IndexedDB, and provides searchable UI with in-browser 3D preview rendering. Level 1 (ZIP upload & catalog) is required. Level 2 (3D preview/GIF generation) is the primary goal.

---

## LEVEL 1 – CORE REQUIREMENTS (MANDATORY)

### 1. Browser ZIP Upload & Processing
- HTML5 file input accepting multiple ZIP files
- Unzip files in browser using JSZip library
- Scan each ZIP for:
  - `manifest.json`
  - `icon.png`
  - `/plugins/<plugin>/Decorations/*.hhh`
- Use Web Workers to avoid blocking UI during extraction

### 2. Data Extraction
Extract mod info:
- mod name, author, version from manifest.json
- icon (store as Blob or Base64 in IndexedDB)
- ZIP filename and upload timestamp

Extract cosmetic info:
- filename
- inferred type (from filename pattern)
- SHA256 hash (using browser crypto API)
- internal ZIP path
- display name (derived from filename)

### 3. Storage – IndexedDB Schema
Object stores required:
- **mods**: id (UUID), mod_name, author, version, icon_data, source_zip_name, upload_timestamp
- **cosmetics**: id (UUID), mod_id (FK), display_name, filename, hash, type, internal_path
- **assets** (Level 2): id, cosmetic_id (FK), mesh_data (Blob), texture_data (Blob), preview_image (Blob), preview_gif (Blob)

### 4. UI Requirements
- Browser-based single-page application (React/Vue/Svelte)
- Upload button/drag-and-drop zone for ZIPs
- Search bar with real-time filtering
- Data grid listing cosmetics with:
  - Cosmetic name
  - Mod name
  - Type
  - Preview button
- Filters: by mod, by type, by upload date
- Responsive design (desktop + mobile)

---

## LEVEL 2 – PRIMARY GOAL (3D PREVIEW & GIF GENERATION)

### 5. UnityFS Parsing in Browser
- Parse `.hhh` UnityFS asset bundles using JavaScript/WASM
- Options:
  - JavaScript UnityFS parser (custom or library)
  - WASM-compiled UnityPy (if available)
  - unity-asset-parser npm package
- Extract textures as TypedArrays → convert to PNG via Canvas API
- Extract meshes → convert to GLTF JSON format

### 6. 3D Rendering & Preview
- Use Three.js for WebGL-based 3D rendering
- Load GLTF meshes with textures
- Interactive viewer:
  - Mouse drag to rotate
  - Mouse wheel to zoom
  - Reset view button
- Generate static preview images (PNG/WebP) from canvas
- Generate animated GIF previews using canvas capture

### 7. Preview Export
- Download individual preview images
- Download animated GIFs
- Bulk export all previews as ZIP
- Share preview via data URL (embed in URL)

---

## CONSTRAINTS
- Browser-only (no Node.js backend for core features)
- Must work offline after initial page load
- Must gracefully handle corrupt `.hhh` files
- Must support Chrome, Firefox, Edge, Safari (modern browsers)
- Hosted as static site on GitHub Pages
- All processing happens client-side (privacy-friendly)

---

## ACCEPTANCE CRITERIA

### Level 1 Complete When:
- Users can upload multiple ZIP files
- ZIPs are extracted and scanned in browser
- All cosmetics and mod info populate IndexedDB
- Search works instantly with filters
- Icons display correctly from uploaded data
- Duplicate scans do not create duplicate entries
- Corrupt ZIPs do not crash the application

### Level 2 Complete When:
- At least one `.hhh` mesh and texture extracted successfully
- 3D preview renders in browser using Three.js
- Users can rotate/zoom the preview
- Static preview images (PNG) can be generated and downloaded
- Animated preview GIFs can be generated
- Preview generation does not freeze the UI

---

## DELIVERABLES
- Source repository (GitHub)
- Live demo on GitHub Pages
- IndexedDB schema implementation
- README with user guide and developer setup
- Automated tests for ZIP scanning and IndexedDB operations
- Browser compatibility documentation

---

## EXECUTION STEPS FOR THE AGENT

1. **Setup project structure** for browser-based app (Vite + React/TypeScript)
2. **Implement file upload UI** with drag-and-drop support
3. **Integrate JSZip** for client-side ZIP extraction
4. **Build IndexedDB wrapper** with schema and migrations
5. **Implement ZIP scanner** to extract manifest.json, icons, .hhh files
6. **Implement metadata extraction** and SHA256 hashing
7. **Build importer** with duplicate detection (hash-based)
8. **Create catalog UI** with search, filters, and data grid
9. **Test with sample ZIPs** and verify data integrity
10. **(Level 2) Integrate UnityFS parser** for .hhh files
11. **(Level 2) Implement asset extraction** (meshes → GLTF, textures → PNG)
12. **(Level 2) Build Three.js preview viewer** with rotation/zoom
13. **(Level 2) Implement preview image generation** (canvas → PNG)
14. **(Level 2) Implement GIF generation** (canvas capture → GIF encoder)
15. **Configure GitHub Pages deployment** (Vite build → gh-pages)
16. **Write documentation** and user guide
17. **Add E2E tests** for upload → preview workflow

