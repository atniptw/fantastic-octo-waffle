# Scale & Anchoring Issue - Root Cause Analysis & Solution

## Problem Summary

When users upload a package + decorations (.hhh files), the cosmetics appear:
1. **100-1634x too large** - filling the entire view window
2. **At the center** (0, 0, 0) instead of attached to skeleton bones

## Root Cause

### Why Decorations Are Huge

Each `.hhh` decoration file contains a structure like:
```
- Cigar (root GameObject, scale 1.0, position relative to where it should attach)
  └─ 本体 (mesh container, scale 100.0 or 1634.52)
      └─ Mesh geometry (small vertices, e.g., 0.01 units)
```

The **child object has a 100-1634x scale**. This is intentional in Unity because:
- Mesh geometry was created small (0.01 units)
- Parent scale multiplies through hierarchy
- In the game, decoration is parented to a bone → bone position + local scale = correct size

**When exported STANDALONE (current behavior):**
- No parent bone → child scale becomes WORLD-SCALE
- Result: 100-1634x too large

### Why Decorations Appear at Center

Without the skeleton bone parent context, decorations are root nodes at (0, 0, 0). They can't be positioned on the avatar.

## Solution Architecture

### Stage 1: Load Avatar Skeleton
```csharp
var avatarContext = unityPackageParser.Parse(packageBytes);
// Result: 87 GameObjects, 87 Transforms including:
//   - Skeleton bones (head, neck, etc.)
//   - Decoration attachment anchors named "Head decoration(Do Not Move)"
//   - 18 meshes with materials
```

### Stage 2: Merge Decorations into Skeleton Context
```csharp
var decorationBytes = File.ReadAllBytes("Cigar_neck.hhh");
var hhhParser = new HhhParser();

// NEW API: Merge .hhh into skeleton, parent to "neck" bone
bool success = hhhParser.TryMergeDecorationIntoContext(
    decorationBytes,
    avatarContext,      // Same context as skeleton
    targetBoneName: "neck"
);

// Result: avatarContext now contains:
//   - Original 87 GameObjects
//   - NEW: Cigar GameObject (parented to neck bone)
//   - NEW: Cigar meshes + transforms
//   - Single merged transform hierarchy
```

### Stage 3: Export Combined GLB
```csharp
var combinedGlb = hhhParser.ConvertToGlbFromContext(avatarContext);
// Result: GLB with skeleton + decoration properly parented

// When rendered in model-viewer:
// - Cigar decoration is child of neck bone
// - Child scale (100.0) is RELATIVE to parent
// - Appears at correct size + position on neck
```

## Why This Works

With proper parenting:
- **Scale becomes relative**: 100x scale child appears normal when parented to bone
- **Position is relative**: Decoration's local position (e.g., -1.66, -1.23, 0) is relative to bone
- **Hierarchy preserved**: GLB maintains Transform parent-child relationships
- **model-viewer renders correctly**: Three.js applies transforms recursively

## Implementation Status

✅ **Done:**
- `HhhParser.TryMergeDecorationIntoContext()` method implemented
- Parses .hhh into existing context
- Reparents decoration to target bone
- Merges all semantic data (GameObjects, Transforms, Meshes, Materials)
- Tests pass (28/28)

⏳ **Next Steps (UI Integration):**
1. Update `DecorationIndexService.PersistEntryAsync()` to NOT convert .hhh to standalone GLB
2. Update `DecorationIndexService` to accept optional skeleton context
3. When user selects avatar + decorations, call new merge API
4. Extract bone name from filename (e.g., `Cigar_neck.hhh` → "neck" bone)
5. Export combined skeleton + decoration GLB

## Filename Bone Mapping

Decoration filenames contain bone tags (per MoreHead convention):
- `*_head.hhh` → parent to "head" bone
- `*_neck.hhh` → parent to "neck" bone  
- `*_body.hhh` → parent to "body" bone
- `*_hip.hhh` → parent to "hip" bone
- `*_leftarm.hhh` → parent to "leftarm" bone
- `*_rightarm.hhh` → parent to "rightarm" bone
- `*_leftleg.hhh` → parent to "leftleg" bone
- `*_rightleg.hhh` → parent to "rightleg" bone

Extract via regex: `_(\w+)\.hhh$` → capture group 1 = bone name

## Expected Visual Outcome

**Before (Current - Broken):**
```
┌─────────────────┐
│    [CIGAR]      │  ← 1634x too large, at center
│  FILLS SCREEN   │
└─────────────────┘
```

**After (With Merge - Fixed):**
```
┌─────────────────┐
│    [AVATAR]     │
│      │          │
│    [NECK]       │
│      │          │
│   [CIGAR]       │  ← Child of neck, correct size
│    (small)      │
└─────────────────┘
```

## Testing

Diagnostic test included: `ScaleAndAnchoringDiagnosticsTests.cs`

Shows:
- ✅ Skeleton correctly identified (17 bones including attachment anchors)
- ✅ Decorations correctly parse with expected metadata
- ✅ Extreme scales found in child objects (expected architecture)
- ✅ After merging + parenting, scales appear correct

## API Reference

### TryMergeDecorationIntoContext

```csharp
public bool TryMergeDecorationIntoContext(
    byte[] hhhBytes,              // .hhh decoration file
    BaseAssetsContext existingContext,  // Skeleton context to merge into
    string? targetBoneName = null // Optional: bone name to parent to
)
```

**Returns:** `true` if merge successful, `false` if merge failed or target bone not found

**Example:**
```csharp
var avatarContext = unityPackageParser.Parse(avatarPackage);
var parser = new HhhParser();

// Merge decoration into avatar
var merged = parser.TryMergeDecorationIntoContext(
    cigarHhhBytes,
    avatarContext,
    targetBoneName: "neck"
);

if (merged) {
    var glb = parser.ConvertToGlbFromContext(avatarContext);
    // Save/display GLB with combined skeleton + decoration
}
```
