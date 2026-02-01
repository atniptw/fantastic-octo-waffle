# Mesh Extraction Issue - Investigation & Status Update

**Date**: February 1, 2026  
**Status**: Issue Identified & Documented - Requires TypeTree Investigation

---

## Executive Summary

Real mesh extraction from bundles (Cigar_neck.hhh, ClownNose_head.hhh, Glasses_head.hhh) is now **partially working**:
- ✅ Bundles parse correctly (valid UnityFS format)
- ✅ SerializedFiles parse and object table found
- ✅ Mesh objects (ClassID 43) detected successfully
- ✅ Three.js export format implemented and ready
- ❌ **Mesh geometry data missing** - TypeTree parsing returns incomplete field dictionary

The root cause is **incomplete TypeTree field extraction** from the mesh object's binary data. The TypeTreeReader is only retrieving ~24 top-level fields instead of all mesh struct fields, resulting in:
- Empty SubMeshes array
- Empty VertexData
- No Index/Vertex positions
- Missing external resource (StreamingInfo) references

---

## What Was Fixed

### 1. ✅ Metadata Endianness Bug (SerializedFile v22+)

**Problem**: Unity 2022+ uses dual-endianness system:
- Header bytes 0-15: Platform-native endianness
- Extended header (v22+ only, bytes 20-47): Same endianness as header
- Metadata region: Different endianness (stored in byte 48)

**Previous Bug**: Parser read metadata endianness from wrong location, causing TypeTree to be parsed with incorrect byte order (manifested as "2022.3.40f1c1" → "022.3.40f1c1").

**Fix Applied**: 
```csharp
// Step 2: Read metadata endianness byte (v22+ stores this at byte 48)
if (header.Version >= 22)
{
    int metadataEndianByte = stream.ReadByte();
    header.Endianness = (byte)metadataEndianByte;
}
```

**Impact**: TypeTree and object table now parse with correct endianness. UnityVersion string correctly reads as "2022.3.40f1c1" (was "022.3.40f1c1").

### 2. ✅ StreamingInfoResolver Path Matching

**Problem**: External .resS resource resolution used incorrect string comparison:
```csharp
// WRONG: possibleNames.Contains(n.Path) - allows partial matches
// CORRECT: n.Path.EndsWith(name, StringComparison.Ordinal)
```

**Fix Applied**: Changed to case-sensitive basename matching with EndsWith.

### 3. ✅ MeshExportService Three.js Format

**Added Methods**:
- `ExportToThreeJS(MeshGeometryDto)` - Exports to Three.js BufferGeometry JSON format
- `ExportToJSON(MeshGeometryDto)` - JSON serialization of extracted mesh
- Helper methods for array marshaling (float/uint to bytes to base64)

**Added DTO Properties**:
- UV2, UV3, Colors, Tangents (nullable float arrays for optional vertex attributes)

---

## Root Cause: TypeTree Parsing Incomplete

### Current Behavior

**Test: ExtractMeshes_CigarNeck**
```
Extracted: 0 meshes
Reason: MeshParser returns mesh with:
  - Name: '' (empty)
  - SubMeshes: 0 (should be 3)
  - VertexData: null
  - CompressedMesh: null
  - StreamData: null
```

**Debug Output**:
```
DEBUG: MapToMesh - data has 24 keys: m_Name, m_SubMeshes, m_Shapes, m_BindPose, 
m_VertexData, m_CompressedMesh, m_LocalAABB, m_StreamData, ...
[Missing expected fields like indices, vertex count, etc.]
DEBUG: Mapped 0 SubMeshes
DEBUG: MeshParser returned mesh: Name='', SubMeshes=0
```

### Why This Happens

The TypeTreeReader.ReadObject() traverses the tree structure recursively and attempts to populate a dictionary with field values. However:

1. **Mesh struct has nested arrays**: SubMeshes, IndexBuffer, VertexData channels, etc.
2. **Arrays store external data pointers**: StreamingInfo references point to .resS file (not inline)
3. **Binary buffer exhaustion**: When array reading tries to deserialize external resources inline, it hits EOF and returns empty

Example Mesh TypeTree structure:
```
Root
├── m_Name (string) ✓ reads fine
├── m_SubMeshes (array<SubMesh>) ✗ reads 0 elements
│   ├── firstByte
│   ├── indexCount
│   └── topology
├── m_VertexData (struct)
│   ├── m_VertexCount
│   ├── m_Channels (array)
│   └── m_DataSize (array - references external .resS)
└── m_StreamData (StreamingInfo)
    ├── path (external .resS filename)
    ├── offset
    └── size
```

### Hypothesis

**TypeTreeReader may not be correctly handling complex nested structures or is halting array enumeration prematurely when encountering:**
1. External resource references (StreamingInfo)
2. Zero-length arrays that should map to external .resS data
3. Buffer exhaustion when trying to read inline data that's actually external

---

## Test Updates

Updated real bundle test skip reasons from:
```
"Fixture bundle data corrupted - bundle name not properly null-terminated"
```

To:
```
"TypeTree parsing incomplete: mesh data fields empty (no positions/normals/submeshes). Investigating TypeTreeReader traversal logic."
```

All 251 unit tests passing ✅  
Real bundle tests properly skipped with diagnostic reason 🔄

---

## Next Steps

### Priority 1: Debug TypeTree Field Extraction

**Investigation points**:
1. Enable detailed logging in TypeTreeReader.ReadNode() to track which fields are being skipped
2. Compare TypeTree structure from C# parser vs. UnityPy reference
3. Verify field ordering matches TypeTree node order
4. Check for premature EOF handling when encountering external data references

**Expected outcome**: Determine why submesh array reads 0 elements despite TypeTree nodes existing.

### Priority 2: Handle External Resource Streaming

Once TypeTree parsing is fixed:
1. Verify StreamingInfo path resolution matches .resS files
2. Implement StreamingInfoResolver integration in MeshExtractionService
3. Load vertex/index data from .resS when present

### Priority 3: Three.js Export Validation

1. Write unit tests for ExportToThreeJS(MeshGeometryDto)
2. Validate base64-encoded binary data format
3. Test with mock MeshGeometryDto objects

### Priority 4: Multi-Bundle Validation

Once extraction works:
1. Test all 3 real bundles (Cigar_neck, ClownNose_head, Glasses_head)
2. Validate against UnityPy reference output
3. Update snapshots with successful extraction results

---

## Code Changes Summary

### Files Modified
1. **SerializedFile.cs** - Fixed v22+ metadata endianness reading
2. **MeshExtractionServiceRealBundleTests.cs** - Updated skip reasons
3. **MeshExportService.cs** - Added Three.js export methods (from prior work)
4. **MeshGeometryDto.cs** - Added vertex attribute properties (from prior work)

### Test Status
- **Unit Tests**: 251 passing, 61 skipped (all expected)
- **Build**: ✅ Clean (0 warnings, 0 errors)
- **Integration**: Real bundle parsing works; mesh data extraction incomplete

---

## References

- [SerializedFileEndiannessV22.md](docs/SerializedFileEndiannessV22.md) - Dual endianness architecture
- [UnityParsing.md](docs/UnityParsing.md) - Bundle/SerializedFile structure
- [MeshExtractionServiceImpl.md](docs/MeshExtractionServiceImpl.md) - Implementation notes (TBD)
- UnityPy Python Reference: https://github.com/K0lb3/UnityPy

