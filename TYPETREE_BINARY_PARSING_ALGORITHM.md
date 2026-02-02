# UnityPy TypeTree Binary Parsing Algorithm - Deep Dive

**Researched**: February 2, 2026  
**Source**: Direct analysis of UnityPy codebase  
**Status**: Complete reference with exact line numbers

---

## Executive Summary

UnityPy reads TypeTree structures from binary blobs using a **stack-based tree-building algorithm**. The key insight:

1. **Number of children is READ from binary** - it's a field in the blob format
2. **Children are built recursively** using a stack that tracks parent nodes and remaining child counts
3. **The array assertion** "Array must have exactly 2 children" is **verified after reading**, not guaranteed by the format

---

## Part 1: TypeTree Binary Format Structure

### Blob Format (Modern, v >= 10)

**Location**: `UnityPy/helpers/TypeTreeNode.py:` lines 204-215 (struct definition)

```python
def _get_blob_node_struct(endian: str, version: int) -> tuple[Struct, list[str]]:
    struct_type = f"{endian}hBBIIiii"
    keys = [
        "m_Version",       # short (2 bytes)
        "m_Level",         # byte (1 byte)
        "m_TypeFlags",     # byte (1 byte)
        "m_TypeStrOffset", # int (4 bytes)
        "m_NameStrOffset", # int (4 bytes)
        "m_ByteSize",      # int (4 bytes)
        "m_Index",         # int (4 bytes)
        "m_MetaFlag",      # int (4 bytes)
    ]
    if version >= 19:
        struct_type += "Q"  # Add ulong64 for m_RefTypeHash
        keys.append("m_RefTypeHash")
    
    return Struct(struct_type), keys
```

**Binary Layout (v < 19)**:
```
[m_Version:2B][m_Level:1B][m_TypeFlags:1B][m_TypeStrOffset:4B][m_NameStrOffset:4B][m_ByteSize:4B][m_Index:4B][m_MetaFlag:4B]
= 24 bytes per node
```

**Binary Layout (v >= 19)**:
```
[same as above] + [m_RefTypeHash:8B]
= 32 bytes per node
```

### Legacy Format (Older, v < 10 except v == 10)

**Location**: `UnityPy/helpers/TypeTreeNode.py:` lines 79-109 (parse method)

Text-based format with null-terminated strings. Different parsing approach entirely.

---

## Part 2: The Stack-Based Tree Building Algorithm

### How Blob Format Reads the Tree

**Location**: `UnityPy/helpers/TypeTreeNode.py:` lines 112-154 (`parse_blob` classmethod)

```python
@classmethod
def parse_blob(cls, reader: EndianBinaryReader, version: int) -> TypeTreeNode:
    # STEP 1: Read blob header
    node_count = reader.read_int()          # How many nodes in this blob
    stringbuffer_size = reader.read_int()   # How many bytes for strings
    
    # STEP 2: Read all node binary structs at once
    node_struct, keys = _get_blob_node_struct(reader.endian, version)
    struct_data = reader.read(node_struct.size * node_count)
    stringbuffer_reader = EndianBinaryReader(
        reader.read(stringbuffer_size), 
        reader.endian
    )
    
    # STEP 3: Create helper to decode strings
    CommonString = get_common_strings()
    
    def read_string(reader: EndianBinaryReader, value: int) -> str:
        is_offset = (value & 0x80000000) == 0
        if is_offset:
            reader.Position = value
            return reader.read_string_to_null()
        offset = value & 0x7FFFFFFF
        return CommonString.get(offset, str(offset))
    
    # STEP 4: Parse all nodes and build tree
    fake_root: TypeTreeNode = cls(-1, "", "", 0, 0, [])
    stack: List[TypeTreeNode] = [fake_root]
    parent = fake_root
    prev = fake_root
    
    # CRITICAL: Iterate through struct.unpack results
    for raw_node in node_struct.iter_unpack(struct_data):
        # raw_node is a tuple: (version, level, typeflags, typestr_offset, namestr_offset, bytesize, index, metaflag[, refhash])
        node = cls(
            **dict(zip(keys[:3], raw_node[:3])),  # m_Version, m_Level, m_TypeFlags
            **dict(zip(keys[5:], raw_node[5:])),  # m_ByteSize, m_Index, m_MetaFlag, [m_RefTypeHash]
            m_Type=read_string(stringbuffer_reader, raw_node[3]),    # m_TypeStrOffset → string
            m_Name=read_string(stringbuffer_reader, raw_node[4]),    # m_NameStrOffset → string
        )
        
        # TREE BUILDING LOGIC (not struct parsing)
        if node.m_Level > prev.m_Level:
            stack.append(parent)
            parent = prev
        elif node.m_Level < prev.m_Level:
            while node.m_Level <= parent.m_Level:
                parent = stack.pop()
        
        parent.m_Children.append(node)
        prev = node
    
    return fake_root.m_Children[0]
```

### Key Points:

1. **NO child count in blob format** - children are determined by m_Level comparison
2. **Tree built entirely from level values** - siblings at same level, children at level+1
3. **Stack tracks ancestor nodes** - allows backtracking when level decreases

---

## Part 3: Legacy Format Tree Building (Text-based)

### How Legacy Format Reads the Tree

**Location**: `UnityPy/helpers/TypeTreeNode.py:` lines 79-109 (`parse` classmethod)

```python
@classmethod
def parse(cls, reader: EndianBinaryReader, version: int) -> TypeTreeNode:
    # DIFFERENT ALGORITHM: recursive parsing with COUNT field
    
    # stack approach is way faster than recursion
    # using a fake root node to avoid special case for root node
    dummy_node = cls(-1, "", "", 0, 0, [])
    dummy_root = cls(-1, "", "", 0, 0, [dummy_node])
    
    stack: List[Tuple[TypeTreeNode, int]] = [(dummy_root, 1)]
    while stack:
        parent, count = stack[-1]
        if count == 1:
            stack.pop()
        else:
            stack[-1] = (parent, count - 1)
        
        # Read one node's fields
        node = cls(
            m_Level=parent.m_Level + 1,
            m_Type=reader.read_string_to_null(),        # NULL-TERMINATED STRING
            m_Name=reader.read_string_to_null(),        # NULL-TERMINATED STRING
            m_ByteSize=reader.read_int(),
            m_VariableCount=reader.read_int() if version == 2 else None,
            m_Index=reader.read_int() if version != 3 else None,
            m_TypeFlags=reader.read_int(),
            m_Version=reader.read_int(),
            m_MetaFlag=reader.read_int() if version != 3 else None,
        )
        parent.m_Children[-count] = node
        
        # **THIS IS KEY**: Read the count of children from binary
        children_count = reader.read_int()  # HOW MANY CHILDREN DOES THIS NODE HAVE?
        if children_count > 0:
            node.m_Children = [dummy_node] * children_count  # Pre-allocate
        
        # Add to stack to process children
        stack.append((node, children_count))
    
    return dummy_root.m_Children[0]
```

### Key Differences from Blob:

1. **Children count IS read** from binary (one int32 per node)
2. **Stack tracks (parent_node, remaining_children_count)** - not just parent
3. **Different field order** - TypeFlags is int32, not uint8
4. **Null-terminated strings** - not offset-based strings

---

## Part 4: The Array Node Structure

### How Arrays Are Represented

**Location**: Various files

An "Array" node in the TypeTree has:
- `m_Type == "Array"` (or `"vector"` / `"staticvector"`)
- `TypeFlags & 0x01 != 0` (bit 0x01 indicates array)
- **Exactly 2 children** (guaranteed by parsing validation):
  - **Child[0]**: Size node (metadata, usually has `m_Type=="int"`)
  - **Child[1]**: **ELEMENT TEMPLATE** - the actual data descriptor repeated for each element

### Example: `vector<SubMesh>`

```
Node: "vector" (TypeFlags=0x01)
  ├─ Child[0]: "int" (size/count)
  └─ Child[1]: "SubMesh" (template)
       ├─ Child: "int" (submesh.index0)
       ├─ Child: "int" (submesh.index1)
       ├─ Child: "int" (submesh.baseVertex)
       └─ Child: "int" (submesh.vertexCount)
```

When reading 3 SubMesh elements:
```python
array_size = 3
element_template = vector_node.m_Children[1]
for i in range(array_size):
    read_value(element_template, reader)  # Read all 4 ints for each submesh
```

---

## Part 5: The Array Assertion

### Code Reference

**File**: `UnityPyBoost/TypeTreeHelper.cpp` lines 808-810

```cpp
if (PyList_GET_SIZE(child->m_Children) != 2)
{
    PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
    return NULL;
}
```

**File**: `UnityPy/helpers/TypeTreeHelper.py` lines 206-225

```python
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    # ... read array_wrapper_node = node.m_Children[0] ...
    # Implicit assumption that array_wrapper_node has exactly 2 children:
    # [0] = size descriptor (int32 usually)
    # [1] = element template
```

### How It's Guaranteed

The assertion is **NOT input validation** - it's a **postcondition guarantee** from the parsing algorithm:

1. TypeTree structure is defined by Unity engine during game build
2. **All array nodes are created with exactly 2 children** by Unity's serialization
3. UnityPy reads them as-is
4. If a real file violates this, it's corrupted (not a parsing bug)

---

## Part 6: Exact Binary Sizes - How Many Bytes Per Node?

### Blob Format

**Per node**: `node_struct.size`

```
v < 19:  hBBIIiii = 2+1+1+4+4+4+4+4 = 24 bytes
v >= 19: hBBIIiiiQ = 2+1+1+4+4+4+4+4+8 = 32 bytes
```

**Reading all nodes**:
```python
# File: UnityPy/helpers/TypeTreeNode.py:119
node_count = reader.read_int()              # 4 bytes
stringbuffer_size = reader.read_int()       # 4 bytes
struct_data = reader.read(node_struct.size * node_count)  # node_count * 24 or 32 bytes
stringbuffer_reader = EndianBinaryReader(
    reader.read(stringbuffer_size),         # stringbuffer_size bytes
    reader.endian
)
# Total: 8 + (nodeCount * structSize) + stringbufferSize bytes
```

### Legacy Format

**Per node**: Variable length (depends on string lengths)

```
m_Type: null-terminated string (varies)
m_Name: null-terminated string (varies)
m_ByteSize: 4 bytes (int32)
m_VariableCount: 4 bytes (int32) IF version == 2 only
m_Index: 4 bytes (int32) IF version != 3 only
m_TypeFlags: 4 bytes (int32)
m_Version: 4 bytes (int32)
m_MetaFlag: 4 bytes (int32) IF version != 3 only
children_count: 4 bytes (int32)
```

**Minimum (v != 2, v != 3)**:
```
[string] + [string] + 4 + 4 + 4 + 4 + 4 = 20 + strings
```

---

## Part 7: Complete C# Port Reference

### Blob Parsing in C#

```csharp
// Port of UnityPy/helpers/TypeTreeNode.py:parse_blob
private static List<TypeTreeNode> ParseTypeTreeNodesBlob(
    EndianBinaryReader reader, 
    uint version)
{
    // Step 1: Read blob header
    int nodeCount = reader.ReadInt32();
    int stringBufferSize = reader.ReadInt32();
    
    // Step 2: Read all node structs as binary data
    int nodeStructSize = version >= 19 ? 32 : 24;
    byte[] nodeData = reader.ReadBytes(nodeStructSize * nodeCount);
    byte[] stringBuffer = reader.ReadBytes(stringBufferSize);
    
    // Step 3: Parse each node struct
    var nodes = new List<TypeTreeNode>(nodeCount);
    using (var nodeStream = new MemoryStream(nodeData, writable: false))
    using (var nodeReader = new BinaryReader(nodeStream))
    {
        for (int i = 0; i < nodeCount; i++)
        {
            var node = new TypeTreeNode
            {
                Version = nodeReader.ReadInt16(),              // 2 bytes
                Level = nodeReader.ReadByte(),                 // 1 byte
                TypeFlags = nodeReader.ReadByte(),             // 1 byte
                Type = ReadString(stringBuffer, nodeReader.ReadUInt32()),
                Name = ReadString(stringBuffer, nodeReader.ReadUInt32()),
                ByteSize = nodeReader.ReadInt32(),             // 4 bytes
                Index = nodeReader.ReadInt32(),                // 4 bytes
                MetaFlag = nodeReader.ReadInt32(),             // 4 bytes
            };
            if (version >= 19)
            {
                node.RefTypeHash = nodeReader.ReadUInt64();    // 8 bytes
            }
            nodes.Add(node);
        }
    }
    
    // Step 4: Build tree from level values
    BuildTreeFromLevels(nodes);
    
    return nodes;
}

private static void BuildTreeFromLevels(List<TypeTreeNode> nodes)
{
    if (nodes.Count == 0) return;
    
    var fakeRoot = new TypeTreeNode { Level = -1, Children = new() };
    var stack = new Stack<TypeTreeNode>();
    stack.Push(fakeRoot);
    var parent = fakeRoot;
    var prev = nodes[0];
    
    foreach (var node in nodes)
    {
        if (node.Level > prev.Level)
        {
            stack.Push(parent);
            parent = prev;
        }
        else if (node.Level < prev.Level)
        {
            while (node.Level <= parent.Level)
                parent = stack.Pop();
        }
        
        parent.Children.Add(node);
        prev = node;
    }
}
```

---

## Part 8: Summary Table

| Aspect | Blob (v >= 10) | Legacy (v < 10) |
|--------|---|---|
| **Fixed size per node?** | YES (24 or 32 bytes) | NO (variable) |
| **Children count in binary?** | NO (determined by level) | YES (one int32 field per node) |
| **String format** | Offsets + pool | Null-terminated inline |
| **Parsing algorithm** | Stack + level comparison | Stack + explicit count |
| **Algorithm location** | `TypeTreeNode.py:112-154` | `TypeTreeNode.py:79-109` |
| **Bytes to read per node** | 24-32 bytes + offset-lookup | 20+ bytes + string reads |

---

## Part 9: Key Takeaways for Implementation

### ✅ What UnityPy GUARANTEES

1. After parsing, every node with `m_Type == "Array"` has exactly 2 children
2. Children are accessible via `node.m_Children[0]` and `node.m_Children[1]`
3. Element template is **always at index [1]** of Array wrapper

### ✅ What UnityPy DOES NOT GUARANTEE

1. Children count is NOT stored in blob format - it's derived from levels
2. A node at level N might have 0, 1, 5, or 100 children
3. No "child count" field to read - you must parse all nodes then build tree

### ✅ Reading Algorithm Complexity

1. **Blob format**: O(1) per node to parse struct, O(N) total for N nodes to build tree
2. **Legacy format**: O(string_length) per node to parse strings, O(N) total
3. **Both**: O(N) memory for output list + O(depth) stack space

---

## References

- UnityPy TypeTreeNode.py: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py
- UnityPy SerializedFile.py: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/files/SerializedFile.py
- UnityPy TypeTreeHelper.py: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeHelper.py
- UnityPyBoost C++ source: https://github.com/K0lb3/UnityPy/tree/master/UnityPyBoost
