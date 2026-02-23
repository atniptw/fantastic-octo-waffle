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
- None yet (pin + candidate list only)

### Excluded Areas Confirmed
- GUI paths excluded: planned (`AssetStudio.GUI`)
- Native/export paths excluded: planned (`AssetStudioFBXNative`, native decoder bindings)
- Network/server-related paths excluded: planned by policy

### License and Notice Files
- LICENSE copied: pending first file import batch
- Third-party notices copied: pending first file import batch

### Validation
- Fixture set used: not started
- Test command results: not started
- Known deltas: unitypackage extraction paths pending follow-up mapping
