# TypeTree Format Analysis: Legacy vs Blob

## Summary
**YES, there are TWO different TypeTree formats** in UnityPy, and the version check determines which one to use.

---

## 1. Format Detection Logic

**Location**: [UnityPy/files/SerializedFile.py#L116-L139](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/files/SerializedFile.py#L116-L139)

```python
def __init__(self, reader: EndianBinaryReader, serialized_file: SerializedFile, is_ref_type: bool):
    version = serialized_file.header.version
    # ... other code ...
    
    if serialized_file._enable_type_tree:
        if version >= 12 or version == 10:
            self.node = TypeTreeNode.parse_blob(reader, version)  # BLOB FORMAT
        else:
            self.node = TypeTreeNode.parse(reader, version)       # LEGACY FORMAT
```

### Version Cutoff
- **Blob Format (New)**: Version >= 12 OR version == 10
- **Legacy Format (Old)**: Version < 10 (i.e., versions 2-9)

**This is the key version check that determines format!**

---

## 2. Legacy Format Parser

**Location**: [UnityPy/helpers/TypeTreeNode.py#L79-L109](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L79-L109)

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

### Legacy Field Reading Order
1. `m_Type` (string, null-terminated)
2. `m_Name` (string, null-terminated)
3. `m_ByteSize` (int32)
4. `m_VariableCount` (int32, **only if version == 2**)
5. `m_Index` (int32, **NOT if version == 3**)
6. `m_TypeFlags` (int32)
7. `m_Version` (int32)
8. `m_MetaFlag` (int32, **NOT if version == 3**)
9. `children_count` (int32)

**Note**: The order is NOT hardcoded; fields are read in strict sequential order. Version-specific fields are conditionally skipped.

---

## 3. Blob Format Parser

**Location**: [UnityPy/helpers/TypeTreeNode.py#L112-L154](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L112-L154)

```python
@classmethod
def parse_blob(cls, reader: EndianBinaryReader, version: int) -> TypeTreeNode:
    node_count = reader.read_int()
    stringbuffer_size = reader.read_int()

    node_struct, keys = _get_blob_node_struct(reader.endian, version)
    struct_data = reader.read(node_struct.size * node_count)
    stringbuffer_reader = EndianBinaryReader(reader.read(stringbuffer_size), reader.endian)

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
        # ... tree building logic ...
    return fake_root.m_Children[0]
```

### Blob Struct Definition

**Location**: [UnityPy/helpers/TypeTreeNode.py#L330-L347](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L330-L347)

```python
def _get_blob_node_struct(endian: str, version: int) -> tuple[Struct, list[str]]:
    struct_type = f"{endian}hBBIIiii"
    keys = [
        "m_Version",        # h (int16)
        "m_Level",          # B (uint8)
        "m_TypeFlags",      # B (uint8)
        "m_TypeStrOffset",  # I (uint32) - OFFSET
        "m_NameStrOffset",  # I (uint32) - OFFSET
        "m_ByteSize",       # i (int32)
        "m_Index",          # i (int32)
        "m_MetaFlag",       # i (int32)
    ]
    if version >= 19:
        struct_type += "Q"
        keys.append("m_RefTypeHash")  # Q (uint64)

    return Struct(struct_type), keys
```

### Blob Format Field Order (Binary Structure)
1. `m_Version` (int16 - **NOT int32!**)
2. `m_Level` (uint8 - **NOT missing!**)
3. `m_TypeFlags` (uint8 - **NOT int32!**)
4. `m_TypeStrOffset` (uint32) - points to string buffer
5. `m_NameStrOffset` (uint32) - points to string buffer
6. `m_ByteSize` (int32)
7. `m_Index` (int32)
8. `m_MetaFlag` (int32)
9. `m_RefTypeHash` (uint64, **only if version >= 19**)

---

## 4. Field Type Differences (Legacy vs Blob)

**Evidence from UnityPyBoost C++ header**:

**Location**: [UnityPyBoost/TypeTreeHelper.hpp#L32-L49](https://github.com/K0lb3/UnityPy/blob/master/UnityPyBoost/TypeTreeHelper.hpp#L32-L49)

```cpp
struct TypeTreeNodeObject {
    // ... other fields ...
    
    PyObject *m_Level;         // legacy: /, blob: u8
    PyObject *m_ByteSize;      // legacy: i32, blob: i32
    PyObject *m_Version;       // legacy: i32, blob: i16
    PyObject *m_TypeFlags;     // legacy: i32, blob: u8
    PyObject *m_VariableCount; // legacy: i32, blob: /
    PyObject *m_Index;         // legacy: i32, blob: i32
    PyObject *m_MetaFlag;      // legacy: i32, blob: i32
    PyObject *m_RefTypeHash;   // legacy: /, blob: u64
}
```

### Comparison Table

| Field | Legacy (v2-9) | Blob (v≥12 or v10) |
|-------|---------------|-------------------|
| **m_Level** | NOT STORED | uint8 |
| **m_Type** | null-terminated string | string offset (uint32) |
| **m_Name** | null-terminated string | string offset (uint32) |
| **m_ByteSize** | int32 | int32 |
| **m_Version** | int32 | int16 |
| **m_TypeFlags** | int32 | uint8 |
| **m_VariableCount** | int32 (v==2 only) | NOT STORED |
| **m_Index** | int32 (v!=3 only) | int32 |
| **m_MetaFlag** | int32 (v!=3 only) | int32 |
| **m_RefTypeHash** | NOT STORED | uint64 (v≥19 only) |

---

## 5. Why One Works But Another Doesn't

The "hardcoded order" problem you mentioned stems from the **different struct layouts**:

1. **Legacy format**: Fields read sequentially from byte stream in text form (null-terminated strings)
   - No alignment issues within the format itself
   - But strings are variable-length

2. **Blob format**: Binary-packed struct with fixed offsets
   - String offsets instead of inline strings
   - Fixed binary layout: `hBBIIiii` (little or big endian)
   - v≥19 adds 8-byte `m_RefTypeHash` at end

A bundle created in **Unity 4.x or early 5.x** (version < 10) uses **legacy format**. Later Unity versions (5.5+) use **blob format**. If you parse one with the other's parser, you'll read garbage because:
- Legacy parser expects strings but blob provides offsets
- Blob parser expects fixed-size struct but legacy has variable-length data

---

## 6. Key References from UnityPy

1. **Detection Logic**: [SerializedFile.py#L116-L139](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/files/SerializedFile.py#L116-L139)
2. **Legacy Parser**: [TypeTreeNode.py#L79-L109](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L79-L109)
3. **Blob Parser**: [TypeTreeNode.py#L112-L154](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L112-L154)
4. **Blob Struct**: [TypeTreeNode.py#L330-L347](https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py#L330-L347)
5. **Field Type Map**: [TypeTreeHelper.hpp#L32-L49](https://github.com/K0lb3/UnityPy/blob/master/UnityPyBoost/TypeTreeHelper.hpp#L32-L49)

---

## Conclusion

The TypeTree format switching is **version-based**:
- **Version < 10** → Legacy format (text strings)
- **Version 10 or ≥ 12** → Blob format (binary struct with string offsets)
- **Version 11** → (appears to be skipped/unused)

Use the detection logic from `SerializedFile.py` line 134-135 to decide which parser to call. Port **both** `parse()` and `parse_blob()` methods to C#, and make sure your C# code checks the file version before choosing which parsing path to take.
