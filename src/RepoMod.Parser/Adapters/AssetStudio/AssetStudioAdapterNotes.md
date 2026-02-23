# AssetStudio Adapter Notes

This folder contains adapters that map imported AssetStudio parser behavior into stable `RepoMod.Parser` abstractions.

## Rules
- Do not expose vendor types outside adapter layer.
- Do not reference GUI/export/native code paths.
- Keep all parse inputs in-memory for browser compatibility.
- Preserve deterministic behavior for fixture-based testing.

## Current Status
- Adapter implementation not yet imported.
- Manifest placeholder exists in `AssetStudioImportManifest`.
