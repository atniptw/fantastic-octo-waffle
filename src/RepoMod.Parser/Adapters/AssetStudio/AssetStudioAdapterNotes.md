# AssetStudio Adapter Notes

This folder contains adapters that map imported AssetStudio parser behavior into stable `RepoMod.Parser` abstractions.

## Rules
- Do not expose vendor types outside adapter layer.
- Do not reference GUI/export/native code paths.
- Keep all parse inputs in-memory for browser compatibility.
- Preserve deterministic behavior for fixture-based testing.

## Current Status
- Adapter implementation is active for MVP scanning paths.
- Direct `.unitypackage` scan path now probes package `asset` payloads using imported AssetStudio `FileReader` and maps discovered entries into parser contracts.
- Current MVP boundary is fixture-driven: `FoxMask_head.hhh` and `MoreHead-Asset-Pack_v1.3.unitypackage` are covered by unit tests.
- Additional AssetStudio imports are not required unless a new fixture reveals a missing parser path.
