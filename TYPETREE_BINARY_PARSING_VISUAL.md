# TypeTree Binary Parsing - Visual Guides & Quick Reference

## Visual 1: Blob Format Binary Layout

```
TypeTree Blob Structure
=======================

[HEADER]
├─ nodeCount: int32 (4 bytes)              "How many nodes in this blob?"
└─ stringBufferSize: int32 (4 bytes)       "How many bytes for strings?"

[NODES SECTION]
├─ Node[0]: 24 or 32 bytes (binary struct)
│   ├─ m_Version: int16 (2 bytes)
│   ├─ m_Level: uint8 (1 byte)
│   ├─ m_TypeFlags: uint8 (1 byte)
│   ├─ m_TypeStrOffset: uint32 (4 bytes)    → STRING POOL LOOKUP
│   ├─ m_NameStrOffset: uint32 (4 bytes)    → STRING POOL LOOKUP
│   ├─ m_ByteSize: int32 (4 bytes)
│   ├─ m_Index: int32 (4 bytes)
│   └─ m_MetaFlag: int32 (4 bytes)
│       [if v >= 19: m_RefTypeHash: uint64 (8 bytes)]
│
├─ Node[1]: 24 or 32 bytes (binary struct)
│   └─ ...
│
└─ Node[N-1]: 24 or 32 bytes (binary struct)
    └─ ...

[STRING POOL]
├─ "Transform" (null-terminated)
├─ "Vector3f" (null-terminated)
├─ "position" (null-terminated)
└─ ... (stringBufferSize total bytes)
```

**Total blob size**: 8 + (nodeCount × structSize) + stringBufferSize bytes

---

## Visual 2: Tree Building Process (Blob Format)

```
Flat list of nodes (read sequentially):
================================================

Index | m_Level | m_Type      | m_Name       | m_TypeFlags | m_ByteSize
------|---------|-------------|--------------|-------------|----------
  0   |    0    | "Transform" | "Transform"  | 0x00        | 60
  1   |    1    | "Vector3f"  | "m_Position" | 0x00        | 12
  2   |    2    | "float"     | "x"          | 0x00        | 4
  3   |    2    | "float"     | "y"          | 0x00        | 4
  4   |    2    | "float"     | "z"          | 0x00        | 4
  5   |    1    | "Array"     | "m_Children" | 0x01        | -1
  6   |    2    | "int"       | "size"       | 0x00        | 4
  7   |    2    | "Transform" | "item"       | 0x00        | 60


Tree building algorithm:
======================

Input:  flat list of nodes
Output: hierarchical tree

stack = [fake_root]
parent = fake_root
prev = fake_root

FOR EACH node IN nodes:
    IF node.level > prev.level:
        # Node is child of previous node
        stack.push(parent)
        parent = prev
    
    ELSE IF node.level < prev.level:
        # Node is back at same or higher level
        # Pop stack until we find correct parent level
        WHILE node.level <= parent.level:
            parent = stack.pop()
    
    # Add node to current parent
    parent.children.append(node)
    prev = node


Result tree:
===========

Transform (level=0)
├─ Vector3f "m_Position" (level=1)
│  ├─ float "x" (level=2)
│  ├─ float "y" (level=2)
│  └─ float "z" (level=2)
└─ Array "m_Children" (level=1)
   ├─ int "size" (level=2)
   └─ Transform "item" (level=2)
```

---

## Visual 3: Array Node Structure

```
An Array node as represented in the tree:
==========================================

Node: "Array"
│  m_Type = "Array"
│  m_TypeFlags = 0x01  ← Bit indicates array
│  m_ByteSize = -1     ← Variable length
│
└─ Children (exactly 2, always):
   │
   ├─ [0] Size Descriptor
   │  ├─ m_Type = "int"
   │  ├─ m_Name = "size"
   │  └─ m_ByteSize = 4
   │
   └─ [1] ELEMENT TEMPLATE ← This is the pattern repeated
      ├─ m_Type = "SubMesh"
      ├─ m_Name = "item"
      ├─ m_ByteSize = 16
      │
      └─ Children (definition of SubMesh):
         ├─ int "vertexStart" (1 × 4 bytes)
         ├─ int "vertexCount" (1 × 4 bytes)
         ├─ int "triangleStart" (1 × 4 bytes)
         └─ int "triangleCount" (1 × 4 bytes)


When reading binary data:
=========================

Binary stream:
┌─────────────────────────────────────────┐
│ 0x03000000  ← 3 elements (int32, LE)   │
│ 10,20,30,40  ← elem[0]: v=(10,20), t=(30,40)
│ 50,60,70,80  ← elem[1]: v=(50,60), t=(70,80)
│ 90,100,110,120 ← elem[2]: v=(90,100), t=(110,120)
└─────────────────────────────────────────┘

Parsing algorithm:
1. Read array size: 3
2. For i = 0 to 2:
   - Read SubMesh using element template
   - Element template has 4 int children
   - Read 4 × 4 = 16 bytes per element
   - Total: 3 × 16 = 48 bytes read
```

---

## Visual 4: Comparison - Blob vs Legacy Format

```
BLOB FORMAT (v >= 10)           |  LEGACY FORMAT (v < 10)
Used by: Modern Unity (2017+)   |  Used by: Old Unity (pre-2017)
================================|================================
Fixed struct size               |  Variable size per node
24 or 32 bytes                  |  20+ bytes (depends on strings)
                                |
Per-node binary format:         |  Per-node text format:
┌─────────────────────┐         |  ┌──────────────────────────┐
│ version: int16(2B)  │         |  │ type: cstring (null-term)│
│ level: uint8(1B)    │         |  │ name: cstring (null-term)│
│ typeflags: uint8(1B)│         |  │ bytesize: int32(4B)      │
│ type_offset: int32  │ ──┐     |  │ var_count: int32(4B)*   │
│ name_offset: int32  │ ──┼──┬──→ string pool lookup
│ bytesize: int32(4B) │   │  │  │ index: int32(4B)*        │
│ index: int32(4B)    │   │  │  │ typeflags: int32(4B)     │
│ metaflag: int32(4B) │   │  │  │ version: int32(4B)       │
└─────────────────────┘   │  │  │ metaflag: int32(4B)*     │
                          │  │  │ children_count: int32(4B)│
Tree building:          │  │  └──────────────────────────┘
- m_Level field         │  │
- Compare with prev     │  │  Tree building:
- Stack pops/pushes     │  │  - children_count field
                        │  │  - Explicit stack entries
String lookup:          │  │
- Offset in blob        │  │  No string pool:
- Look up in pool       │  │  - Strings inline
                        │  │  - Variable lengths
```

---

## Visual 5: How Array Assertion Works

```
UnityPy Code:
=============

if PyList_GET_SIZE(child->m_Children) != 2:
    raise ValueError("Array must have exactly 2 children")


Why this works:
===============

Array nodes come from Unity serialization:
┌─────────────────────────────────────────┐
│ Unity Engine (when saving game build)   │
│ ┌─────────────────────────────────────┐ │
│ │ Serialize field: List<SubMesh>      │ │
│ │ Creates TypeTree:                   │ │
│ │ ┌─ Array (wrapper)                  │ │
│ │ ├─ int (size)       ← always here   │ │
│ │ └─ SubMesh (item)   ← always here   │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
         ↓ (serialized to binary)
┌─────────────────────────────────────────┐
│ .resS file / SerializedFile blob        │ ← UnityPy reads this
│ ┌─────────────────────────────────────┐ │
│ │ TypeTree nodes 0-10:                │ │
│ │ 0: Array (level=1)                  │ │
│ │ 1: int (level=2)                    │ │
│ │ 2: SubMesh (level=2)                │ │
│ │ 3: int (level=3) [part of SubMesh]  │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
         ↓ (built into tree)
┌─────────────────────────────────────────┐
│ TypeTreeNode tree:                      │
│ Array (m_Level=1)                       │
│ ├─ m_Children[0]: int (m_Level=2)       │
│ └─ m_Children[1]: SubMesh (m_Level=2)   │ ← Exactly 2!
│    └─ m_Children[0]: int (m_Level=3)    │
└─────────────────────────────────────────┘


ASSERTION IS: "Verify what Unity created"
NOT: "Validate possibly-malformed input"

If a real file has != 2 children for an Array:
→ File is corrupted
→ Not a parsing bug
→ Assert rightfully fails
```

---

## Visual 6: Stack State During Tree Building

```
Building tree from flat node list with levels:
================================================

Nodes:
0(L0) → 1(L1) → 2(L2) → 3(L2) → 4(L1) → 5(L2)

Step-by-step:

INIT:
stack = [fake_root(-1)]
parent = fake_root(-1)
prev = none

─────────────────────────────────────────────

PROCESS node 0 (L0):
 prev.level(none) < node.level(0)?  → No special handling
 Action: fake_root.children.append(node0)
 State: parent = fake_root, prev = node0

─────────────────────────────────────────────

PROCESS node 1 (L1):
 node.level(1) > prev.level(0)?  → YES (child of prev!)
 Action: stack.push(fake_root); parent = node0
 Result: parent.children.append(node1)
 State: parent = node0, prev = node1
 Stack: [fake_root, node0]

─────────────────────────────────────────────

PROCESS node 2 (L2):
 node.level(2) > prev.level(1)?  → YES (child of prev!)
 Action: stack.push(node0); parent = node1
 Result: parent.children.append(node2)
 State: parent = node1, prev = node2
 Stack: [fake_root, node0, node1]

─────────────────────────────────────────────

PROCESS node 3 (L2):
 node.level(2) == prev.level(2)?  → EQUAL (sibling!)
 Action: (none, parent stays same)
 Result: parent.children.append(node3)
 State: parent = node1, prev = node3
 Stack: [fake_root, node0, node1] (unchanged)

─────────────────────────────────────────────

PROCESS node 4 (L1):
 node.level(1) < prev.level(2)?  → YES (backtrack!)
 Action: WHILE 1 <= parent.level(1): parent = stack.pop()
         → parent goes from node1 to node0
         → now parent.level(0) < node.level(1)
         → exit while
 Result: parent.children.append(node4)
 State: parent = node0, prev = node4
 Stack: [fake_root]

─────────────────────────────────────────────

PROCESS node 5 (L2):
 node.level(2) > prev.level(1)?  → YES (child of prev!)
 Action: stack.push(node0); parent = node4
 Result: parent.children.append(node5)
 State: parent = node4, prev = node5
 Stack: [fake_root, node0, node4]

─────────────────────────────────────────────

FINAL TREE:
fake_root
├─ node0(L0)
│  ├─ node1(L1)
│  │  ├─ node2(L2)
│  │  └─ node3(L2)
│  └─ node4(L1)
│     └─ node5(L2)
```

---

## Quick Reference: Reading Array Size in Binary

```
C# Code to read array:
======================

// 1. Check if this is an array node
if (node.m_Type == "Array" && node.m_Children.Count == 2)
{
    // 2. Read the array count from binary stream
    int arraySize = reader.ReadInt32();  // 4 bytes, int32
    
    // 3. Get the element template
    TypeTreeNode elementTemplate = node.m_Children[1];
    
    // 4. Read each element
    var elements = new List<object>();
    for (int i = 0; i < arraySize; i++)
    {
        object element = ReadValue(elementTemplate, reader);
        elements.Add(element);
    }
}


Python (UnityPy):
=================

if node.m_Type == "Array" and len(node.m_Children) == 2:
    array_size = reader.read_int()  # 4 bytes
    element_template = node.m_Children[1]
    
    elements = []
    for i in range(array_size):
        element = read_value(element_template, reader)
        elements.append(element)
```

---

## Bytes Consumed Summary

| Operation | Bytes | Example |
|-----------|-------|---------|
| Blob header (nodeCount + stringBufSize) | 8 | Always at blob start |
| One node struct (v < 19) | 24 | (2+1+1+4+4+4+4+4) |
| One node struct (v >= 19) | 32 | (24 + 8 for hash) |
| Array size (int32) | 4 | `3` → read 3 elements |
| Float | 4 | `3.14159265` |
| Int | 4 | `42` |
| String (cstring) | N+1 | `"hello\0"` = 6 bytes |

