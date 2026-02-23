# Data Model

## Storage Engine
IndexedDB (browser local persistence).

## Core Entities

### ModPackage
- `id` (string)
- `sourceFileName` (string)
- `importedAtUtc` (datetime)
- `status` (enum: imported, parsed, failed)
- `notes` (string, optional)

### BundleFile
- `id` (string)
- `modPackageId` (string)
- `pathInArchive` (string)
- `fileName` (string)
- `extension` (string)
- `blobRef` (blob key)
- `hash` (string, optional)

### CosmeticItem
- `id` (string)
- `bundleFileId` (string)
- `displayName` (string)
- `author` (string, optional)
- `version` (string, optional)
- `slotTag` (enum: head, neck, body, hip, leftarm, rightarm, leftleg, rightleg, world, unknown)
- `prefabPath` (string, optional)

### AvatarBase
- `id` (string)
- `source` (enum: unitypackage, bundle, built-in)
- `version` (string, optional)
- `blobRef` (blob key)

### ParseArtifact
- `id` (string)
- `bundleFileId` (string)
- `artifactType` (enum: mesh, material, texture, skeleton, metadata)
- `payloadRef` (blob key or json key)
- `parserSource` (enum: primary, fallback)
- `createdAtUtc` (datetime)

### PreviewPreset
- `id` (string)
- `name` (string)
- `avatarBaseId` (string)
- `cosmeticItemIds` (string array)
- `lastRenderedAtUtc` (datetime, optional)

## Metadata Rules (MVP)
- Treat `.hhh` as AssetBundle container files.
- Derive initial metadata from filename/path conventions when explicit metadata is missing.
- Promote parser-derived metadata over filename-derived values when both exist.

## Schema Versioning
- Maintain a numeric schema version in IndexedDB metadata store.
- Add migration steps for any breaking entity change.
- Keep backward-compatible reads for one prior schema version when feasible.

## Retention and Cleanup
- Keep imported blobs until explicitly removed.
- Provide cleanup routines for failed parse artifacts.
- De-duplicate blobs by hash where practical.
