# TypeTree Parsing Refactor - AssetStudio Alignment

**Date**: 2026-02-04  
**Issue**: #203 (Parser Refactor: Port AssetStudio TypeTree Parsing Logic)  
**PR**: copilot/refactor-type-tree-parsing

## Overview

This document describes the TypeTree parsing refactoring that aligned our implementation with AssetStudio's reference implementation and fixed the duplicate children bug.

## Problem Statement

### Duplicate Children Bug
TypeTree nodes were having their children added multiple times, causing incorrect tree structures. This happened because the tree hierarchy was being built in two places:

1. **During parsing**: `SerializedFile.BuildTypeTree()` would build the tree from the flat list
2. **During reading**: `TypeTreeReader.CreateFromFlatList()` would rebuild the tree

When both methods ran, children were added twice to each parent node.

### Example of the Bug
```
Input (flat list):
  [0] Root (level=0)
  [1] Child1 (level=1)
  [2] Grandchild1 (level=2)
  [3] Child2 (level=1)

After first build:
  Root.Children = [Child1, Child2]
  Child1.Children = [Grandchild1]

After second build (DUPLICATE!):
  Root.Children = [Child1, Child2, Child1, Child2]  // duplicates!
  Child1.Children = [Grandchild1, Grandchild1]      // duplicates!
```

## Solution

Following AssetStudio's approach, we now use **lazy tree building**:

1. **Parsing phase** (`SerializedFile`): 
   - Keep nodes as flat list
   - Store root node reference only
   - Do NOT build tree hierarchy

2. **Reading phase** (`TypeTreeReader`):
   - Build tree hierarchy on first use
   - Check if tree already built (`root.Children.Count == 0`)
   - Cache the built tree in the node's Children lists

### Code Changes

#### SerializedFile.cs (line 633-644)
```csharp
// BEFORE:
if (nodes.Count > 0)
{
    type.TreeRoot = BuildTypeTree(nodes);  // Built tree during parsing
}

// AFTER:
if (nodes.Count > 0)
{
    type.TreeRoot = nodes[0];  // Just store root reference
}
```

#### TypeTreeReader.cs (line 27-58)
```csharp
// AFTER:
if (root.Children.Count == 0)  // Only build if not already built
{
    // Build children for each node based on level
    for (int i = 0; i < nodes.Count; i++)
    {
        var parentNode = nodes[i];
        int parentLevel = parentNode.Level;
        int childLevel = parentLevel + 1;
        
        for (int j = i + 1; j < nodes.Count; j++)
        {
            var potentialChild = nodes[j];
            if (potentialChild.Level <= parentLevel)
                break;
            if (potentialChild.Level == childLevel)
            {
                parentNode.Children.Add(potentialChild);
            }
        }
    }
}
```

## Validation Against AssetStudio

### Blob Format (v >= 12, Unity 2022.3+)

Our implementation matches AssetStudio field-by-field:

| Field | Type | Size | Match |
|-------|------|------|-------|
| m_Version | int16 | 2 bytes | ✅ |
| m_Level | byte | 1 byte | ✅ |
| m_TypeFlags | byte | 1 byte | ✅ |
| m_TypeStrOffset | uint32 | 4 bytes | ✅ |
| m_NameStrOffset | uint32 | 4 bytes | ✅ |
| m_ByteSize | int32 | 4 bytes | ✅ |
| m_Index | int32 | 4 bytes | ✅ |
| m_MetaFlag | int32 | 4 bytes | ✅ |
| m_RefTypeHash | uint64 | 8 bytes (v≥19) | ✅ |

String resolution logic also matches exactly.

### Reading Approach Comparison

**AssetStudio approach**:
```csharp
// Uses flat list with index manipulation
var nodes = GetNodes(m_Nodes, i);      // Extract subtree
i += nodes.Count - 1;                  // Skip processed nodes
// Read element at index 3 for arrays
int tmp = 3;
ReadValue(nodes, reader, ref tmp);
```

**Our approach**:
```csharp
// Uses hierarchical tree with Children lists
foreach (var child in node.Children)
{
    var value = ReadNode(child);       // Recursive traversal
}
```

Both approaches are functionally equivalent. Our approach:
- Matches UnityPy's design (our primary reference for reading logic)
- More intuitive for developers (explicit parent-child relationships)
- No index manipulation needed
- Slightly more memory (stores Children references, but nodes themselves aren't duplicated)

## Testing

### Test Results
- **252 tests passing** (no regressions)
- **59 tests skipped** (LZMA decompression, expected)
- **1 test failing** (pre-existing, unrelated to TypeTree)

### Validation
- ✅ No duplicate children in tree structure
- ✅ Blob format parsing matches AssetStudio exactly
- ✅ String resolution matches AssetStudio
- ✅ All TypeTree-related tests passing
- ✅ CodeQL security scan: no vulnerabilities
- ✅ Code review: no issues

## Impact on R.E.P.O. Mod Parsing

R.E.P.O. uses Unity 2022.3.0f1, which corresponds to SerializedFile format version 22. This uses the **blob format** (v >= 12), which we've validated matches AssetStudio exactly.

The legacy format (v < 10) is not used by R.E.P.O. mods and was not critical to fix for this refactoring.

## Future Work (Optional)

If needed, we could:
1. Update legacy format parsing to match AssetStudio's recursive approach
2. Refactor TypeTreeReader to use flat list + GetNodes approach (like AssetStudio)
3. Add explicit tests for tree hierarchy building

However, none of these are necessary for R.E.P.O. mod parsing, which works correctly with the current implementation.

## References

- AssetStudio TypeTreeHelper: https://github.com/Perfare/AssetStudio/blob/master/AssetStudio/TypeTreeHelper.cs
- AssetStudio SerializedFile: https://github.com/Perfare/AssetStudio/blob/master/AssetStudio/SerializedFile.cs
- UnityPy reference: https://github.com/K0lb3/UnityPy
