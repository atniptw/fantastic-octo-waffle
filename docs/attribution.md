# Attribution and Provenance

This project plans to extract parser-relevant code from third-party repositories under permissive licenses.

## Planned Sources
- Primary: aelurum/AssetStudio
- Fallback: Razviar/assetstudio

## Policy
1. Pin imported source to commit SHA.
2. Record upstream file paths for all imported files.
3. Preserve required license and notice text.
4. Avoid importing non-permissive or platform-native-only code unless explicitly reviewed.
5. Re-run provenance review when upgrading imported code.

## Required Record Per Import Batch
- Source repository URL
- Commit SHA
- Imported file list
- Local destination paths
- License/notice files copied
- Notes on any local modifications

## Extraction Record Template

Use this template for each import batch:

### Batch
- Date:
- Maintainer:
- Source repo:
- Commit SHA:

### Imported Files
- Upstream path:
- Local path:

### Excluded Areas Confirmed
- GUI paths excluded:
- Native/export paths excluded:
- Network/server-related paths excluded:

### License and Notice Files
- LICENSE copied:
- Third-party notices copied:

### Validation
- Fixture set used:
- Test command results:
- Known deltas:

Manifest reference:
- [src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs](../src/RepoMod.Parser/Adapters/AssetStudio/AssetStudioImportManifest.cs)

## Current Extraction Record

### Batch
- Date: 2026-02-23
- Maintainer: Project owner
- Source repo: https://github.com/aelurum/AssetStudio
- Commit SHA: 6b66ec74674f61d7b331d0766fc38511e9c885f3

### Imported Files
- Upstream path: `AssetStudio/BuildTarget.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/BuildTarget.cs`
- Upstream path: `AssetStudio/ClassIDType.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ClassIDType.cs`
- Upstream path: `AssetStudio/EndianBinaryReader.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/EndianBinaryReader.cs`
- Upstream path: `AssetStudio/EndianSpanReader.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/EndianSpanReader.cs`
- Upstream path: `AssetStudio/EndianType.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/EndianType.cs`
- Upstream path: `AssetStudio/FileIdentifier.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/FileIdentifier.cs`
- Upstream path: `AssetStudio/FileReader.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/FileReader.cs`
- Upstream path: `AssetStudio/FileType.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/FileType.cs`
- Upstream path: `AssetStudio/ImportHelper.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ImportHelper.cs`
- Upstream path: `AssetStudio/ObjectInfo.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ObjectInfo.cs`
- Upstream path: `AssetStudio/ResourceReader.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ResourceReader.cs`
- Upstream path: `AssetStudio/SerializedFileHeader.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/SerializedFileHeader.cs`
- Upstream path: `AssetStudio/BundleFile.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/BundleFile.cs`
- Upstream path: `AssetStudio/BundleDecompressionHelper.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/BundleDecompressionHelper.cs`
- Upstream path: `AssetStudio/StreamFile.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/StreamFile.cs`
- Upstream path: `AssetStudio/WebFile.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/WebFile.cs`
- Upstream path: `AssetStudio/TempFileStream.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/TempFileStream.cs`
- Upstream path: `AssetStudio/UnityVersion.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/UnityVersion.cs`
- Upstream path: `AssetStudio/ColorConsole.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ColorConsole.cs`
- Upstream path: `AssetStudio/ColorConsoleHelper.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ColorConsoleHelper.cs`
- Upstream path: `AssetStudio/ILogger.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/ILogger.cs`
- Upstream path: `AssetStudio/Logger.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/Logger.cs`
- Upstream path: `AssetStudio/Progress.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/Progress.cs`
- Upstream path: `AssetStudio/CustomOptions/CustomBundleOptions.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/CustomOptions/CustomBundleOptions.cs`
- Upstream path: `AssetStudio/CustomOptions/ImportOptions.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/CustomOptions/ImportOptions.cs`
- Upstream path: `AssetStudio/BundleCompression/Oodle/Oodle.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/BundleCompression/Oodle/Oodle.cs`
- Upstream path: `AssetStudio/BundleCompression/SevenZipLzma/SevenZipLzma.cs`
	- Local path: `src/RepoMod.Parser/Vendor/AssetStudio/AssetStudio/BundleCompression/SevenZipLzma/SevenZipLzma.cs`

### Excluded Areas Confirmed
- GUI paths excluded: planned (`AssetStudio.GUI`)
- Native/export paths excluded: planned (`AssetStudioFBXNative`, native decoder bindings)
- Network/server-related paths excluded: planned by policy

### License and Notice Files
- LICENSE copied: `src/RepoMod.Parser/Vendor/AssetStudio/LICENSE.upstream`
- Third-party notices copied: pending additional notice mapping in next batch

### Validation
- Fixture set used: not started
- Test command results: pending (batch imported as staged vendor source, excluded from compile by project guard)
- Known deltas: unitypackage extraction paths pending follow-up mapping; vendor code remains compile-excluded until adapter integration phase.
