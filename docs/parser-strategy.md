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

## Library Boundary (Implemented)
- Runtime parser project: [src/RepoMod.Parser/RepoMod.Parser.csproj](../src/RepoMod.Parser/RepoMod.Parser.csproj)
- Current abstractions:
	- `IArchiveScanner`
	- `IModParser`
- Current app integration: parser services registered via DI in Blazor startup.

## Extraction Manifest (To Fill During Import)
- Source repository URL
- Source commit SHA
- Imported file list (allowlisted only)
- Excluded paths (GUI/export/native)
- License/notice files copied
- Local modifications summary

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
