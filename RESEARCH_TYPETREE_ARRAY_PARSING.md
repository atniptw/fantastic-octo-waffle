# UnityPy TypeTree Array Parsing Research

## Overview

This document provides a detailed analysis of how UnityPy parses TypeTree arrays from binary data, including the algorithm for element template selection, Array wrapper node handling, and special cases like vectors.

---

## 1. High-Level Array Parsing Algorithm

### Entry Point

The main entry point for reading TypeTree values is:

**File**: `UnityPy/helpers/TypeTreeHelper.py`  
**Function**: `read_value()` (lines 186-260)

```python
def read_value(
    node: TypeTreeNode,
    reader: EndianBinaryReader,
    config: TypeTreeConfig,
) -> Any:
    # Handles single values: primitives, strings, pairs, classes
    # Also handles the VECTOR pattern (arrays of arrays)
```

The vector pattern is detected and handled in `read_value()` around **lines 206-220**:

```python
# Vector
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    if metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
        align = True

    # size = read_value(node.m_Children[0].m_Children[0], reader, as_dict)
    size = reader.read_int()  # Read array length (4 bytes, int32)
    if size < 0:
        raise ValueError("Negative length read from TypeTree")
    subtype = node.m_Children[0].m_Children[1]  # Get element template at index [1]
    if metaflag_is_aligned(subtype.m_MetaFlag):
        value = read_value_array(subtype, reader, config, size)
    else:
        value = [read_value(subtype, reader, config) for _ in range(size)]
```

---

## 2. Exact Algorithm for Array Element Template Selection

### Key Finding: Array Wrapper Node Structure

**The "Array" wrapper node always has exactly 2 children**:

1. **Child [0]**: Size node (usually unused, can be ignored)
2. **Child [1]**: **DATA TEMPLATE** - The actual element type template to use for all elements

### Code Reference

**File**: `UnityPyBoost/TypeTreeHelper.cpp` (C++ compiled optimization)  
**Function**: `read_typetree_value()` at lines 800-826

```cpp
if (child && child->_data_type == NodeDataType::Array)
{
    // array
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
    if (!_read_length<swap>(reader, &length))  // Read array length
    {
        return NULL;
    }

    child = (TypeTreeNodeObject *)PyList_GET_ITEM(child->m_Children, 1);  // INDEX [1] = TEMPLATE
    // ... then read 'length' elements using 'child' as template
}
```

### Python Equivalent in Pure Python

**File**: `UnityPy/helpers/TypeTreeHelper.py` lines 206-220

```python
# Vector (dynamic array with length prefix)
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    if metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
        align = True
    
    # Get the Array wrapper node's first child (index 0 = size, unused)
    # Get the Array wrapper node's second child (index 1 = TEMPLATE)
    size = reader.read_int()  # Read the actual array count from binary
    if size < 0:
        raise ValueError("Negative length read from TypeTree")
    
    subtype = node.m_Children[0].m_Children[1]  # THIS IS THE TEMPLATE
    
    # Use the template to read 'size' elements
    if metaflag_is_aligned(subtype.m_MetaFlag):
        value = read_value_array(subtype, reader, config, size)
    else:
        value = [read_value(subtype, reader, config) for _ in range(size)]
```

---

## 3. What the Array Wrapper Nodes Represent

### Structure

An "Array" node is a **container that wraps a dynamic-length list** in the TypeTree:

```
Parent Node (e.g., "Submeshes")
└── Array (m_Type == "Array")
    ├── Child[0]: Size node (optional reference, usually has m_ByteSize=0)
    └── Child[1]: Element template (THE ACTUAL DATA TEMPLATE)
```

### Semantics

- **Array Node Purpose**: Indicates that the parent node contains a **variable-length array**
- **Array Node Children**:
  - `[0]` = Metadata about size encoding (rarely used)
  - `[1]` = **The template to use for each array element**
- **Not a Class**: The "Array" node itself is NOT a class/struct—it's a **structural directive**

### Example: vector<SubMesh>

If you have a `vector<SubMesh>` in the TypeTree:

```
Renderer
├── m_Submeshes (type: "vector")  ← Parent node
    └── Array (m_Type: "Array")   ← Wrapper
        ├── Child[0]: Size info
        └── Child[1]: SubMesh template  ← Use THIS for each element
```

When reading `m_Submeshes`:
1. Read the **int32 array length** from binary (e.g., 3)
2. Use `Child[1]` template to read **3 SubMesh objects**

---

## 4. Relationship Between Array Node's Children and Element Type

### The Two-Child Structure Guarantees

**Assertion Check** (line 809 in TypeTreeHelper.cpp):
```cpp
if (PyList_GET_SIZE(child->m_Children) != 2)
{
    PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
    return NULL;
}
```

**Meaning**: Every valid Array node MUST have exactly 2 children, no more, no less.

### Child[0] (Size Node)

- Usually has minimal or placeholder data
- In older formats, might contain encoded size information
- **Typically ignored** in modern UnityPy (length is read directly from binary as int32)
- Marked with `m_Type` that indicates size encoding (rarely used)

### Child[1] (Element Template)

- **THIS IS THE CRITICAL CHILD**
- Contains the full TypeTree description of ONE element
- Could be:
  - A primitive type (int, float, bool, etc.)
  - A complex class (SubMesh, Mesh, etc.)
  - Even nested structures or pairs
- **Used repetitively** to read N elements (where N = array length read from binary)

### Algorithm for Selecting Template

```python
# Pseudocode
def get_array_element_template(array_node):
    """Given an Array wrapper node, return the template for elements"""
    if len(array_node.m_Children) != 2:
        raise ValueError("Invalid Array node structure")
    
    element_template = array_node.m_Children[1]  # Always index 1
    return element_template
```

---

## 5. How UnityPy Reads Each Element Using the Template

### Flow Diagram

```
Binary Data
    ↓
Read int32 (array length = N)
    ↓
For i = 0 to N-1:
    ├─ Template = ArrayNode.m_Children[1]
    ├─ Call read_value(Template, reader, config)
    │   (Template describes what to read for ONE element)
    └─ Add result to list
    ↓
Return list of N elements
```

### Code Implementation

**For aligned/simple types** (lines 826-843 in TypeTreeHelper.cpp):

```cpp
if (std::find(..., SUPPORTED_VALUE_ARRAY_READ_TYPES, ...) != ...)
{
    // Complex element type: iterate and read each one
    value = PyList_New(length);
    for (int i = 0; i < length; i++)
    {
        PyObject *item = read_typetree_value<swap>(reader, child, config);
        // 'child' is the element template (from Array.m_Children[1])
        if (item == NULL)
        {
            Py_DECREF(value);
            return NULL;
        }
        PyList_SET_ITEM(value, i, item);
    }
}
else
{
    // Optimized path for simple types (uint, float, etc.)
    value = read_typetree_value_array<swap>(reader, child, config, length);
}
```

**In pure Python** (lines 271-361 in TypeTreeHelper.py):

```python
def read_value_array(node, reader, config, size):
    """Read an array of 'size' elements, where node is the element template"""
    align = metaflag_is_aligned(node.m_MetaFlag)
    
    # Check if simple type with fast path
    func = FUNCTION_READ_MAP_ARRAY.get(node.m_Type)
    if func:
        value = func(reader, size)  # Direct array read for primitives
    
    # For complex types
    elif node.m_Type == "pair":
        # ... read key-value pairs
    elif node.m_Children and node.m_Children[0].m_Type == "Array":
        # Nested array (array of arrays)
        # Read inner size and recursively read elements
    elif node.m_Type startswith "PPtr<":
        # Pointer array
    else:
        # Class array: read N instances of the class
        value = [
            {child._clean_name: read_value(child, reader, config) 
             for child in node.m_Children}
            for _ in range(size)
        ]
    
    if align:
        reader.align_stream()
    return value
```

---

## 6. Special Handling for Complex Types

### vector<SubMesh> Pattern

**C# Class Example**:
```csharp
public class Mesh
{
    public SubMesh[] submeshes;  // This becomes vector<SubMesh> in TypeTree
}
```

**TypeTree Structure**:
```
Mesh (class)
├── m_Name: string
├── m_Vertices: Vector<Vector3>
│   └── Array
│       ├── Child[0]: size metadata
│       └── Child[1]: Vector3 template
└── m_Submeshes: Vector<SubMesh>
    └── Array
        ├── Child[0]: size metadata
        └── Child[1]: SubMesh template
            ├── indexStart: uint32
            ├── indexCount: uint32
            ├── topology: int32
            └── firstVertex: uint32
```

**Reading Process**:
1. Detect first child is "Array"
2. Read length from binary (e.g., 3)
3. Use Child[1] (SubMesh template) 3 times
4. For each SubMesh:
   - Read indexStart (uint32)
   - Read indexCount (uint32)
   - Read topology (int32)
   - Read firstVertex (uint32)

### Nested Arrays (vector<vector<T>>)

**Pattern Detection** (lines 308-311 in TypeTreeHelper.py):

```python
# Vector
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    subtype = node.m_Children[0].m_Children[1]  # Get element template
    
    if subtype is itself another vector (has Array as first child):
        # Nested array case
        value = [read_value_array(subtype, reader, config, reader.read_int()) 
                 for _ in range(size)]
```

**Example: vector<vector<uint32>>**:
```
Parent vector
└── Array (outer)
    └── Child[1]: Inner vector
        └── Array (inner)
            └── Child[1]: uint32 template
```

**Reading**:
1. Read outer count: N
2. For each of N elements:
   - Read inner count: M
   - For each of M elements:
     - Read uint32

---

## 7. Key Code Lines with File Paths and Line Numbers

### Core Algorithm Lines

| Component | File | Lines | Purpose |
|-----------|------|-------|---------|
| **Vector Pattern Detection** | `UnityPy/helpers/TypeTreeHelper.py` | 206-220 | Detect and handle "Array" as first child |
| **Element Template Selection** | `UnityPy/helpers/TypeTreeHelper.py` | 215 | `subtype = node.m_Children[0].m_Children[1]` |
| **Array Element Reading (Python)** | `UnityPy/helpers/TypeTreeHelper.py` | 271-361 | `read_value_array()` function |
| **Array Validation (C++)** | `UnityPyBoost/TypeTreeHelper.cpp` | 809-811 | Assert 2 children on Array node |
| **Array Template Reading (C++)** | `UnityPyBoost/TypeTreeHelper.cpp` | 800-826 | Full array reading logic |
| **Array Length Reading** | `UnityPyBoost/TypeTreeHelper.cpp` | 240-258 | `_read_length()` template function |
| **Element-by-Element Reading** | `UnityPyBoost/TypeTreeHelper.cpp` | 826-843 | Loop through array elements |
| **Fast Path for Primitives** | `UnityPyBoost/TypeTreeHelper.cpp` | 871-920 | `read_typetree_value_array()` |

### Python Array Reading Map

**File**: `UnityPy/helpers/TypeTreeHelper.py` lines 56-74

```python
FUNCTION_READ_MAP_ARRAY = {
    "char": EndianBinaryReader.read_u_byte_array,
    "SInt16": EndianBinaryReader.read_short_array,
    "UInt16": EndianBinaryReader.read_u_short_array,
    "int": EndianBinaryReader.read_int_array,
    "SInt32": EndianBinaryReader.read_int_array,
    "UInt32": EndianBinaryReader.read_u_int_array,
    "float": EndianBinaryReader.read_float_array,
    "double": EndianBinaryReader.read_double_array,
    "bool": EndianBinaryReader.read_boolean_array,
}
```

### C++ NodeDataType Enum

**File**: `UnityPyBoost/TypeTreeHelper.hpp` lines 12-28

```cpp
enum NodeDataType {
    u8, u16, u32, u64,           // 0-3
    s8, s16, s32, s64,           // 4-7
    f32, f64,                    // 8-9
    boolean,                     // 10
    str,                         // 11
    bytes,                       // 12
    pair,                        // 13
    Array,                       // 14 ← KEY: Identifies Array nodes
    PPtr,                        // 15
    ReferencedObject,            // 16
    ReferencedObjectData,        // 17
    ManagedReferencesRegistry,   // 18
    unk = 255
};
```

### Test Case: Array Parsing Test

**File**: `tests/test_typetree.py` lines 149-170

```python
def test_simple_nodes_array():
    def generate_list_node(item_node: TypeTreeNode):
        root = generate_dummy_node("root", "root")
        array = generate_dummy_node("Array", "Array")
        array.m_Children = [None, item_node]  # [size, template]
        root.m_Children = [array]
        return root

    for typs, py_typ, bounds in SIMPLE_NODE_SAMPLES:
        values = generate_sample_data(typs, py_typ, bounds)
        for typ in typs:
            node = generate_dummy_node(typ)
            array_node = generate_list_node(node)
            # ...array.m_Children[1] is the template that gets used
```

---

## 8. TypeTree Node Structure (Reference)

**File**: `UnityPy/helpers/TypeTreeNode.py`

TypeTreeNode has these critical attributes:

```python
class TypeTreeNode:
    m_Level: int            # Depth in tree hierarchy
    m_Type: str             # "Array", "Vector3", "SubMesh", "int", etc.
    m_Name: str             # Field name
    m_ByteSize: int         # Size of this node's data (0 for wrapper nodes)
    m_Version: int          # Format version
    m_MetaFlag: int         # Alignment and other flags
    m_Children: List[TypeTreeNode]  # Child nodes
    m_TypeFlags: int        # Type-specific flags
    m_Index: int            # Position in parent
    m_VariableCount: int    # For version 2+
```

---

## 9. Distinguishing Array Structures

### Pattern Recognition

UnityPy distinguishes arrays through:

| Pattern | Detection | Handling |
|---------|-----------|----------|
| **Vector** | `node.m_Children[0].m_Type == "Array"` | Read length, iterate with template |
| **Direct Array** | `node.m_Type == "Array"` | (Not typically at root, usually nested) |
| **Nested Array** | Subtype also has "Array" | Recursive iteration |
| **Primitive Array** | `node.m_Type` in `FUNCTION_READ_MAP_ARRAY` | Bulk read optimization |
| **Class Array** | Element template is a class | Read N class instances |
| **Pair Array** | `node.m_Type == "pair"` | Read N (key, value) tuples |

### Code for Distinction

**File**: `UnityPy/helpers/TypeTreeHelper.py` lines 271-293

```python
def read_value_array(node, reader, config, size):
    func = FUNCTION_READ_MAP_ARRAY.get(node.m_Type)
    if func:
        # Primitive array type
        return func(reader, size)
    elif node.m_Type == "string":
        return [reader.read_aligned_string() for _ in range(size)]
    elif node.m_Type == "TypelessData":
        return [reader.read_bytes() for _ in range(size)]
    elif node.m_Type == "pair":
        # Pair array
        return [(key_func(reader), value_func(reader)) for _ in range(size)]
    elif node.m_Children and node.m_Children[0].m_Type == "Array":
        # Nested array (vector<vector<T>>)
        return [[read_value(elem, reader, config) for elem in reader.read_int()] 
                for _ in range(size)]
    else:
        # Class array (multiple object instances)
        return [{child._clean_name: read_value(child, reader, config) 
                 for child in node.m_Children} for _ in range(size)]
```

---

## 10. Alignment Handling

### Alignment Flag Check

**File**: `UnityPy/helpers/TypeTreeHelper.py` lines 393-394

```python
def metaflag_is_aligned(meta_flag: int | None) -> bool:
    return ((meta_flag or 0) & kAlignBytes) != 0

kAlignBytes = 0x4000  # Bit 14 is the alignment flag
```

### When Alignment Applies

```python
# At array level
if metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
    align = True  # Align after reading the entire array

# At element template level
subtype = node.m_Children[0].m_Children[1]
if metaflag_is_aligned(subtype.m_MetaFlag):
    # Use optimized array reading (already handles per-element alignment)
    value = read_value_array(subtype, reader, config, size)
else:
    # Read unaligned
    value = [read_value(subtype, reader, config) for _ in range(size)]
```

---

## Summary: Key Takeaways

1. **Array Structure**: Every "Array" node has exactly 2 children; Child[1] is the **element template**

2. **Element Template**: The template at `Array.m_Children[1]` is **reused for every element** in the array

3. **Length Source**: Array length is **read directly from binary as int32**, not from the TypeTree structure

4. **Vector Detection**: Detected when `node.m_Children[0].m_Type == "Array"`

5. **Recursive Algorithm**:
   - Read int32 (array length)
   - Get template = `Array.m_Children[1]`
   - For count 0 to length: `read_value(template, reader, config)`

6. **Special Cases**:
   - Primitive arrays: bulk read optimization
   - Nested arrays: recursive vector detection
   - Class arrays: iterate and instantiate
   - Pair arrays: key-value iteration

7. **Critical Code Locations**:
   - Python pure: `UnityPy/helpers/TypeTreeHelper.py` lines 206-220, 271-361
   - C++ optimized: `UnityPyBoost/TypeTreeHelper.cpp` lines 800-843, 871-920

