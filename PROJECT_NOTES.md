# R.E.P.O. Mod Browser Project (Overview)

 

This overview links to modular design docs under [docs/README.md](docs/README.md):
- [Architecture](docs/Architecture.md)
- [Thunderstore API](docs/ThunderstoreAPI.md)
- [Data Models](docs/DataModels.md)
- [Cloudflare Worker](docs/CloudflareWorker.md)
- [Blazor UI](docs/BlazorUI.md)
- [Unity Parsing](docs/UnityParsing.md)
- [Testing Strategy](docs/TestingStrategy.md)
- [Deployment](docs/Deployment.md)
- [Workflow](docs/Workflow.md)

## Vision
Preview cosmetic mods directly in the browser without launching R.E.P.O.

## Scope
- Game: R.E.P.O. (Thunderstore community `repo`)
- Source: Thunderstore API
- Target: Blazor WebAssembly + Cloudflare Worker + Three.js

## Architecture Summary
See [docs/Architecture.md](docs/Architecture.md) for system overview and data flow.

## Implementation Plan (Summary)
- Phase 1: Project setup, binary utils, fixtures
- Phase 2: Port Unity bundle/serialized file parsing (from UnityPy)
- Phase 3: Port mesh parsing + Three.js export
- Phase 4: Blazor UI + JS interop renderer
- Phase 5: Worker proxy + caching + deployment
- Phase 6: Textures, materials, multi-mesh, search/filter

## Status
- Planning phase; modular design documentation established

---
Last Updated: January 17, 2026
# R.E.P.O. Mod Browser Project

Note: Detailed design documentation has been modularized under the `docs/` folder for easier navigation:
- Architecture: docs/Architecture.md
- Thunderstore API: docs/ThunderstoreAPI.md
- Data Models: docs/DataModels.md
- Cloudflare Worker: docs/CloudflareWorker.md
- Blazor UI: docs/BlazorUI.md
- Unity Parsing: docs/UnityParsing.md
- Testing Strategy: docs/TestingStrategy.md
- Deployment: docs/Deployment.md
- Workflows (CI/CD): docs/Workflow.md

## Project Vision
Create a browser-based viewer for cosmetic mods from R.E.P.O., eliminating the need to load mods into the game just to preview them.

## Problem Statement
- **Current Pain Point**: Cosmetic mods must be loaded into R.E.P.O. to preview, increasing game load times
- **Desired Solution**: Preview mods directly in a web browser without launching the game
- **Game**: R.E.P.O. (Steam)
- **Mod Source**: Thunderstore - https://new.thunderstore.io/c/repo/
  - Over 2,554 mods available
  - Categories include: Cosmetics, Items, Valuables, Weapons, Levels, Monsters
  - Community URL slug: `repo` (lowercase)

## Technical Architecture

### Components
1. **Frontend**: GitHub Pages (Blazor WASM)
   - Static website hosting
   - Blazor WebAssembly app
   - User interface for browsing mods
   - Unity asset parsing in browser

2. **Proxy Layer**: Cloudflare Worker
   - Acts as intermediary between frontend and Thunderstore API
   - Handles CORS and API requests

3. **Data Source**: Thunderstore API
  - Community: **R.E.P.O.** (official name)
  - Community identifier: `repo` (slug for API calls)
   - Community page: https://new.thunderstore.io/c/repo/
   - API Documentation: https://new.thunderstore.io/api/docs
   
  **Official Categories** (verified):
  - Cosmetics ⭐ (primary target)
  - Valuables ⭐ (primary target)
  - Items ⭐ (primary target)
  - Weapons ⭐ (primary target)
  - Levels
  - Monsters
  - Drones
  - Upgrades
  - Audio
  - Server-side
  - Client-side
  - Misc
  - Libraries
  - Tools
  - Modpacks
  - Mods

### Implementation Stack
- **Language**: C# / .NET Core
- **Framework**: Blazor WebAssembly
- **Rendering**: Three.js via JS interop
- **Reference**: UnityPy (Python) - source of truth for parsing logic

### Data Flow
```
User → GitHub Pages (Blazor WASM) → Cloudflare Worker (Proxy) → Thunderstore API
                ↓
       Download & Extract Mod (zip)
                ↓
       Parse Unity Files (.hhh) in Blazor
                ↓
       Extract Mesh Geometry
                ↓
       Render via Three.js (JS Interop)
```

## Technical Details

### Mod Structure
- **Format**: ZIP archive
- **Contents**: Unity asset files (UnityFS bundles)
- **File Extension**: `.hhh` (UnityFS bundle format)

#### UnityFS Bundle (.hhh) Structure
- **Node 0**: SerializedFile containing objects/metadata (including Mesh objects)
- **Node 1**: `.resS` resource file with actual vertex/index data payloads
- **Additional nodes**: Other resources
- **Mesh Object**: ClassID 43, references external data via `m_StreamData`

### POC Status
- Various proof of concept work completed
- Architecture validated

## Technical Implementation Details

### Unity Mesh Parsing Requirements
Based on porting notes from UnityPy:

#### Key Components to Implement
1. **UnityFS Bundle Parser** ✅ IMPLEMENTED
   - Parse bundle header and nodes
   - Decompress node data (nodes are pre-decompressed in ParsedBundle)
   - Handle multi-node structure (SerializedFile + .resS resources)
   - `src/bundle.rs` - ~180 lines

2. **Mesh Object Parser (ClassID 43)** ⚠️ PARTIAL
   - Parse 20 mesh fields in specific order with proper 4-byte alignment
   - Key fields: m_Name, m_SubMeshes, m_VertexData, m_IndexBuffer, m_StreamData, m_CompressedMesh
   - Handle external vertex data references
   - `src/deserialize/mesh.rs` - CompressedMesh path working, VertexData needs work

3. **Vertex Data Extraction** ⚠️ IN PROGRESS
   - Parse VertexData structure (channels, streams, raw bytes) - PARTIAL
   - Resolve StreamingInfo references to .resS node - ✅ DONE
   - Extract position, normal, UV, tangent data from external storage - NEEDS WORK
   - Apply channel offsets and strides correctly - NOT IMPLEMENTED

4. **Index Buffer Handling** ✅ IMPLEMENTED
   - Support both 16-bit and 32-bit index formats
   - Handle inline IndexBuffer or compressed triangles in CompressedMesh
   - Unpack PackedBitVector data when needed
   - `src/packed_bit_vector.rs` - Working

5. **PackedBitVector Decompression** ✅ IMPLEMENTED
   - Implement unpacking algorithm for compressed mesh data
   - Handle: vertices, UVs, normals, tangents, weights, signs, bone indices, triangles
   - Apply scaling: `value = int * (range / ((1 << bit_size) - 1)) + start`

#### Critical Parsing Rules
- **Alignment**: 4-byte alignment after byte arrays and bool triplets (failure causes corruption)
- **External Data**: Most vertex data stored in .resS node, not inline
- **StreamingInfo Path**: Format like `archive:/CAB-<hash>/CAB-<hash>.resS`

#### Test Assets Available
- Cigar (220 verts), FrogHatSmile (500), BambooCopter (533)
- Glasses (~600), Odradek, XBandAidB, SamusPlushie (~53k)
- All use external vertex storage
- Located at: `/workspaces/UnityPy/example-mod/*.hhh`

### POC Status (Rust - Proof of Concept)
**Repository**: https://github.com/atniptw/UnityPy/tree/master/rust_wasm_port

**Purpose**: Validate that Unity asset parsing can work in WASM
**Status**: ✅ PROVEN - Three.js demo successfully rendering real mesh geometry

**Key Takeaway**: The entire pipeline is viable. Now implement properly in Blazor.

---

### Blazor WASM Implementation Strategy

**Critical Decision**: **DIRECT PORT from UnityPy Python code**

#### DO NOT:
- ❌ Try to "learn" the format incrementally
- ❌ Reverse engineer from scratch
- ❌ Build piece by piece without reference
- ❌ Experiment with parsing approaches

#### DO:
- ✅ Copy logic directly from UnityPy Python implementation
- ✅ Port algorithms line-by-line from working Python code
- ✅ Use UnityPy as the source of truth for ALL parsing logic
- ✅ Validate C# output matches Python output exactly

#### Reference Implementation
**UnityPy Python Project**: https://github.com/K0lb3/UnityPy

**Key Modules to Port**:
1. `UnityPy/files/BundleFile.py` → Bundle parser
2. `UnityPy/files/SerializedFile.py` → SerializedFile parser
3. `UnityPy/classes/Mesh.py` → Mesh object reader
4. `UnityPy/helpers/MeshHelper.py` → Mesh geometry extraction
5. `UnityPy/helpers/PackedBitVector.py` → Decompression algorithm

### Planned Blazor Project Structure
```
BlazorModViewer/
├── BlazorModViewer.Client/          # WASM app
│   ├── Pages/
│   │   └── Index.razor              # Main viewer UI
│   ├── Services/
│   │   ├── ThunderstoreService.cs   # API client
│   │   └── MeshRenderService.cs     # JS interop for Three.js
│   └── wwwroot/
│       ├── js/
│       │   └── meshRenderer.js      # Three.js wrapper
│       └── index.html
├── BlazorModViewer.Shared/
│   └── Models/                      # Shared DTOs
└── UnityAssetParser/                # Core parsing library
    ├── Bundle/
    │   ├── BundleFile.cs            # Port from BundleFile.py
    │   └── SerializedFile.cs        # Port from SerializedFile.py
    ├── Classes/
    │   └── Mesh.cs                  # Port from Mesh.py
    ├── Helpers/
    │   ├── MeshHelper.cs            # Port from MeshHelper.py
    │   ├── PackedBitVector.cs       # Port from PackedBitVector.py
    │   └── BinaryReader.cs          # Binary utilities
    └── Output/
        └── ThreeJsExporter.cs       # Export to Three.js format
```

### Rendering Strategy
- ✅ Extract mesh geometry with C# (direct port from UnityPy)
- ✅ Export to Three.js JSON format
- ✅ Render via Three.js with C# → JS interop
- 🔄 Texture extraction (future, also port from UnityPy)

## Implementation Plan

### Phase 1: Project Setup & Core Infrastructure
- [ ] Create Blazor WASM project structure
- [ ] Set up UnityAssetParser class library
- [ ] Configure GitHub Actions for build/deploy
- [ ] Implement binary reader utilities (EndianBinaryReader)
- [ ] Set up test fixtures with sample .hhh files

### Phase 2: Port Unity Bundle Parser (Direct from UnityPy)
- [ ] Port `BundleFile.py` → `BundleFile.cs`
  - UnityFS signature parsing
  - Block decompression (LZMA, LZ4)
  - Node extraction
- [ ] Port `SerializedFile.py` → `SerializedFile.cs`
  - Header parsing
  - Type tree reading
  - Object metadata extraction
- [ ] Validate against Python output (byte-for-byte comparison)

### Phase 3: Port Mesh Parsing (Direct from UnityPy)
- [ ] Port `PackedBitVector.py` → `PackedBitVector.cs`
  - Bit unpacking algorithm
  - Int and float decompression
- [ ] Port `Mesh.py` → `Mesh.cs`
  - 20-field binary structure reading
  - VertexData parsing
  - CompressedMesh handling
  - StreamingInfo resolution
- [ ] Port `MeshHelper.py` → `MeshHelper.cs`
  - Channel/stream interpretation
  - Vertex attribute extraction
  - Index buffer processing
- [ ] Export to Three.js JSON format
- [ ] Validate mesh output matches Python exactly

### Phase 4: Blazor UI & Rendering
- [ ] Create Thunderstore mod browser UI
- [ ] Implement zip extraction in browser (DotNetZip or JS interop)
- [ ] Set up Three.js renderer with C# → JS interop
- [ ] Load .hhh from zip, parse with C#, render mesh
- [ ] Add camera controls, lighting, material preview

### Phase 5: Infrastructure & Deployment
- [ ] Implement Cloudflare Worker proxy
- [ ] Integrate Thunderstore API (community slug: `repo`)
- [ ] Configure API to filter R.E.P.O. cosmetic categories
- [ ] Deploy to GitHub Pages
- [ ] Add caching strategy
- [ ] Performance optimization
- [ ] Browser compatibility testing

### Phase 6: Polish & Features
- [ ] Texture extraction and display
- [ ] Material property preview
- [ ] Multiple mesh support
- [ ] Mod comparison view
- [ ] Search and filter functionality

## Reference Implementations
- **UnityPy (PRIMARY REFERENCE)**: https://github.com/K0lb3/UnityPy
  - Python implementation - THE SOURCE OF TRUTH
  - All parsing logic must be ported directly from this project
  - Do not deviate, do not "learn" - just port the working code
  
- **Your Fork with Notes**: https://github.com/atniptw/UnityPy/blob/master/PORTING_NOTES.md
  - Contains Blazor-specific porting guidance
  - Mesh format documentation
  - Rust POC as proof of concept (reference only, not used in production)

- **Target Platform**: .NET 8 Blazor WebAssembly
  - C# for all parsing logic
  - Three.js (via JS interop) for rendering

## Common Pitfalls to Avoid
1. Treating `m_DataSize` as a length field instead of raw data
2. Ignoring external .resS resources (vertex data will be empty)
3. Skipping 4-byte alignment after arrays/bools (causes bogus reads)
4. Not bounds-checking PackedBitVector data_size

## Minimal Extraction Flow
```
1. Parse Mesh fields (in order, with alignment)
2. Read StreamingInfo
3. Locate .resS node and slice by offset/size
4. Use VertexData channels/streams to read positions (Float32, dim 3)
5. Unpack indices from CompressedMesh triangles or IndexBuffer
6. Render in WebGL
```

## Team Structure & Workflow

### Human Developer (You)
- Project direction and requirements
- Architecture decisions
- Quality assurance and validation
- Final code review

### Copilot Instances (GitHub Actions Runners)
- Code implementation based on clear specifications
- Direct porting from UnityPy Python code
- Running tests and validation
- Build and deployment automation

### Technical Lead (This Instance)
- Create detailed implementation tasks with Python file references
- Break down porting work into manageable chunks
- Provide exact Python → C# porting instructions
- Track progress and coordinate work
- Prevent "learning from scratch" - enforce direct porting

## Critical Workflow Rule

**For every implementation task, the TL must provide**:
1. Exact Python file/function to port from UnityPy
2. Line numbers in Python source
3. Expected C# class/method structure
4. Validation criteria (compare output to Python)

**Copilot agents must**:
1. Take the Python code as gospel
2. Port logic line-by-line to C#
3. Not try to "understand" or "learn" the format
4. Validate output matches Python exactly

## Thunderstore API Integration

### Community Information

**Official Details** (verified via API):
- **Community Name**: R.E.P.O.
- **Community Identifier**: `repo`
- **API Base**: `https://thunderstore.io/c/repo/api/v1/`

**Categories Endpoint**:
```
GET https://thunderstore.io/api/experimental/community/repo/category/
```

**All Available Categories** (16 total):
1. **Cosmetics** - Visual customizations (heads, bodies, accessories)
2. **Valuables** - In-game collectibles
3. **Items** - Gameplay items
4. **Weapons** - Weapon mods
5. **Levels** - Custom maps
6. **Monsters** - Enemy modifications
7. **Drones** - Drone customizations
8. **Upgrades** - Player upgrades
9. **Audio** - Sound replacements
10. **Server-side** - Server functionality
11. **Client-side** - Client functionality
12. **Misc** - Miscellaneous
13. **Libraries** - Code libraries
14. **Tools** - Utility mods
15. **Modpacks** - Mod collections
16. **Mods** - General mods

**Primary Focus for 3D Preview**: Cosmetics, Valuables, Items, Weapons

### API Endpoints

**Base URL**: `https://thunderstore.io/api/v1/`

#### 1. Community Package List (PRIMARY ENDPOINT)
```
GET /c/{community_identifier}/api/v1/package/
```

**For R.E.P.O.**:
```
GET https://thunderstore.io/c/repo/api/v1/package/
```

**Response Structure**:
```json
[
  {
    "name": "string",
    "full_name": "namespace-name",
    "owner": "string",
    "package_url": "https://...",
    "donation_link": "string (optional)",
    "date_created": "ISO 8601",
    "date_updated": "ISO 8601",
    "uuid4": "uuid",
    "rating_score": 0,
    "is_pinned": false,
    "is_deprecated": false,
    "has_nsfw_content": false,
    "categories": ["string"],
    "versions": [
      {
        "name": "string",
        "full_name": "namespace-name-version",
        "description": "string",
        "icon": "https://gcdn.thunderstore.io/.../icon.png",
        "version_number": "1.0.0",
        "dependencies": ["namespace-name-version"],
        "download_url": "https://thunderstore.io/package/download/.../file.zip",
        "downloads": 0,
        "date_created": "ISO 8601",
        "website_url": "string",
        "is_active": true,
        "uuid4": "uuid",
        "file_size": 0
      }
    ]
  }
]
```

**Notes**:
- Returns ALL packages for the community in one response (no pagination on v1)
- Large response (~2500+ packages for R.E.P.O.)
- Filter client-side by categories
- Latest version is first in `versions` array

#### 2. Package Details (if needed)
```
GET /c/repo/api/v1/package/{uuid4}
```
Returns single package with all versions.

#### 3. Experimental API (RATE LIMITED)
```
GET /api/experimental/frontend/c/repo/packages/
```
- Has pagination support (`?page=1&page_size=20`)
- May have Cloudflare protection (blocked without cookies/UA)
- More structured response; preferred for UI listing when accessible
- Supports pagination; category and search filters may be available
- Fallback strategy: use v1 API and filter client-side when experimental is blocked

#### 4. Version Readme & Changelog (Experimental)
```
GET /api/experimental/package/{namespace}/{name}/{version}/readme
GET /api/experimental/package/{namespace}/{name}/{version}/changelog
```
- Enriches mod detail view with markdown content
- Render via GitHub Pages using client-side markdown renderer (sanitize!)

#### 5. Package Index (Experimental)
```
GET /api/experimental/package-index
```
- Lightweight index optimized for search/autocomplete
- Use for quick lookup; fall back to v1 packages list if blocked

### Key Data Points We Need

**For Mod Browser**:
- `name`, `owner` (display)
   - `categories` (filter by categories above)
- `icon` (thumbnail)
- `description` (from latest version)
- `rating_score` (popularity sort)
- `is_deprecated` (filter out)
   - `has_nsfw_content` (optional filter/warning)
   - `date_updated` (sort by recent)

**Real Example** (MoreHead mod):
```json
{
  "name": "MoreHead",
  "full_name": "YMC_MHZ-MoreHead",
  "owner": "YMC_MHZ",
  "categories": ["Cosmetics", "Tools", "Server-side", "Client-side", ...],
  "rating_score": 124,
  "versions": [{
    "version_number": "1.4.3",
    "description": "Customizable cosmetics. 简单好玩的装饰模组，提供unitypackage，供玩家自行导入模型。",
    "icon": "https://gcdn.thunderstore.io/live/repository/icons/YMC_MHZ-MoreHead-1.4.3.png",
    "download_url": "https://thunderstore.io/package/download/YMC_MHZ/MoreHead/1.4.3/",
    "downloads": 25123,
    "file_size": 2202890,
    "dependencies": ["nickklmao-MenuLib-2.5.1"]
  }]
}
```

**Notes**:
- Mods can have multiple categories
- Popular cosmetic mods: MoreHead, FrogHatSmile, BambooCopter, etc.
- File sizes range from ~100KB to ~50MB
- Dependencies typically include BepInEx or libraries

**For Download**:
- `download_url` (from specific version)
- `file_size` (show to user)
- `dependencies` (inform user if dependencies required)
- Follow redirects (302) to CDN; prefer HEAD first to get `Content-Length`

**For Display**:
- `date_updated` (sort by recent)
- `downloads` (show popularity)
- `website_url` (link to mod page/GitHub)

### Package ZIP Structure

When downloading from `download_url`, expect:
```
mod_namespace-name-version.zip
├── manifest.json         # Package metadata
├── README.md            # Mod description
├── icon.png             # Mod icon
├── CHANGELOG.md         # Version history (optional)
└── [mod files]          # DLL, assets, configs, Unity bundles (.hhh)
```

**Unity Assets Location** (cosmetic mods):
- Typically in root or `assets/` folder
- File extension: `.hhh` (Unity bundle)
- May have multiple bundles for different models
- Look for files matching pattern: `*.hhh`, `*_head.hhh`, `*_body.hhh`, etc.

**Manifest Structure** (`manifest.json`):
```json
{
  "name": "ModName",
  "version_number": "1.0.0",
  "website_url": "https://...",
  "description": "...",
  "dependencies": [
    "BepInEx-BepInExPack-5.4.21"
  ]
}
```

### Rate Limiting & Caching

**Observed Behavior**:
- V1 API: No obvious rate limits on GET requests
- Experimental API: Cloudflare protection, may require headers/cookies
- CDN assets (icons, downloads): No rate limits

**Request Headers**:
- Include a descriptive `User-Agent`: `RepoModViewer/0.1 (+https://atniptw.github.io)`
- Set `Accept: application/json`
- Add `Origin` and handle CORS via Cloudflare Worker

**Recommended Caching Strategy**:

1. **Package List** (Cloudflare Worker KV):
   - Cache full package list
   - TTL: 5 minutes
   - Key: `repo:package_list`
   - Refresh on cache miss or TTL expire

2. **Package Downloads** (Browser Cache):
   - Use standard HTTP caching headers
   - Cache ZIP files in browser (IndexedDB)
   - Key: `{namespace}-{name}-{version}`
   - Don't cache in Cloudflare (large files)

3. **Category List** (Worker KV):
- Cache categories result from experimental endpoint
- TTL: 1 hour
- Key: `repo:categories`

3. **Icons** (CDN):
   - Already cached by Thunderstore CDN
   - No special handling needed

4. **Parsed Assets** (Browser):
   - Cache parsed .hhh → Three.js JSON
   - Store in IndexedDB
   - Key: `{mod_full_name}:{asset_filename}`
   - Persist across sessions

**Cloudflare Worker Cache Implementation**:
```javascript
const CACHE_TTL = 300; // 5 minutes

async function getCachedPackages(community) {
  const cacheKey = `${community}:package_list`;
  
  // Try KV first
  let cached = await KV.get(cacheKey, { type: 'json' });
  if (cached) return cached;
  
  // Fetch from Thunderstore
  const response = await fetch(
    `https://thunderstore.io/c/${community}/api/v1/package/`
  );
  const packages = await response.json();
  
  // Store in KV
  await KV.put(cacheKey, JSON.stringify(packages), {
    expirationTtl: CACHE_TTL
  });
  
  return packages;
}
```

### Error Handling

**HTTP Status Codes**:
- `200 OK` - Success
- `404 Not Found` - Invalid community or package
- `429 Too Many Requests` - Rate limited (experimental API)
- `403 Forbidden` - Cloudflare block (experimental API)
- `503 Service Unavailable` - Thunderstore down

**Retry Strategy**:
- Exponential backoff: 1s, 2s, 4s, 8s
- Max retries: 3
- On repeated failures: Show cached data if available
- Inform user of API issues

### CORS Considerations

**Direct Browser Calls**:
- ❌ V1 API does NOT have CORS headers
- ✅ CDN assets (icons, downloads) have CORS
- ✅ Must use Cloudflare Worker as proxy

**Cloudflare Worker Requirements**:
- Add CORS headers to all responses
- Proxy package list endpoint
- Forward downloads (or redirect to direct CDN URL)
- Handle preflight OPTIONS requests

**Example Worker Route Usage**:
```
GET /api/packages            -> proxy to /c/repo/api/v1/package/
GET /api/categories          -> proxy to /api/experimental/community/repo/category/
GET /api/package/{ns}/{name}/{ver}/readme    -> proxy experimental readme
GET /api/package/{ns}/{name}/{ver}/changelog -> proxy experimental changelog
```

**Example Client Requests**:
```bash
# List packages (via worker)
curl -H "User-Agent: RepoModViewer/0.1 (+https://atniptw.github.io)" \
  https://worker.example.com/api/packages

# List categories
curl https://worker.example.com/api/categories

# Get readme for a version
curl https://worker.example.com/api/package/YMC_MHZ/MoreHead/1.4.3/readme
```

---

## Blazor Component Architecture

### Component Hierarchy
_TODO: Define component structure and responsibilities_

```
App.razor
├── Router
├── Layout/
│   ├── MainLayout.razor
│   └── NavMenu.razor
├── Pages/
│   ├── Index.razor (Mod Browser)
│   ├── ModDetail.razor (Single mod view)
│   └── Viewer3D.razor (3D preview)
└── Shared/
    ├── ModCard.razor
    ├── ModFilter.razor
    └── LoadingSpinner.razor
```

**State Management**:
- [ ] Mod list state
- [ ] Selected mod state
- [ ] Parsing progress state
- [ ] 3D viewer state

**Routing**:
- [ ] `/` - Mod browser/search
- [ ] `/mod/{namespace}/{name}` - Mod detail view
- [ ] `/mod/{namespace}/{name}/preview` - 3D viewer

---

## Cloudflare Worker Design

### Proxy Logic
_TODO: Define worker endpoints and request handling_

**Worker Endpoints**:
- [ ] `/api/mods` - Proxy to Thunderstore package list
- [ ] `/api/mod/{namespace}/{name}` - Proxy to package details
- [ ] `/api/download/{namespace}/{name}/{version}` - Proxy download with CORS

**CORS Configuration**:
```
Access-Control-Allow-Origin: https://atniptw.github.io
Access-Control-Allow-Methods: GET, OPTIONS
Access-Control-Allow-Headers: Content-Type
```

**Caching Strategy**:
- [ ] Cache package lists (TTL: 5 minutes)
- [ ] Cache package details (TTL: 1 hour)
- [ ] Cache downloads (TTL: 24 hours)
- [ ] Use Cloudflare KV for persistent cache

**Error Handling**:
- [ ] Rate limit responses (429)
- [ ] Thunderstore API errors
- [ ] Timeout handling
- [ ] Fallback responses

---

## Data Models & DTOs

### C# Data Models (Design)

#### Thunderstore DTOs
```csharp
public sealed class PackageVersion
{
  public string Name { get; set; }
  public string FullName { get; set; }
  public string Description { get; set; }
  public Uri Icon { get; set; }
  public string VersionNumber { get; set; }
  public List<string> Dependencies { get; set; } = new();
  public Uri DownloadUrl { get; set; }
  public int Downloads { get; set; }
  public DateTime DateCreated { get; set; }
  public Uri WebsiteUrl { get; set; }
  public bool IsActive { get; set; }
  public Guid Uuid4 { get; set; }
  public long FileSize { get; set; }
}

public sealed class ThunderstorePackage
{
  public string Name { get; set; }
  public string FullName { get; set; }
  public string Owner { get; set; }
  public Uri PackageUrl { get; set; }
  public Uri DonationLink { get; set; }
  public DateTime DateCreated { get; set; }
  public DateTime DateUpdated { get; set; }
  public Guid Uuid4 { get; set; }
  public int RatingScore { get; set; }
  public bool IsPinned { get; set; }
  public bool IsDeprecated { get; set; }
  public bool HasNsfwContent { get; set; }
  public List<string> Categories { get; set; } = new();
  public List<PackageVersion> Versions { get; set; } = new();
}

public sealed class PackageCategory
{
  public string Name { get; set; }
  public string Slug { get; set; }
}

public sealed class CommunityInfo
{
  public string Identifier { get; set; } // "repo"
  public string Name { get; set; }       // "R.E.P.O."
}
```

JSON Mapping Notes:
- `icon`, `package_url`, `download_url` map to `Uri`
- `uuid4` maps to `Guid`
- Arrays map to `List<T>`
- Field names match v1 API exactly to allow simple `System.Text.Json` deserialization

#### Unity Asset Parsing Models (Design)
```csharp
public sealed class BundleNode
{
  public string Path { get; set; } // e.g., archive:/CAB-<hash>/CAB-<hash>.resS
  public byte[] Data { get; set; }
}

public sealed class StreamingInfo
{
  public ulong Offset { get; set; }
  public string Path { get; set; } // resource path
  public ulong Size { get; set; }
}

public sealed class VertexChannel
{
  public int Stream { get; set; }
  public int Offset { get; set; }
  public int Format { get; set; }     // 0=Float32
  public int Dimension { get; set; }  // components (e.g., 3)
}

public sealed class StreamInfo
{
  public int ChannelMask { get; set; }
  public int Stride { get; set; }
  public int Offset { get; set; }
}

public sealed class VertexData
{
  public int VertexCount { get; set; }
  public List<VertexChannel> Channels { get; set; } = new();
  public List<StreamInfo> Streams { get; set; } = new();
}

public sealed class PackedBitVector
{
  public uint NumItems { get; set; }
  public float Range { get; set; }
  public float Start { get; set; }
  public uint NumBits { get; set; } // low byte = bit_size
  public uint DataSize { get; set; }
  public byte[] Data { get; set; }
}

public sealed class MeshData
{
  public string Name { get; set; }
  public int VertexCount { get; set; }
  public int IndexCount { get; set; }
  public List<float> Positions { get; set; } = new(); // flat array [x,y,z,...]
  public List<ushort> IndicesU16 { get; set; } = new();
  public StreamingInfo StreamData { get; set; }
}
```

#### Three.js Export Models (Design)
```csharp
public sealed class ThreeJsGeometry
{
  public List<float> Positions { get; set; } = new();
  public List<ushort> Indices { get; set; } = new();
  public List<float> Normals { get; set; } = new(); // optional
  public List<float> Uv { get; set; } = new();      // optional
}

public sealed class ThreeJsMesh
{
  public string Name { get; set; }
  public ThreeJsGeometry Geometry { get; set; } = new();
  public string MaterialName { get; set; }
}
```

#### Worker Cache Models (Design)
```csharp
public sealed class CachedPackageList
{
  public DateTime CachedAt { get; set; }
  public List<ThunderstorePackage> Packages { get; set; } = new();
}

public sealed class CachedCategories
{
  public DateTime CachedAt { get; set; }
  public List<PackageCategory> Categories { get; set; } = new();
}
```

Validation Snapshots:
- Persist reference JSON from v1 API and parsed results
- Compare C# DTO serialization against reference using JSON diff
- Tolerate float precision in geometry arrays

---

## GitHub Actions Workflow

### CI/CD Pipeline
_TODO: Define build, test, and deployment automation_

**Workflows**:

1. **Build & Test** (`build.yml`)
   - [ ] Trigger: Push to main, PR
   - [ ] Build Blazor WASM
   - [ ] Run unit tests
   - [ ] Run integration tests
   - [ ] Validate Unity parsing against reference data

2. **Deploy** (`deploy.yml`)
   - [ ] Trigger: Push to main (after successful build)
   - [ ] Build for production
   - [ ] Deploy to GitHub Pages
   - [ ] Update Cloudflare Worker

3. **Agent Tasks** (`agent-task.yml`)
   - [ ] Trigger: Issue labeled with `copilot-agent`
   - [ ] Agent reads issue specification
   - [ ] Implements task
   - [ ] Creates PR with changes
   - [ ] Validates against tests

**Agent Task Template**:
```markdown
## Task: [Description]

### Port from UnityPy:
- File: `UnityPy/path/to/file.py`
- Lines: XXX-YYY
- Target: `UnityAssetParser/Path/To/File.cs`

### Validation:
- [ ] Unit tests pass
- [ ] Output matches Python reference
- [ ] Code compiles without warnings

### References:
- Python source: [link]
- Related documentation: [link]
```

---

## Testing Strategy

### Test Fixtures
_TODO: Define test data and validation approach_

**Test Assets**:
- [ ] Sample .hhh files (various sizes)
- [ ] Reference outputs from UnityPy (JSON)
- [ ] Known mesh data (vertex counts, bounds)
- [ ] Edge cases (compressed, external data, etc.)

**Test Structure**:
```
Tests/
├── UnityAssetParser.Tests/
│   ├── Bundle/
│   │   ├── BundleFileTests.cs
│   │   └── SerializedFileTests.cs
│   ├── Classes/
│   │   └── MeshTests.cs
│   ├── Helpers/
│   │   ├── PackedBitVectorTests.cs
│   │   └── MeshHelperTests.cs
│   └── Fixtures/
│       ├── sample1.hhh
│       ├── sample1.expected.json
│       └── ...
└── BlazorModViewer.Tests/
    ├── Services/
    └── Components/
```

**Validation Approach**:
1. Parse asset with C# implementation
2. Parse same asset with Python UnityPy
3. Compare outputs (JSON diff)
4. Assert exact match for critical fields
5. Tolerate minor differences (floating point precision)

### Unit Tests
- [ ] Binary reader utilities
- [ ] PackedBitVector decompression
- [ ] Mesh field parsing
- [ ] Vertex data extraction
- [ ] Index buffer handling

### Integration Tests
- [ ] Full bundle parse → mesh extraction
- [ ] ZIP extraction → asset discovery
- [ ] Three.js geometry export
- [ ] End-to-end pipeline validation

---

## Deployment Architecture

### GitHub Pages Configuration
_TODO: Define hosting and deployment setup_

**Repository Structure**:
```
fantastic-octo-waffle/
├── docs/ (GitHub Pages root)
│   ├── _framework/
│   ├── css/
│   ├── js/
│   │   └── meshRenderer.js (Three.js)
│   ├── index.html
│   └── service-worker.js
├── src/ (Source code)
└── .github/workflows/
```

**Build Configuration**:
- [ ] Blazor publish to `docs/`
- [ ] Asset optimization (trimming, AOT)
- [ ] Service worker for offline support
- [ ] Cache headers configuration

**Custom Domain** (optional):
- [ ] DNS configuration
- [ ] HTTPS enforcement
- [ ] Subdomain setup

### Cloudflare Worker Deployment
_TODO: Define worker deployment process_

**Deployment**:
- [ ] Wrangler CLI configuration
- [ ] Environment variables (API keys)
- [ ] Route configuration
- [ ] KV namespace setup

**Monitoring**:
- [ ] Error logging
- [ ] Performance metrics
- [ ] Rate limit tracking
- [ ] Usage analytics

### CDN & Asset Hosting
_TODO: Define asset delivery strategy_

**Static Assets**:
- [ ] Three.js library (CDN vs. bundled)
- [ ] Web fonts
- [ ] Icons and images

**Cache Strategy**:
- [ ] Immutable assets (content hash)
- [ ] Versioned assets (cache busting)
- [ ] Dynamic content (no-cache)

---

**Last Updated**: January 17, 2026
**Status**: Planning Phase - Modular Design Documentation Established
