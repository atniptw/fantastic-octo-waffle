# R.E.P.O. Cosmetic Catalog Browser Tool – Design Specifications

## 1. Project Overview
Players of **R.E.P.O.** use mods to add cosmetic heads and decorations. However:
- The game does not provide good mod search features.
- You must install mods before seeing cosmetics.
- Many cosmetic mods use the *MoreHead* format and store cosmetic files under:
  ```
  plugins/<plugin>/Decorations/*.hhh
  ```
- `.hhh` files are Unity asset bundles (UnityFS).

**Goal:** Build a browser-based web application hosted on GitHub Pages that fetches mod ZIPs from Thunderstore API, extracts cosmetic metadata, and previews cosmetics directly in the browser.

---

## 2. Target Platform
- **Browser-based** (Chrome, Firefox, Edge, Safari)
- Static web application hosted on **GitHub Pages**
- No backend server required for initial version
- Client-side only (JavaScript/TypeScript)
- Cross-platform (Windows, macOS, Linux, mobile)

---

## 3. Scope Levels

### Level 1 – Thunderstore Integration & Metadata Extraction (Required)
The application will:
- Fetch mod packages from Thunderstore API
- Allow users to browse/search available R.E.P.O. mods
- Download mod ZIP files from Thunderstore CDN
- Unzip files entirely in the browser using JSZip
- Scan each ZIP for:
  - `manifest.json`
  - `icon.png`
  - `.hhh` cosmetic files under `plugins/<plugin>/Decorations/`
- Parse metadata
- Store results in IndexedDB (browser local storage)
- Present a searchable UI listing all cosmetics

### Level 2 – In-Browser Cosmetic Preview (Primary Goal)
- Parse `.hhh` (UnityFS) bundles in the browser using UnityFS parsers
- Extract meshes and textures
- Convert textures to PNG/WebP
- Convert meshes to GLTF or display using Three.js/Babylon.js
- Render 3D previews directly in the browser
- Generate preview images/GIFs for each cosmetic

---

## 4. Data Model (IndexedDB)

Browser-based storage using IndexedDB instead of SQLite:

### Object Store: mods
| Field | Type | Description |
|-------|------|-------------|
| id | string (key) | UUID |
| mod_name | string | From manifest |
| author | string |
| version | string |
| icon_data | Blob/string | Base64 or Blob |
| source_zip_name | string | Original filename |
| upload_timestamp | number | When uploaded |

### Object Store: cosmetics
| Field | Type |
|-------|------|
| id | string (key) | UUID |
| mod_id | string | Foreign key to mods |
| display_name | string |
| filename | string |
| hash | string | SHA256 |
| type | string |
| internal_path | string |

### Object Store: assets (Level 2)
| Field | Type |
|-------|------|
| id | string (key) | UUID |
| cosmetic_id | string |
| mesh_data | Blob | GLTF binary |
| texture_data | Blob | PNG/WebP |
| preview_image | Blob | Generated preview |
| preview_gif | Blob | Animated preview (optional) |

---

## 5. Core Features

### Level 1 - Thunderstore Integration & Browser-Based Catalog
- Browse and search R.E.P.O. mods on Thunderstore
- Fetch mod ZIPs directly from Thunderstore API/CDN
- Unzip in browser using JSZip library
- Scan for `.hhh` files and metadata
- Extract metadata and compute SHA256 hashes
- Store in IndexedDB
- Search/filter UI with instant updates
- Display mod icons from uploaded data
- Export/import catalog data for backup

### Level 2 - In-Browser Preview & Rendering
- Parse `.hhh` Unity asset bundles in browser
- Extract textures and meshes client-side
- Render 3D previews using Three.js or Babylon.js
- Generate static preview images (PNG/WebP)
- Generate animated preview GIFs using canvas/WebGL capture
- Download preview images individually or in bulk
- Share preview URLs (with embedded data URLs)

### Level 3 - API Integration (Optional)
- Fetch mod metadata from Thunderstore API
- Download mods via CORS proxy
- Auto-import from mod URLs
- Browse Thunderstore catalog without leaving app

---

## 6. Technical Considerations

### Browser ZIP Handling
- Use **JSZip** for client-side ZIP extraction
- Modern browsers support large file handling (2GB+ via File API)
- Memory-efficient streaming for large ZIPs
- Handle corrupt/incomplete uploads gracefully

### UnityFS Parsing in Browser
- `.hhh` uses UnityFS (Unity asset bundle format)
- Browser-compatible parsers:
  - **unity-asset-parser** (JavaScript port)
  - WASM-compiled UnityPy (if available)
  - Custom JavaScript UnityFS parser
- Extract textures as TypedArrays → convert to PNG via Canvas API
- Extract meshes → parse to GLTF JSON format

### 3D Rendering Options
- **Three.js** - Lightweight, well-documented, excellent GLTF support
- **Babylon.js** - More feature-rich, slightly larger bundle
- **WebGL native** - Maximum control, more complex
- **Recommendation**: Three.js for balance of features and bundle size

### Storage Constraints
- IndexedDB typically limited to 50% of available disk space
- Quota API available to request more storage
- Users can clear storage if needed
- Implement storage quota monitoring UI

### Performance Considerations
- Use Web Workers for ZIP extraction to avoid blocking UI
- Use Web Workers for UnityFS parsing
- Lazy-load 3D rendering library only when needed
- Implement progressive loading for large catalogs
- Cache parsed assets to avoid re-processing

### GitHub Pages Deployment
- Static site only (no server-side code)
- Build process: Vite → dist/ → gh-pages branch
- All processing happens client-side
- Service worker for offline support (optional)
- CDN automatically handles global distribution

---

## 7. Future Enhancements
- **Thunderstore API integration** - Direct mod downloading
- **CORS proxy server** - For fetching mods from Thunderstore
- **Cosmetic comparison** - Side-by-side preview
- **Tagging system** - User-created tags for organization
- **Favorites/collections** - Bookmark favorite cosmetics
- **Export cosmetic lists** - Share collections as JSON
- **Social sharing** - Share preview images to Discord/Twitter
- **PWA support** - Install as desktop/mobile app
- **Cloud sync** - Optional account system for cross-device sync

---

## 8. Milestones

### Milestone A (Level 1) - MVP Web App
- HTML5 file upload UI
- JSZip integration for browser-based extraction
- Metadata extraction from manifest.json
- IndexedDB storage implementation
- Search and filter UI
- Display mod icons

### Milestone B (Level 2) - Preview System
- `.hhh` UnityFS parsing in browser
- Asset extraction (meshes, textures)
- Three.js 3D preview viewer
- Preview image generation (PNG/WebP)
- Preview GIF generation (animated)
- Download individual previews

### Milestone C - Advanced Features
- Bulk preview export
- Thunderstore API integration
- CORS proxy setup (optional backend)
- PWA packaging
- Performance optimizations

---

## 9. Deliverables
- Source code repository
- GitHub Pages deployment (live site)
- Documentation (README, user guide)
- Developer setup instructions
- Automated tests (unit + E2E)
- Browser compatibility matrix

