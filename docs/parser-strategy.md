# Parser Strategy

## Hard Constraints
- Parsing must run fully client-side in the browser.
- No backend API, proxy, or server pipeline may receive mod files.
- Parser architecture must avoid any design that redistributes third-party mod content.

## Decision
Use `aelurum/AssetStudio` as the primary source to extract a minimal .NET parser library.

## Rationale
- Active maintenance signal for modern Unity formats
- C# codebase fits Blazor/.NET integration path
- MIT licensing enables extraction with proper attribution

## Fallback
Use `Razviar/assetstudio` as fallback if sample-corpus compatibility fails with the primary source.

## Last Resort
Use UnityPy only as a correctness oracle and troubleshooting reference, not as primary implementation.

## MVP Compatibility Baseline
- Primary target: MoreHead-style `.hhh` AssetBundles
- Authoring/runtime baseline: Unity 2022.3 (per mod ecosystem guidance)
- Expanded Unity coverage is post-MVP unless required by blocking samples

## Extraction Policy
- Extract only parser-relevant code paths
- Exclude GUI, exporters, and native platform-specific code
- Keep a strict allowlist of imported files/namespaces
- Pin source by commit SHA in docs before importing
- Keep processing local-only with no network upload requirement for parse operations

Current pin:
- `aelurum/AssetStudio` @ `6b66ec74674f61d7b331d0766fc38511e9c885f3`

## Library Boundary (Implemented)
- Runtime parser project: [src/RepoMod.Parser/RepoMod.Parser.csproj](../src/RepoMod.Parser/RepoMod.Parser.csproj)
- Current abstractions:
	- `ISceneExtractor` (primary API for all parsing workflows)
	- `IArchiveScanner` (legacy file-based unitypackage discovery; used by file-based entry points and tests)
	- `IModParser` (metadata extraction from bundle filenames)
- Current app integration: parser services registered via DI in Blazor startup.
- Adapter staging area: [src/RepoMod.Parser/Adapters/AssetStudio](../src/RepoMod.Parser/Adapters/AssetStudio)
- Vendor staging area: [src/RepoMod.Parser/Vendor/AssetStudio](../src/RepoMod.Parser/Vendor/AssetStudio)

## Parsing Architecture (Current)

### In-Memory First Strategy
All parsing workflows now operate on byte payloads in memory, eliminating temporary file writes to disk.

**Unitypackage Parsing (`ParseUnityPackage`):**
1. Accept unitypackage as either file path (legacy) or byte array (primary)
2. Read gzipped tar.gz stream in-memory to extract `UnityPackageItem` entries
3. Discover embedded bundles via content probing (AssetStudio `FileReader` magic byte detection)
4. Parse each discovered bundle via in-memory `FileReader → BundleFile` or `SerializedFile` loading
5. Extract render primitives (meshes, materials, textures) and metadata (GUID references, avatar candidates)
6. Preserve metadata graph even when renders are absent (synthetic root refs for unitypackage fallback)

**Cosmetic Bundle Parsing (`ParseCosmeticBundle`):**
1. Accept cosmetic bundle (`.hhh` or other Unity asset format) as either file path or byte array
2. Probe bundle format using in-memory `FileReader`
3. Load as `SerializedFile` or `BundleFile` via in-memory streams
4. Extract render primitives (must be non-empty; fail-fast if absent)
5. No synthetic fallback refs (strict enforcement for GLB composition safety)

### Key Implementation Details
- **In-Memory FileReader**: AssetStudio's `FileReader` class detects bundle format from magic bytes without file I/O
- **AssetsManager**: Maintains reference graph during deserialization of Unity serialized files
- **Object Extraction**: Shared `AppendSerializedFileObjects` helper iterates Unity objects and builds `UnityObjectRef`, `UnityRenderMesh`, `UnityRenderMaterial`, `UnityRenderTexture` records
- **Container Identity**: Stable hashing on source name (file path or asset blob tag), resilient to in-memory vs. file-based distinction
- **Fail-Fast Validation**: Cosmetic bundles enforce render primitive presence; unitypackage provides metadata fallback for graph completeness

## Vendor Integration Status

**Extraction manifest:**
- [src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs](../src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs)

**Compile-enabled vendor modules (AssetStudio pinned @ `6b66ec74674f61d7b331d0766fc38511e9c885f3`):**
- Core types: `FileType`, `BuildTarget`, `ClassIDType`, `EndianType`
- Reader primitives: `EndianBinaryReader`, `EndianSpanReader`, `FileReader`, `ResourceReader`, `ObjectInfo`, `SerializedFileHeader`, `FileIdentifier`
- Bundle/decompression: `BundleFile`, `BundleDecompressionHelper`, `StreamFile`, `ImportHelper`, `UnityVersion`
- Utilities: `ILogger`, `Logger`, `ColorConsole`, `BigArrayPool`, `OffsetStream`, `Extensions` (stream/binary reader/writer)
- Math: Vector2, Vector3, Vector4, Quaternion, Matrix4x4, Color
- Options: `CustomBundleOptions`, `ImportOptions`, `Asmo/OptionsFile`
- Compression: `Brotli/*`, `SevenZipLzma/*`, `BundleDecompressionHelper`, `Oodle.cs`

**UI/export/native paths excluded (as per extraction policy):**
- GUI components, native platform wrappers, asset export code, network upload handlers

## Implementation Notes

- **Unitypackage parsing** reads gzipped tar.gz in-memory, discovers bundles via `FileReader` content probing, and extracts both render data and metadata graphs
- **No temporary files** are written during parsing; all operations use `MemoryStream` and `FileReader` in-memory APIs
- **Synthetic metadata fallback** for unitypackage only (not cosmetic) preserves graph structure for metadata composition even if no renders are found
- **Fail-fast cosmetic validation** ensures cosmetic bundles produce render primitives or fail loudly (no silent skipping)

## Definition of Done (Current)
- [x] Vendor files pinned to documented commit SHA
- [x] Parser compiles without GUI/native/server dependencies  
- [x] Fixture tests pass (49/49 unit tests)
- [x] App passes repository verification contract
- [x] All temporary file writes removed
- [x] In-memory parsing APIs primary (file-based retained for backward compat tests)

## Switch Criteria (Primary -> Fallback)
Only if any of these hold on the fixture corpus (not currently triggered):
1. Unsupported critical sample blocks MVP flow
2. Required fix would introduce non-browser-safe dependencies
3. Fallback passes same corpus with lower integration risk

## Licensing and Attribution
- Preserve MIT license text from imported source
- Track provenance in a dedicated attribution record

## Parity Validation Strategy
- Build fixture corpus from representative mod files
- Compare parsed outputs against expected structures
- Use UnityPy comparisons when parser results are ambiguous
- Record known deltas and accepted deviations
