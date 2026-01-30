# Unity Parsing Notes

 

## Overview
Direct port of UnityPy parsing rules covering UnityFS bundle nodes, SerializedFile object metadata, Mesh structure, and extraction of positions/indices from external .resS resources.

## Bundle Structure (.hhh)
- UnityFS bundle with nodes
- Node 0: SerializedFile (objects/metadata)
- Node 1: .resS resource (vertex/index payloads)

## SerializedFile Parsing

### Structure
SerializedFile is the container format for Unity scene/asset data inside UnityFS bundles. It contains:
- **Header**: Format version, endianness, file/data sizes
- **Type Tree**: Type metadata describing object layouts (optional, version-dependent)
- **Object Table**: List of objects with PathID, ClassID, offset, size
- **File Identifiers**: External file references for cross-bundle dependencies
- **Object Data Region**: Raw serialized object payloads

### Header Formats

#### Version >= 22 (Unity 2022+)
- MetadataSize (uint32) - size of metadata region (header + type tree + object table)
- FileSize (int64) - total file size
- Version (uint32) - SerializedFile format version
- DataOffset (int64) - start of object data blob
- Endianness (byte) - 0 = little-endian, 1 = big-endian
- Reserved (3 bytes padding)

#### Version >= 14 < 22 (Unity 5.x - 2021)
- MetadataSize (uint32)
- FileSize (uint32)
- Version (uint32)
- DataOffset (uint32)
- Endianness (byte)
- Reserved (3 bytes padding)

#### Version < 14
Different layout with Unity version string, target platform, and EnableTypeTree flag. Port exact UnityPy branches for these older formats.

### Endianness Handling
- If `Endianness == 1` (big-endian), all multi-byte integers must be byte-swapped after reading
- `EndianBinaryReader` wrapper class conditionally swaps bytes based on header flag
- Apply endianness consistently to all fields (header, type tree, object table, type tree nodes)
- Big-endian rare (console platforms: PS3, Xbox 360, Wii U)

### Type Tree
Contains type metadata for object deserialization:
- `ClassId`: Unity ClassID (43 = Mesh, 28 = Texture2D, etc.)
- `IsStrippedType`: Whether editor-only data is removed
- `ScriptTypeIndex`: For MonoBehaviour scripts (-1 if not applicable)
- `ScriptId`: MD5 hash for MonoBehaviour (16 bytes, version >= 17)
- `OldTypeHash`: Type hash (16 bytes, version >= 5)
- `TypeTreeNodes`: Field structure (if `EnableTypeTree` is true)
- `TypeDependencies`: Dependency indices (version >= 21)

#### TypeTreeNode
- `Type`: Field type name (e.g., "Vector3f", "int")
- `Name`: Field name (e.g., "m_Position", "m_Size")
- `ByteSize`: Field size in bytes (-1 for variable-length)
- `Index`: Node index in tree
- `TypeFlags`: Flags (bit 0x4000 indicates array)
- `Version`: Field version
- `MetaFlag`: Metadata flags
- `Level`: Tree depth (0 = root, 1 = child, etc.)

For version >= 10, strings are stored in a string table for deduplication (referenced by offsets).
For version < 10, strings are inline null-terminated.

### Object Table
Each `ObjectInfo` entry:
- `PathId`: Unique object identifier within file (m_PathID)
- `ByteStart`: Offset relative to DataOffset where object data starts
- `ByteSize`: Object payload size in bytes
- `TypeId`: Index into type tree, or direct ClassID (if no type tree)
- `ClassId`: Unity ClassID (resolved from TypeId via type tree lookup)
- `ScriptTypeIndex`: For MonoBehaviour (version >= 11)
- `Stripped`: 1 = stripped, editor-only data removed (version >= 15)

Layout varies by version:
- Version >= 14: PathId (int64), ByteStart (int64 for v22+, uint32 otherwise), ByteSize (uint32), TypeId (int32)
- Version < 14: Different sizes and alignment (port exact UnityPy layout)

4-byte alignment required before object table and between entries for version >= 14.

### ClassId Resolution
- If type tree present and `HasTypeTree == true`: `ClassId = TypeTree.Types[ObjectInfo.TypeId].ClassId`
- Else: `ClassId = ObjectInfo.TypeId` (direct use as ClassID)
- Handle edge case: TypeId out of range → throw `InvalidObjectInfoException`

### External References (FileIdentifier)
Each `FileIdentifier`:
- `Guid`: 16-byte GUID (version >= 6, else `Guid.Empty`)
- `Type`: Identifier type (0 = not serialized, other values per Unity internal)
- `PathName`: External file path (e.g., "archive:/CAB-hash/CAB-hash.resS")

Resolution: When object references external file (e.g., `StreamingInfo.path`), match `PathName` to BundleFile nodes by exact string comparison.

### TypeTree Object Deserialization

#### Current Implementation Status (January 2026)
The `TypeTreeReader` service dynamically deserializes object data using TypeTree node metadata. **Current implementation has critical performance issues** that prevent practical use on complex objects.

**Working**: Simple objects (GameObject with 15 nodes) deserialize correctly.  
**Failing**: Complex objects (Material ~107 nodes, Shader ~3800 nodes) timeout due to O(N²) or worse traversal complexity.

#### Architecture Problem

The current implementation uses a **flat node list with global `_nodeIndex`** approach:
```csharp
private readonly IReadOnlyList<TypeTreeNode> _nodes;
private int _nodeIndex;  // Global position in flat list
```

This creates fundamental performance issues when reading arrays of complex types:
1. For each array element, `_nodeIndex` resets to template start position
2. Must traverse entire subtree for each element to find where it ends
3. With nested arrays/structs, traversal becomes exponential: O(N * M * K)
   - N = array size
   - M = fields per element  
   - K = average subtree depth

**Example**: Shader type (3848 nodes) with arrays of structs → minutes to deserialize vs. milliseconds in UnityPy.

#### Root Cause Analysis

**Modern Unity TypeTree format (2022.3+)**:
- Arrays use wrapper node with `TypeFlags = 0x01`, empty `Type`/`Name` fields
- Structure: `Parent → Wrapper(0x01) → Size → Data → ...descendants`
- Must skip wrapper + size + all data descendants to reach next sibling

**Flat index traversal issue**:
```csharp
// For primitive arrays: fast (read N values)
for (int i = 0; i < size; i++)
    list.Add(ReadPrimitive(dataNode.Type));

// For complex arrays: catastrophically slow
for (int i = 0; i < size; i++)
{
    _nodeIndex = templateStart;  // Reset to template
    // Walk entire subtree for EACH element
    while (_nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > dataLevel)
    {
        ReadValue(_nodes[_nodeIndex]);  // Recursive, advances index
    }
}
```

Each array element requires full subtree traversal. With nested structures (array of structs containing arrays), cost multiplies.

#### Comparison with UnityPy (Python)

UnityPy uses **recursive tree traversal** which naturally tracks position:
```python
def read_typetree(nodes, stream):
    def read_node(node_idx):
        node = nodes[node_idx]
        if is_array(node):
            size = read_int(stream)
            return [read_node(child_idx) for _ in range(size)]
        elif is_primitive(node):
            return read_primitive(stream, node.type)
        else:
            return {child.name: read_node(child_idx) 
                    for child_idx in get_children(node_idx)}
    return read_node(0)
```

Recursion with explicit child indices eliminates need for index resetting.

#### Proposed Solutions

**Option 1: Pre-compute Subtree Ranges (Recommended)**
Cache the end index for each node's subtree during TypeTree parsing:
```csharp
public class TypeTreeNode
{
    // Existing fields...
    public int SubtreeEndIndex { get; set; }  // Index of next sibling
}

// During TypeTree parse, compute once:
for (int i = 0; i < nodes.Count; i++)
{
    int endIdx = i + 1;
    int level = nodes[i].Level;
    while (endIdx < nodes.Count && nodes[endIdx].Level > level)
        endIdx++;
    nodes[i].SubtreeEndIndex = endIdx;
}
```

Then in ReadValue:
```csharp
private object? ReadValue(TypeTreeNode node)
{
    int savedIndex = _nodeIndex;
    // Read logic...
    _nodeIndex = node.SubtreeEndIndex;  // Jump to next sibling
    return value;
}
```

**Benefits**: O(1) skip instead of O(N) traversal, preserves flat list structure.  
**Tradeoff**: Extra memory (4 bytes per node), one-time O(N) preprocessing.

**Option 2: Recursive Tree Structure**
Convert flat list to actual tree during parsing:
```csharp
public class TypeTreeNode
{
    public List<TypeTreeNode> Children { get; set; }
    // No global index needed
}

private object? ReadValue(TypeTreeNode node, EndianBinaryReader reader)
{
    if (node.Children.Count == 0)
        return ReadPrimitive(node.Type, reader);
    
    if (IsArray(node))
    {
        int size = reader.ReadInt32();
        var dataNode = node.Children[1];  // Skip wrapper, size nodes
        return Enumerable.Range(0, size)
            .Select(_ => ReadValue(dataNode, reader))
            .ToList();
    }
    
    return node.Children.ToDictionary(
        child => child.Name,
        child => ReadValue(child, reader));
}
```

**Benefits**: Natural traversal, matches UnityPy logic, no index management.  
**Tradeoff**: Memory overhead (pointers), tree construction cost, larger refactor.

**Option 3: Hybrid - Recursive Calls with Flat Storage**
Keep flat list but use recursion with explicit index ranges:
```csharp
private (object? value, int nextIndex) ReadValueRec(int nodeIdx)
{
    var node = _nodes[nodeIdx];
    int endIdx = FindSubtreeEnd(nodeIdx);  // Or use cached value
    
    if (nodeIdx + 1 >= endIdx)
        return (ReadPrimitive(node.Type), endIdx);
    
    // Process children in range [nodeIdx+1, endIdx)
    // ...
    return (value, endIdx);
}
```

**Benefits**: Simpler than full tree, explicit control over ranges.  
**Tradeoff**: Still requires subtree end computation.

#### Implementation Recommendation

**Phase 1** (Immediate): Implement Option 1 (pre-computed subtree ranges)
- Modify `TypeTreeNode` to add `SubtreeEndIndex` property
- Add preprocessing step in `ParseTypeTreeNodes()` to compute ranges
- Update `TypeTreeReader` to use `node.SubtreeEndIndex` instead of loop traversal
- Expected outcome: 1000x+ speedup for complex objects

**Phase 2** (Future optimization): Consider Option 2 if memory permits
- Only if profiling shows preprocessing cost is significant
- Provides cleaner abstraction for future TypeTree features

#### Testing Strategy

Validate fix with progressive complexity:
1. **Simple**: GameObject (15 nodes) - should remain working
2. **Medium**: Material (107 nodes, string/PPtr arrays) - currently times out
3. **Complex**: Shader (3848 nodes, nested structs) - currently times out
4. **Mesh**: Mesh object (239 nodes, large float/int arrays) - critical for rendering

Success criteria: All objects deserialize in <100ms (currently: GameObject=fast, others=timeout after 2+ minutes).

#### Current Workaround

For immediate progress, skip TypeTreeReader validation and use hardcoded Mesh parser:
- Mesh structure is well-documented and stable across Unity versions
- Manually parse Mesh fields with fixed offsets (see docs/MESH_FIELD_ORDER_REFERENCE.md)
- Only use TypeTreeReader for validation/debugging after optimization

### Data Offset & Object Data
- Object data region starts at `Header.DataOffset`
- Each object's absolute offset: `DataOffset + ObjectInfo.ByteStart`
- Validate: `ByteStart + ByteSize ≤ FileSize - DataOffset`
- Expose `ObjectDataRegion` as `data[DataOffset..FileSize]` for efficient slicing

### Parsing Algorithm
1. Parse header (version-specific layout)
2. Validate header bounds (FileSize, DataOffset, MetadataSize)
3. Create `EndianBinaryReader` based on Endianness flag
4. Read version-specific metadata (Unity version string, target platform, EnableTypeTree flag)
5. Parse type tree (type count, then each SerializedType with version-specific fields)
6. Align to 4-byte boundary
7. Parse object table (object count, then each ObjectInfo with version-specific fields)
8. Resolve ClassIDs for objects (TypeId → ClassId via type tree lookup)
9. Align to 4-byte boundary
10. Parse file identifiers (external references)
11. Validate object bounds (ByteStart + ByteSize within data region)
12. Extract object data region slice
13. Return `SerializedFile` instance

### API Usage
```csharp
// Parse SerializedFile from bundle node
var serializedFile = SerializedFile.Parse(nodeData.Span);

// Check for Mesh objects (ClassID 43)
bool hasRenderable = serializedFile.GetObjectsByClassId(43).Any();

// Find specific object by PathId
var meshObj = serializedFile.FindObject(pathId: 123);

// Read object payload for downstream parser
var objectData = serializedFile.ReadObjectData(meshObj);
// Pass to Mesh parser, Texture2D parser, etc.
```

## Mesh Parsing (ClassID 43)
- Field order with 4-byte alignment
- VertexData channels/streams
- CompressedMesh (PackedBitVector)
- StreamingInfo references external .resS

## Minimal Extraction Flow
1. Parse Mesh fields
2. Read StreamingInfo
3. Slice .resS by offset/size
4. Read positions via channels/streams
5. Read indices from triangles or IndexBuffer

## Renderable Check (Shallow Parse)
- Goal: Determine if an asset file is renderable without heavy data reads.
- Method: Open the asset (UnityFS → SerializedFile node, or direct SerializedFile) and read the object table/types.
- If any object has `ClassID 43` (Mesh), mark as renderable.
- Do not read vertex payloads or `.resS` bytes in this step; defer to full parse when the user selects the file to render.
- Implementation: `serializedFile.GetObjectsByClassId(43).Any()` → bool renderable

## Full Parse (On Demand)
- Resolve `StreamingInfo` path and offsets to the corresponding `.resS`.
- Extract attributes via `VertexData` channels/streams (positions, normals, uvs, tangents).
- When data is compressed, use `CompressedMesh` with `PackedBitVector` to reconstruct triangles and attributes.
- Export minimal geometry (positions + indices) first for fast preview, optionally enrich with normals/uv later.

## Geometry Export for Three.js
- Build flat arrays: positions (XYZ), indices (triangle list), optional normals and uvs.
- Preserve submesh boundaries by emitting `groups` (`start`, `count`, `materialIndex`).
- Ensure indices reference valid vertex range; choose `Uint16` vs `Uint32` based on max index.
- If normals absent, let the renderer compute them.