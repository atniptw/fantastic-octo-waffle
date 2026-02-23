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
	- `IArchiveScanner`
	- `IModParser`
- Current app integration: parser services registered via DI in Blazor startup.
- Adapter staging area: [src/RepoMod.Parser/Adapters/AssetStudio](../src/RepoMod.Parser/Adapters/AssetStudio)
- Vendor staging area: [src/RepoMod.Parser/Vendor/AssetStudio](../src/RepoMod.Parser/Vendor/AssetStudio)

## Extraction Manifest (To Fill During Import)
- Source repository URL
- Source commit SHA
- Imported file list (allowlisted only)
- Excluded paths (GUI/export/native)
- License/notice files copied
- Local modifications summary

Reference implementation scaffold:
- [src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs](../src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs)

First-batch candidate files are tracked in `CandidateBatch1SourcePaths` in the manifest.

Current extraction status:
- Batch 1 imported: `BuildTarget.cs`, `ClassIDType.cs`, `FileType.cs`, `ImportHelper.cs`
- Batch 2 imported: reader primitives (`Endian*`, `FileReader`, `ResourceReader`, `ObjectInfo`, `SerializedFileHeader`, `FileIdentifier`)
- Batch 3 imported: bundle/decompression path staging (`BundleFile`, `BundleDecompressionHelper`, `StreamFile`, `WebFile`, related options/logger helpers)
- Vendor source is currently compile-excluded to enable staged adapter integration without breaking builds.

Compile integration status:
- A small allowlisted vendor subset is now compile-enabled (`FileType`, `BuildTarget`, `ClassIDType`, `EndianType`, `EndianBinaryReader`, `EndianSpanReader`, `FileReader`, `ILogger`, `Logger`, `ImportHelper`, `Brotli/*`, `BundleCompression/SevenZipLzma/*`).
- Remaining vendor files stay compile-excluded until adapter dependencies are resolved incrementally.

Known scope note:
- Explicit `unitypackage` handling is not yet mapped in the first candidate batch and will be added in a follow-up extraction pass after AssetBundle baseline import.

## Implementation Checklist
1. Pin upstream commit SHA in manifest and attribution docs.
2. Populate allowlist with exact upstream source paths before copying files.
3. Copy only allowlisted files into vendor staging folder.
4. Copy LICENSE and required third-party notices.
5. Run forbidden-dependency review (`DllImport`, native wrappers, networking APIs).
6. Implement adapter mapping from vendor types to parser contracts.
7. Validate fixture corpus and record results.
8. Apply fallback criteria only if primary source blocks MVP fixtures.

## Definition of Done (Extraction Step)
- Vendor files are pinned to a documented commit SHA.
- Attribution record includes source paths and notices.
- Parser compiles without GUI/native/server dependencies.
- Fixture tests pass for MVP corpus.
- App still passes repository verification contract.

## Switch Criteria (Primary -> Fallback)
Switch from primary to fallback source when any of these hold on the fixture corpus:
1. Unsupported critical sample blocks MVP flow.
2. Required fix would introduce non-browser-safe dependencies.
3. Fallback passes the same corpus with lower integration risk.

## Licensing and Attribution
- Preserve MIT license text from imported source
- Retain copyright headers/notices as required
- Preserve applicable third-party notices for imported files
- Track provenance in a dedicated attribution record

## Parity Validation Strategy
- Build fixture corpus from representative mod files
- Compare parsed outputs against expected structures
- Use UnityPy comparisons when parser results are ambiguous
- Record known deltas and accepted deviations
