# Vendor Import Staging: AssetStudio

This directory is reserved for allowlisted source files imported from AssetStudio.

## Primary Source
- Repository: https://github.com/aelurum/AssetStudio
- Commit SHA: TODO_PIN_COMMIT_SHA

## Import Checklist
1. Copy only allowlisted parser files.
2. Copy LICENSE and required notices.
3. Record every imported upstream path in attribution docs.
4. Verify no forbidden interop/native/API usage.
5. Run parser fixture tests.

## Forbidden Content
- GUI and desktop-specific UI code
- Native exporter wrappers
- Runtime network/server dependencies
- Any code requiring server-side processing
