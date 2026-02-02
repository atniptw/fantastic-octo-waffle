# TypeTree Parsing - Direct Answers to Research Questions

**Research Date**: February 2, 2026  
**Status**: Complete with exact source code line references

---

## Question 1: How does UnityPy parse TypeTree binary data to build the node tree?

### Answer

UnityPy uses **two different algorithms** depending on Unity version:

#### Blob Format (v >= 12, or v == 10)
**File**: `UnityPy/helpers/TypeTreeNode.py:112-154`

```python
# Read blob header
node_count = reader.read_int()           # 4 bytes: how many nodes
stringbuffer_size = reader.read_int()    # 4 bytes: string pool size

# Read all nodes as binary structs (24 or 32 bytes each)
node_struct, keys = _get_blob_node_struct(reader.endian, version)
struct_data = reader.read(node_struct.size * node_count)  # All nodes in one read!

# Read string pool
stringbuffer_reader = EndianBinaryReader(
    reader.read(stringbuffer_size),
    reader.endian
)

# Parse nodes and build tree
fake_root = TypeTreeNode(-1, "", "", 0, 0, [])
stack = [fake_root]
parent = fake_root
prev = fake_root

# KEY STEP: Iterate through struct.iter_unpack
for raw_node in node_struct.iter_unpack(struct_data):
    # Unpack: (version, level, typeflags, type_offset, name_offset, bytesize, index, metaflag)
    node = TypeTreeNode(
        m_Version=raw_node[0],
        m_Level=raw_node[1],
        m_TypeFlags=raw_node[2],
        m_Type=read_string(stringbuffer_reader, raw_node[3]),  # Lookup type string
        m_Name=read_string(stringbuffer_reader, raw_node[4]),  # Lookup name string
        m_ByteSize=raw_node[5],
        m_Index=raw_node[6],
        m_MetaFlag=raw_node[7],
    )
    
    # TREE BUILDING (level-based)
    if node.m_Level > prev.m_Level:
        # Child: prev was parent, now add to prev
        stack.append(parent)
        parent = prev
    elif node.m_Level < prev.m_Level:
        # Sibling at higher level: backtrack until we find correct parent
        while node.m_Level <= parent.m_Level:
            parent = stack.pop()
    
    # Add node to current parent's children list
    parent.m_Children.append(node)
    prev = node

return fake_root.m_Children[0]
```

#### Legacy Format (v < 10, except v == 10)
**File**: `UnityPy/helpers/TypeTreeNode.py:79-109`

```python
# Different algorithm: uses explicit child count from binary

dummy_node = TypeTreeNode(-1, "", "", 0, 0, [])
dummy_root = TypeTreeNode(-1, "", "", 0, 0, [dummy_node])

stack = [(dummy_root, 1)]  # (parent_node, remaining_children_count)

while stack:
    parent, count = stack[-1]
    
    if count == 1:
        stack.pop()
    else:
        stack[-1] = (parent, count - 1)
    
    # Read one node
    node = TypeTreeNode(
        m_Level=parent.m_Level + 1,
        m_Type=reader.read_string_to_null(),              # NULL-TERMINATED STRING
        m_Name=reader.read_string_to_null(),              # NULL-TERMINATED STRING
        m_ByteSize=reader.read_int(),
        m_VariableCount=reader.read_int() if version == 2 else None,
        m_Index=reader.read_int() if version != 3 else None,
        m_TypeFlags=reader.read_int(),
        m_Version=reader.read_int(),
        m_MetaFlag=reader.read_int() if version != 3 else None,
    )
    parent.m_Children[-count] = node
    
    # KEY DIFFERENCE: Read children count from binary
    children_count = reader.read_int()  # 4 bytes: HOW MANY CHILDREN?
    
    if children_count > 0:
        node.m_Children = [dummy_node] * children_count
    
    # Add to stack for processing children
    stack.append((node, children_count))

return dummy_root.m_Children[0]
```

### Summary

| Aspect | Blob | Legacy |
|--------|------|--------|
| Version | v >= 12, or v == 10 | v < 10 or v == 11 |
| Child count source | Derived from m_Level | Read from binary (int32) |
| Tree building | Level-based comparison | Explicit count stack |
| String format | Offset to pool | Null-terminated inline |

---

## Question 2: What is the exact binary format of a TypeTreeNode?

### Answer

#### Blob Format (Modern)

**File**: `UnityPy/helpers/TypeTreeNode.py:204-215`

**Struct Definition**:
```python
struct_type = f"{endian}hBBIIiii"  # Format string for struct.Struct
keys = [
    "m_Version",       # h = short (2 bytes)
    "m_Level",         # B = byte (1 byte)
    "m_TypeFlags",     # B = byte (1 byte)
    "m_TypeStrOffset", # I = uint32 (4 bytes)
    "m_NameStrOffset", # I = uint32 (4 bytes)
    "m_ByteSize",      # i = int32 (4 bytes)
    "m_Index",         # i = int32 (4 bytes)
    "m_MetaFlag",      # i = int32 (4 bytes)
]
if version >= 19:
    struct_type += "Q"  # Q = uint64 (8 bytes)
    keys.append("m_RefTypeHash")
```

**Binary Layout (v < 19)**:
```
Byte Offset | Field              | Type     | Size | Format Code
------------|-------------------|----------|------|-------------
0-1         | m_Version          | int16    | 2    | h
2           | m_Level            | uint8    | 1    | B
3           | m_TypeFlags        | uint8    | 1    | B
4-7         | m_TypeStrOffset    | uint32   | 4    | I
8-11        | m_NameStrOffset    | uint32   | 4    | I
12-15       | m_ByteSize         | int32    | 4    | i
16-19       | m_Index            | int32    | 4    | i
20-23       | m_MetaFlag         | int32    | 4    | i
            |                    | TOTAL    | 24   |
```

**Binary Layout (v >= 19)**:
```
Byte Offset | Field              | Type     | Size | Format Code
------------|-------------------|----------|------|-------------
0-1         | m_Version          | int16    | 2    | h
2           | m_Level            | uint8    | 1    | B
3           | m_TypeFlags        | uint8    | 1    | B
4-7         | m_TypeStrOffset    | uint32   | 4    | I
8-11        | m_NameStrOffset    | uint32   | 4    | I
12-15       | m_ByteSize         | int32    | 4    | i
16-19       | m_Index            | int32    | 4    | i
20-23       | m_MetaFlag         | int32    | 4    | i
24-31       | m_RefTypeHash      | uint64   | 8    | Q
            |                    | TOTAL    | 32   |
```

#### Legacy Format (Old)

**File**: `UnityPy/helpers/TypeTreeNode.py:89-103`

```
Field Order (v=3, v != 2):
1. m_Type: null-terminated string (varies: strlen + 1 bytes)
2. m_Name: null-terminated string (varies: strlen + 1 bytes)
3. m_ByteSize: int32 (4 bytes)
4. [SKIP if version == 3] m_Index: int32 (4 bytes)
5. m_TypeFlags: int32 (4 bytes)  ← NOTE: int32, not uint8!
6. m_Version: int32 (4 bytes)
7. [SKIP if version == 3] m_MetaFlag: int32 (4 bytes)
8. children_count: int32 (4 bytes)  ← THE KEY: child count in binary!

Total: 20-28 bytes + string lengths (variable)
```

#### Key Difference
```
BLOB:   Strings are offsets (4 bytes each) + separate string pool
LEGACY: Strings are inline null-terminated (variable length each)
```

---

## Question 3: How are children read? How many bytes does it take to read one child?

### Answer

#### Blob Format: NO separate child count field

**Children are determined by m_Level values**

```python
# File: TypeTreeNode.py:145-151
if node.m_Level > prev.m_Level:
    stack.append(parent)
    parent = prev
elif node.m_Level < prev.m_Level:
    while node.m_Level <= parent.m_Level:
        parent = stack.pop()

parent.m_Children.append(node)
```

**Bytes per node**: 24 (v < 19) or 32 (v >= 19)

No additional bytes read for children - **children are derived from level comparison**.

#### Legacy Format: Explicit child count

**File**: `UnityPy/helpers/TypeTreeNode.py:103-106`

```python
# Read this node
node = TypeTreeNode(
    m_Level=parent.m_Level + 1,
    m_Type=reader.read_string_to_null(),
    m_Name=reader.read_string_to_null(),
    # ... more fields ...
)

# THEN read how many children this node has
children_count = reader.read_int()  # 4 bytes!

if children_count > 0:
    node.m_Children = [dummy_node] * children_count
```

**Bytes per node**:
```
- Strings: variable
- m_ByteSize: 4 bytes
- m_Index: 4 bytes (if version != 3)
- m_TypeFlags: 4 bytes
- m_Version: 4 bytes
- m_MetaFlag: 4 bytes (if version != 3)
- children_count: 4 bytes ← ALWAYS HERE
- MINIMUM total: 20 bytes + string lengths
```

### Summary

| Format | Child count | Source | Bytes to read |
|--------|---|---|---|
| Blob | Implicit | m_Level comparison | 0 (derived) |
| Legacy | Explicit | Binary field (int32) | 4 bytes per node |

---

## Question 4: How does it determine the number of children for a node?

### Answer

#### Blob Format: Level-Based Derivation

**File**: `UnityPy/helpers/TypeTreeNode.py:145-151`

```python
for raw_node in node_struct.iter_unpack(struct_data):
    node = cls(...)
    
    # DETERMINE PARENT-CHILD RELATIONSHIP
    if node.m_Level > prev.m_Level:
        # node is child of prev
        stack.append(parent)
        parent = prev
    elif node.m_Level < prev.m_Level:
        # node is at higher level, find correct parent
        while node.m_Level <= parent.m_Level:
            parent = stack.pop()
    
    # Add as child of current parent
    parent.m_Children.append(node)
    prev = node
```

**Algorithm**:
1. Read all nodes sequentially
2. Compare each node's `m_Level` with previous node's level
3. If level increases: previous node becomes parent
4. If level decreases: backtrack stack to find correct parent level
5. Add node to parent's children list
6. After all nodes parsed, children count = parent.m_Children.Count

**Example**:
```
Nodes read:
0: Transform (Level=0)
1: Vector3f (Level=1)  ← Level > prev, so Transform is parent of Vector3f
2: float x (Level=2)   ← Level > prev, so Vector3f is parent of float x
3: float y (Level=2)   ← Level == prev, sibling of float x
4: Matrix4x4 (Level=1) ← Level < prev, backtrack to find Level 0 parent (Transform)
                          Then Transform becomes parent of Matrix4x4

Result:
Transform (2 children: Vector3f, Matrix4x4)
├─ Vector3f (1 child: float x)
│  ├─ float x
│  └─ float y   ← NOT a child of Vector3f, but of same parent due to level
└─ Matrix4x4
```

#### Legacy Format: Explicit Field in Binary

**File**: `UnityPy/helpers/TypeTreeNode.py:103`

```python
# After reading node fields...
children_count = reader.read_int()  # 4 bytes from binary stream
```

**The binary stream directly specifies**:
- How many children each node has
- Parser pre-allocates array
- Children are filled in next iterations

**Example**:
```
Binary stream:
[node1_type] [node1_name] ... [node1_children_count=2]
  [child1_type] [child1_name] ... [child1_children_count=0]
  [child2_type] [child2_name] ... [child2_children_count=0]
[node2_type] [node2_name] ... [node2_children_count=1]
  [child1_type] [child1_name] ... [child1_children_count=0]
```

### Summary

| Format | Method | Source of Info |
|--------|--------|---|
| Blob | Derived from m_Level values | Sequential node comparison |
| Legacy | Read from binary field | Explicit int32 after node fields |

---

## Question 5: If UnityPy asserts "Array must have exactly 2 children", how does it READ the children to guarantee getting 2?

### Answer

UnityPy **does NOT actively read children for an Array node**. Instead:

1. **All children are already parsed** before validation
2. **The assertion is a POST-CONDITION check** (verify, don't build)
3. **Array structure is created by Unity**, not UnityPy

#### The Reading Process

**File**: `UnityPy/helpers/TypeTreeHelper.py:206-220`

```python
# Read a value that contains an Array
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    if metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
        align = True
    
    # The array_wrapper_node is node.m_Children[0]
    array_wrapper_node = node.m_Children[0]
    
    # IMPLICIT ASSUMPTION: array_wrapper_node has exactly 2 children:
    # array_wrapper_node.m_Children[0] = size descriptor (int32 usually)
    # array_wrapper_node.m_Children[1] = element template
    
    size = reader.read_int()  # Read size from binary data stream
    if size < 0:
        raise ValueError("Negative length read from TypeTree")
    
    # Get element template
    element_template = array_wrapper_node.m_Children[1]
    
    # Read array elements
    if metaflag_is_aligned(element_template.m_MetaFlag):
        value = read_value_array(element_template, reader, config, size)
    else:
        value = [read_value(element_template, reader, config) for _ in range(size)]
```

#### The Assertion (C++)

**File**: `UnityPyBoost/TypeTreeHelper.cpp:808-810`

```cpp
if (PyList_GET_SIZE(child->m_Children) != 2)
{
    PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
    return NULL;
}
```

**This is checking** after all TypeTree parsing is complete.

#### Why Exactly 2 Children?

**Array nodes created by Unity always have**:

```
Array wrapper node
├─ Child[0]: Size descriptor
│  └─ m_Type: "int" (or sometimes "uint")
│     m_ByteSize: 4
│
└─ Child[1]: Element template
   └─ m_Type: "SubMesh" (or whatever element type)
      (recursively has its own children)
```

**When reading data**:
```
Binary stream:
[3 (array size as int32)]  ← Read from stream
[elem0_data]               ← Use Child[1] template to read
[elem1_data]               ← Use Child[1] template to read
[elem2_data]               ← Use Child[1] template to read
```

#### How the 2 Children are Guaranteed

1. **During TypeTree parsing** (blob or legacy format), children are built based on levels or explicit counts
2. **For Array-typed nodes**, Unity's serializer ALWAYS creates exactly 2 children
3. **If a file has != 2**: It's corrupted (not a parsing bug)
4. **UnityPy validates** with an assertion to catch corrupted files early

### Code Flow

```python
# Step 1: Parse ALL nodes from binary (blob or legacy)
nodes = ParseTypeTreeNodesBlob(reader, version)
# Result: flat list of TypeTreeNode objects

# Step 2: Build tree from levels (blob) or counts (legacy)
# Result: hierarchical tree with children lists populated

# Step 3: Later, when READING DATA from serialized objects:
def read_value(node):
    if node.m_Type == "Array":
        # Children were already built in Step 2
        # Just verify and use them
        assert len(node.m_Children) == 2
        
        size = reader.read_int()  # Read COUNT from data section
        template = node.m_Children[1]
        
        # Read 'size' elements using template
        for i in range(size):
            read_value(template, reader)
```

### Summary

| Step | What Happens | Bytes Read |
|------|---|---|
| 1. Parse TypeTree nodes | Read node structs from metadata | 24-32 bytes per node |
| 2. Build children lists | Connect nodes into tree | 0 extra bytes |
| 3. Validate Array nodes | Assert exactly 2 children | 0 extra bytes |
| 4. Read data from stream | Read int32 array size | 4 bytes + (size × element_bytes) |
| 5. Use Child[1] template | Repeatedly apply template | Depends on element type |

**The "2 children" is NOT actively read — it's a property of the pre-built tree that UnityPy validates.**

---

## Complete Example: Step-by-Step

### File: `vector<SubMesh>` field in Mesh

```
TypeTree (after parsing):
Mesh (Level=0)
└─ m_SubMeshes (Level=1, m_Type="vector")
   ├─ Child[0]: Array (Level=2, m_Type="Array")
   │  ├─ Child[0]: int (Level=3, m_Type="int")     ← Size descriptor
   │  └─ Child[1]: SubMesh (Level=3, m_Type="SubMesh") ← Template
   │     ├─ vertexStart (Level=4, m_Type="int")
   │     ├─ vertexCount (Level=4, m_Type="int")
   │     ├─ triangleStart (Level=4, m_Type="int")
   │     └─ triangleCount (Level=4, m_Type="int")

Reading data:
1. At m_SubMeshes node: "vector" type → delegate to array reading
2. Get wrapper: m_SubMeshes.m_Children[0] (the Array node)
3. Validate: Array.m_Children.Count == 2 ✓
4. Read size: 3 (int32 from data stream)
5. Get template: Array.m_Children[1]
6. For each of 3 elements:
   - Read vertexStart (int32)
   - Read vertexCount (int32)
   - Read triangleStart (int32)
   - Read triangleCount (int32)
```

---

## Key Files Summary

| Question | Answer Location | Lines |
|----------|---|---|
| How parse? | TypeTreeNode.py | 112-154 (blob) or 79-109 (legacy) |
| Binary format? | TypeTreeNode.py | 204-215 |
| Read children? | TypeTreeNode.py | 145-151 (blob) or 103 (legacy) |
| How many children? | TypeTreeNode.py | Blob: Level comparison; Legacy: int32 field |
| Array assertion? | TypeTreeHelper.cpp | 808-810 |

