# UnityPy TypeTree Parsing - Exact Source Code References

**Document Purpose**: Direct references to UnityPy source with exact line numbers  
**Repository**: https://github.com/K0lb3/UnityPy  
**Last Verified**: February 2, 2026

---

## File 1: TypeTree Binary Struct Definition

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 204-215

```python
def _get_blob_node_struct(endian: str, version: int) -> tuple[Struct, list[str]]:
    struct_type = f"{endian}hBBIIiii"
    keys = [
        "m_Version",
        "m_Level",
        "m_TypeFlags",
        "m_TypeStrOffset",
        "m_NameStrOffset",
        "m_ByteSize",
        "m_Index",
        "m_MetaFlag",
    ]
    if version >= 19:
        struct_type += "Q"
        keys.append("m_RefTypeHash")

    return Struct(struct_type), keys
```

**What this tells us**:
- Struct format string: `hBBIIiii` (or `hBBIIiiiQ` for v >= 19)
- `h` = short (2 bytes)
- `B` = byte (1 byte)
- `I` = unsigned int (4 bytes)
- `i` = signed int (4 bytes)
- `Q` = unsigned long long (8 bytes)
- **Total: 24 bytes (v < 19) or 32 bytes (v >= 19)**

---

## File 2: Blob Format Parsing Algorithm

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 112-154 (parse_blob classmethod)

```python
@classmethod
def parse_blob(cls, reader: EndianBinaryReader, version: int) -> TypeTreeNode:
    node_count = reader.read_int()
    stringbuffer_size = reader.read_int()

    node_struct, keys = _get_blob_node_struct(reader.endian, version)
    struct_data = reader.read(node_struct.size * node_count)
    stringbuffer_reader = EndianBinaryReader(
        reader.read(stringbuffer_size), reader.endian
    )

    CommonString = get_common_strings()

    def read_string(reader: EndianBinaryReader, value: int) -> str:
        is_offset = (value & 0x80000000) == 0
        if is_offset:
            reader.Position = value
            return reader.read_string_to_null()

        offset = value & 0x7FFFFFFF
        return CommonString.get(offset, str(offset))

    fake_root: TypeTreeNode = cls(-1, "", "", 0, 0, [])
    stack: List[TypeTreeNode] = [fake_root]
    parent = fake_root
    prev = fake_root

    for raw_node in node_struct.iter_unpack(struct_data):
        node = cls(
            **dict(zip(keys[:3], raw_node[:3])),
            **dict(zip(keys[5:], raw_node[5:])),
            m_Type=read_string(stringbuffer_reader, raw_node[3]),
            m_Name=read_string(stringbuffer_reader, raw_node[4]),
        )

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

**Key lines**:
- Line 113: `node_count = reader.read_int()` — How many nodes
- Line 114: `stringbuffer_size = reader.read_int()` — String pool size
- Line 117: `struct_data = reader.read(node_struct.size * node_count)` — Read all nodes in one shot
- Line 134: `for raw_node in node_struct.iter_unpack(struct_data):` — Parse each struct
- Line 145-151: **Tree building logic** using level comparison

---

## File 3: Legacy Format Parsing Algorithm

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 79-109 (parse classmethod)

```python
@classmethod
def parse(cls, reader: EndianBinaryReader, version: int) -> TypeTreeNode:
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

        node = cls(
            m_Level=parent.m_Level + 1,
            m_Type=reader.read_string_to_null(),
            m_Name=reader.read_string_to_null(),
            m_ByteSize=reader.read_int(),
            m_VariableCount=reader.read_int() if version == 2 else None,
            m_Index=reader.read_int() if version != 3 else None,
            m_TypeFlags=reader.read_int(),
            m_Version=reader.read_int(),
            m_MetaFlag=reader.read_int() if version != 3 else None,
        )
        parent.m_Children[-count] = node
        children_count = reader.read_int()
        if children_count > 0:
            node.m_Children = [dummy_node] * children_count
        stack.append((node, children_count))
    return dummy_root.m_Children[0]
```

**Key lines**:
- Line 89-96: Read one node's fields from binary
  - `reader.read_string_to_null()` — Null-terminated string (varies in size)
  - `reader.read_int()` — 4-byte int32
- Line 103: **`children_count = reader.read_int()`** — THE KEY DIFFERENCE! Children count is in binary
- Line 104-105: Pre-allocate children array based on count
- Line 106: `stack.append((node, children_count))` — Add to stack with count for next iteration

---

## File 4: Array Assertion (C++ Compiled Version)

**File**: `UnityPyBoost/TypeTreeHelper.cpp`  
**Lines**: 808-810 (read_typetree_value function)

```cpp
if (PyList_GET_SIZE(child->m_Children) != 2)
{
    PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
    return NULL;
}
```

**Context**: This is inside the array handling code:

```cpp
if (child && child->_data_type == NodeDataType::Array)
{
    // ARRAY HANDLING
    if (PyList_GET_SIZE(child->m_Children) != 2)
    {
        PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
        return NULL;
    }

    if (child->_align)
    {
        align = true;
    }
    int32_t length;
    if (!_read_length<swap>(reader, &length))
    {
        return NULL;
    }

    // Get element template from index [1]
    child = (TypeTreeNodeObject *)PyList_GET_ITEM(child->m_Children, 1);
}
```

**What's happening**:
- Line 2: Check if node is Array type
- Line 5-8: **Assert that Array has exactly 2 children**
- Later: Read array length (int32)
- Later: Get child[1] as the element template

---

## File 5: Python Array Reading (Pure Python, No C++)

**File**: `UnityPy/helpers/TypeTreeHelper.py`  
**Lines**: 206-220 (read_value function, Vector/Array case)

```python
# Vector
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    if metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
        align = True

    # size = read_value(node.m_Children[0].m_Children[0], reader, as_dict)
    size = reader.read_int()
    if size < 0:
        raise ValueError("Negative length read from TypeTree")
    subtype = node.m_Children[0].m_Children[1]
    if metaflag_is_aligned(subtype.m_MetaFlag):
        value = read_value_array(subtype, reader, config, size)
    else:
        value = [read_value(subtype, reader, config) for _ in range(size)]
```

**What's happening**:
- Line 207: Check if node has children AND first child is Array type
- Line 211: **Read array size from binary (int32)**
- Line 214: **Get element template from child[1]** (skipping child[0])
- Line 215-218: Read `size` elements using the template

---

## File 6: SerializedFile Type Parsing

**File**: `UnityPy/files/SerializedFile.py`  
**Lines**: (in SerializedType.__init__)

```python
if serialized_file._enable_type_tree:
    if version >= 12 or version == 10:
        self.node = TypeTreeNode.parse_blob(reader, version)
    else:
        self.node = TypeTreeNode.parse(reader, version)
```

**What decides which parser to use**:
- Version >= 12: Use blob format (modern)
- Version == 10: Use blob format (special case)
- Version < 10 or version == 11: Use legacy format (old)

---

## File 7: TypeTreeNode Class Definition

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 43-65 (TypeTreeNodeC class in attrs)

```python
@define(slots=True)
class TypeTreeNodeC:
    m_Level: int
    m_Type: str
    m_Name: str
    m_ByteSize: int
    m_Version: int
    m_Children: List[TypeTreeNode] = field(factory=list)
    m_TypeFlags: Optional[int] = None
    m_VariableCount: Optional[int] = None
    m_Index: Optional[int] = None
    m_MetaFlag: Optional[int] = None
    m_RefTypeHash: Optional[int] = None
```

**Key fields**:
- `m_Level` — Depth in tree (used to determine parent-child relationships)
- `m_Children` — List of child nodes (built after parsing binary structs)
- `m_TypeFlags` — Bit flags (bit 0x01 = array, etc.)

---

## File 8: String Reading from Blob String Pool

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 122-128 (read_string helper inside parse_blob)

```python
def read_string(reader: EndianBinaryReader, value: int) -> str:
    is_offset = (value & 0x80000000) == 0
    if is_offset:
        reader.Position = value
        return reader.read_string_to_null()

    offset = value & 0x7FFFFFFF
    return CommonString.get(offset, str(offset))
```

**What's happening**:
- If bit 0x80000000 is NOT set: value is an offset into the string pool
  - Seek to that position in pool
  - Read null-terminated string
- If bit 0x80000000 IS set: value is a reference to a common/predefined string
  - Extract the lower 31 bits
  - Look up in CommonString dictionary

---

## File 9: Common Strings (Predefined String Pool)

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 167-181 (get_common_strings function)

```python
def get_common_strings(version: Optional[UnityVersion] = None) -> Dict[int, str]:
    if version in COMMONSTRING_CACHE:
        return COMMONSTRING_CACHE[version]

    from .Tpk import TPKTYPETREE

    tree = TPKTYPETREE
    common_string = tree.CommonString
    strings = common_string.GetStrings(tree.StringBuffer)
    if version:
        count = common_string.GetCount(version)
        strings = strings[:count]

    ret: Dict[int, str] = {}
    offset = 0
    for string in strings:
        ret[offset] = string
        offset += len(string) + 1
    COMMONSTRING_CACHE[version] = ret
    return ret
```

**What's happening**:
- Loads predefined common strings (e.g., "Transform", "Vector3f", "int", "float")
- Maps them to byte offsets in a string buffer
- Used for quick lookup when reading blob format

---

## File 10: Dumping (Writing) TypeTree Back to Binary

**File**: `UnityPy/helpers/TypeTreeNode.py`  
**Lines**: 276-296 (dump_blob method)

```python
def dump_blob(self, writer: EndianBinaryWriter, version: int):
    node_writer = EndianBinaryWriter(endian=writer.endian)
    string_writer = EndianBinaryWriter()

    # string buffer setup
    CommonStringOffsetMap = {string: offset for offset, string in 
                            get_common_strings().items()}

    string_offsets: dict[str, int] = {}

    def write_string(string: str) -> int:
        offset = string_offsets.get(string)
        if offset is None:
            common_offset = CommonStringOffsetMap.get(string)
            if common_offset:
                offset = common_offset | 0x80000000
            else:
                offset = string_writer.Position
                string_writer.write_string_to_null(string)
                string_offsets[string] = offset
        return offset

    # ... (write nodes and strings)
    
    node_count = len([write_node(node) for node in self.traverse()])

    # write blob header
    writer.write_int(node_count)
    writer.write_int(string_writer.Position)
    writer.write(node_writer.bytes)
    writer.write(string_writer.bytes)
```

**Key understanding**:
- When writing blob format back, we:
  1. Traverse tree (depth-first)
  2. Build string pool
  3. Assign offsets (either common string ID or custom pool offset)
  4. Write nodeCount + stringBufSize header
  5. Write all node structs
  6. Write string pool

---

## Summary Table: Location of Each Answer

| Question | File | Lines | Key Code |
|----------|------|-------|----------|
| Binary format of node? | TypeTreeNode.py | 204-215 | `hBBIIiii` struct |
| How parse blob? | TypeTreeNode.py | 112-154 | `parse_blob` method |
| How parse legacy? | TypeTreeNode.py | 79-109 | `parse` method |
| How many children for node? | TypeTreeNode.py | 145-151 | Level comparison logic |
| How to read children count? | TypeTreeNode.py | 103 | `children_count = reader.read_int()` |
| Array assertion? | TypeTreeHelper.cpp | 808-810 | Size check != 2 |
| How read array size? | TypeTreeHelper.py | 211 | `size = reader.read_int()` |
| How get element template? | TypeTreeHelper.py | 214 | `subtype = node.m_Children[0].m_Children[1]` |
| Bytes per node (blob)? | TypeTreeNode.py | 204-215 | 24 or 32 bytes |
| String pool lookup? | TypeTreeNode.py | 122-128 | Check bit 0x80000000 |

---

## Direct GitHub Links

For browsing the actual source:

1. **TypeTreeNode parsing**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L79
2. **Array reading**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeHelper.py#L206
3. **SerializedFile**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/files/SerializedFile.py
4. **C++ boost**: https://github.com/K0lb3/UnityPy/tree/master/UnityPyBoost

---

## How to Use These References

When implementing in C#:

```csharp
// 1. Look at TypeTreeNode.py:204-215 for struct layout
// → Use BinaryReader with exact field order

// 2. Look at TypeTreeNode.py:112-154 for blob parsing
// → Implement the tree-building loop

// 3. Look at TypeTreeHelper.py:206-220 for array reading
// → Read int32 size, then read elements using child[1]

// 4. Check line 103 in TypeTreeNode.py
// → Legacy format has children count in binary!

// 5. Validate against C++ boost if confused
// → TypeTreeHelper.cpp:808 shows the 2-children assertion
```

