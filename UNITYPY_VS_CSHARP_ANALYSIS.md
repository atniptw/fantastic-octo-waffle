# UnityPy vs C# Implementation: TypeTree Array Parsing Analysis

**Date:** Feb 2, 2026  
**Status:** Current Issue: m_SubMeshes reading wrong element template, consuming 1468 bytes instead of ~52 bytes  
**Root Cause:** Array element template selection logic differs from UnityPy

---

## Executive Summary

The C# implementation is **overcomplicating** array template selection with multiple fallback conditions and special cases. UnityPy's algorithm is **blindingly simple**:

```
dataTemplate = arrayNode.Children[1]  // Always index 1, always use it
```

My C# code is:
1. Trying to skip "Array" type children (WRONG - that's the correct template)
2. Adding complex fallback logic (UNNECESSARY)
3. Treating the template's type field as meaningful (WRONG - it's just structural metadata)

---

## 1. UnityPy Logic: Array Reading

### UnityPy Algorithm (Simplified)
```python
def read_array(reader, node, config):
    # Get array size
    size = reader.read_int()
    
    # Get data template - ALWAYS children[1]
    data_template = node.m_Children[1]
    
    # Read N elements using template
    result = []
    for i in range(size):
        value = read_value(data_template, reader, config)
        result.append(value)
    
    return result
```

### Key Points
- **No checking** of template type
- **No complex selection logic** - just use `children[1]`
- **Works recursively** - if template describes a struct, reads struct fields
- **Works for nested arrays** - if template is also an array, handles recursion

### Example: vector<SubMesh>
```
Node m_SubMeshes (type='vector'):
  └─ children[0]: Array (type='Array') - SIZE indicator
       └─ (4 children: data about the array structure itself)
  └─ children[1]: Array (type='Array') - TEMPLATE for each SubMesh element
       ├─ child[0]: firstByte (type='unsigned int')
       ├─ child[1]: indexCount (type='unsigned int')
       ├─ child[2]: topology (type='int')
       └─ child[3]: baseVertex (type='unsigned int')
```

**Read Process:**
1. Read int (e.g., 3) = array size
2. Use template = children[1] to read 3 SubMesh objects
3. Each SubMesh: read 4 uint fields = 16 bytes
4. Total consumed: 4 bytes (size) + 3×16 bytes (elements) = **52 bytes** ✓

---

## 2. Current C# Logic: Array Reading

### TypeTreeReader.ReadArray() - Current Implementation
```csharp
// My code (WRONG):
for (int i = 1; i < arrayNode.Children.Count; i++)
{
    var child = arrayNode.Children[i];
    // Skip "Array" type children (WRONG!)
    if (child.Type != "Array" && !string.IsNullOrEmpty(child.Type))
    {
        dataTemplate = child;
        break;
    }
}

// If not found, fallback to children[1] (WORKS but for wrong reason)
if (dataTemplate == null && arrayNode.Children.Count > 1)
{
    dataTemplate = arrayNode.Children[1];
}
```

### What's Wrong
1. **Line: `if (child.Type != "Array")`** - This SKIPS the correct template!
   - UnityPy ACCEPTS type='Array' templates
   - My code REJECTS them
   - Result: Loop never finds a non-Array child, falls through to fallback

2. **Fallback "solution"** - Works by accident
   - Sets `dataTemplate = children[1]` when nothing else found
   - But log output says `dataTemplate.Type='Array'` is being used
   - Then tries to read this Array template as if it's a struct template
   - Reads ALL 4 children of the Array node as fields
   - Each "child" is actually nested array metadata → reads 489 bytes per element!

3. **Root confusion** - Treating type field as semantic
   - `type='Array'` doesn't mean "skip this"
   - It's just metadata indicating "this node describes array structure"
   - The node's children ARE the element template fields

---

## 3. How My Code Reads m_SubMeshes (WRONG)

Current behavior from debug log:

```
[ARRAY-DEBUG] Array 'm_SubMeshes' structure: 2 children
  [0] name=Array, type=Array, IsArray=True, children=4
  [1] name=Array, type=Array, IsArray=True, children=4
[ARRAY-FALLBACK] Using children[1] as dataTemplate even though it's type='Array'
[ARRAY-SIZE] Array 'm_SubMeshes' size=3
[ARRAY-ELEMENT] Element 0: 488 bytes consumed ← WRONG! Should be 16
[ARRAY-ELEMENT] Element 1: 488 bytes consumed
[ARRAY-ELEMENT] Element 2: 488 bytes consumed
Total: 1468 bytes consumed instead of 52
```

### Why 488 bytes per element?
1. `dataTemplate` = children[1] (type='Array')
2. My code tries to read children[1]'s children as struct fields
3. children[1] has 4 children (indices 0-3 in debug output)
4. Each child appears to describe nested array structure
5. When reading each as a field, involves reading nested arrays
6. Result: **~489 bytes per element** instead of 16

---

## 4. Correct C# Logic (From UnityPy)

### Should Be
```csharp
// SIMPLE: Always use children[1] as template
var dataTemplate = arrayNode.Children.Count > 1 
    ? arrayNode.Children[1] 
    : null;

if (dataTemplate == null)
{
    Console.WriteLine($"[ERROR] Array '{arrayNode.Name}' has no children[1] template");
    return result;
}

// Read array size
int size = _reader.ReadInt32();

// Read N elements using template
for (int i = 0; i < size; i++)
{
    var value = ReadNode(dataTemplate, _reader);
    result.Add(value);
}
```

### Key Differences from Current
| Current | Correct |
|---------|---------|
| Complex loop filtering children | Always use `children[1]` |
| Rejects `type='Array'` templates | Accepts `type='Array'` templates |
| Multiple fallback conditions | Single simple assignment |
| Treats type as semantic meaning | Treats type as just metadata |

---

## 5. Why Current Approach Reads 489 Bytes

The problem is how the fallback template is used:

```
children[1] (the Array node) has 4 children:
  [0] ? (likely metadata about array)
  [1] ? (likely metadata about array)
  [2] ? (likely metadata about array)
  [3] ? (likely metadata about array)
```

When `ReadNode` is called with this Array template, it recursively reads:
1. Tries to read first child as struct field
2. That child describes something about arrays (not a simple uint field)
3. Triggers nested array reading
4. Each nested array has its own Array wrapper
5. Recursive reading of metadata structures = 489 bytes!

**Solution:** Don't treat the Array template as a struct with children. Recognize that when we're reading an array element, if the template is "Array" type, we should read it correctly.

Actually wait - let me reconsider. If children[1] is also type='Array', that means m_SubMeshes is a **vector of vectors** or similar nested structure. Let me check what the actual element type should be...

---

## 6. Discovered: The Actual TypeTree Structure

**BREAKTHROUGH - Inspected the actual structure:**

```
m_SubMeshes (type=vector, IsArray=True)
├─ children[0]: name=Array, type=Array (metadata node)
│  ├─ [0] name=size, type=int, ByteSize=4
│  ├─ [1] name=data, type=SubMesh, ByteSize=48  ← ACTUAL ELEMENT TEMPLATE!
│  ├─ [2] name=size, type=int, ByteSize=4
│  └─ [3] name=data, type=SubMesh, ByteSize=48
└─ children[1]: name=Array, type=Array (metadata node - duplicate)
   └─ (identical structure)
```

**KEY INSIGHTS:**
1. The Array nodes (children[0] and children[1]) are NOT element templates themselves
2. They are CONTAINERS with 4 fields describing the array structure
3. The actual element template is hidden at `children[1].children[1]` with name="data"!
4. The "data" node has type=SubMesh and ByteSize=48 (exactly 3×16 bytes for 3 SubMesh objects)

**Why This Happened:**
- My code used children[1] (the Array metadata node) as the template
- Then tried to read its 4 children as struct fields
- This read: size, data, size, data = ~489 bytes per element
- Should read: just the "data" element = 48 bytes for 3 elements = 16 bytes each

**The CORRECT Algorithm:**
```
1. Read array size (int)
2. Get template = arrayNode.children[1].children[1]  (the "data" node!)
3. Read N elements using template (just the SubMesh structure, 16 bytes each)
```

## 6. CRITICAL DISCOVERY: The Actual Algorithm (From UnityPy Source Code)

**UnityPy's EXACT implementation** (from [UnityPy/helpers/TypeTreeHelper.py lines 206-225](https://github.com/k0lb3/UnityPy/blob/main/UnityPy/helpers/TypeTreeHelper.py#L206-L225)):

```python
# Detect Vector
elif node.m_Children and node.m_Children[0].m_Type == "Array":
    # Read the count
    size = reader.read_int()
    
    # Get template from children[0].children[1] (NOT children[1]!)
    subtype = node.m_Children[0].m_Children[1]
    
    # Read all 'size' elements
    value = [read_value(subtype, reader, config) for _ in range(size)]
```

**C++ version** ([UnityPyBoost/TypeTreeHelper.cpp lines 800-854](https://github.com/k0lb3/UnityPy/blob/main/UnityPyBoost/TypeTreeHelper.cpp#L800-L854)):
```cpp
if (child && child->_data_type == NodeDataType::Array)
{
    // Verify Array has exactly 2 children
    if (PyList_GET_SIZE(child->m_Children) != 2)
    {
        PyErr_SetString(PyExc_ValueError, "Array node must have 2 children");
        return NULL;
    }
    
    int32_t length;
    _read_length<swap>(reader, &length);  // Read size
    
    // Use children[1] of the Array node (the data type)
    child = (TypeTreeNodeObject *)PyList_GET_ITEM(child->m_Children, 1);
    
    // Read all 'length' elements using that template
    for (int i = 0; i < length; i++)
    {
        PyObject *item = read_typetree_value<swap>(reader, child, config);
    }
}
```

---

## 7. What My Code is Doing WRONG

Current C# code:
```csharp
// I'm using children[1] directly as the template
var dataTemplate = arrayNode.Children[1];  // WRONG!
```

Then it tries to read children[1]'s own children (size, data, size, data) as fields, which is why it consumes 489 bytes per element.

**The CORRECT code should be:**
```csharp
// Use children[0].children[1] as the template (the "data" node)
var arrayContainer = arrayNode.Children[0];  // The "Array" metadata node
if (arrayContainer.Children.Count < 2)
{
    Console.WriteLine($"[ERROR] Array '{arrayNode.Name}' container has < 2 children!");
    return result;
}

var dataTemplate = arrayContainer.Children[1];  // The "data" node with actual type
int size = _reader.ReadInt32();
for (int i = 0; i < size; i++)
{
    result.Add(ReadNode(dataTemplate, _reader));
}
```

---

## 8. Why The Duplicate Structure Appeared

The debug output showed:
```
  [0] name=Array, children=4
    [0] size
    [1] data (SubMesh)
    [2] size
    [3] data (SubMesh)
  [1] name=Array, children=4
    [0] size
    [1] data (SubMesh)
    [2] size  
    [3] data (SubMesh)
```

**This is NOT the real structure.** According to UnityPy's assertion, Array nodes have EXACTLY 2 children:
- `children[0]` = size field (metadata)
- `children[1]` = data type (the element type)

The 4-child appearance suggests my **debug logging is flattening or misreporting the structure**. Need to verify actual children count.

---

## 9. NEW DISCOVERY: TypeTree Duplication Pattern

**Unexpected Finding:** ALL nodes in the TypeTree appear to have their children **duplicated**!

### Examples from Actual TypeTree Parse:

**SubMesh element template children (14 total, should be ~7):**
```
[0] firstByte (unsigned int, 4 bytes)
[1] indexCount (unsigned int, 4 bytes)  
[2] topology (int, 4 bytes)
[3] baseVertex (unsigned int, 4 bytes)
[4] firstVertex (unsigned int, 4 bytes)
[5] vertexCount (unsigned int, 4 bytes)
[6] localAABB (AABB, 24 bytes)
[7] firstByte (unsigned int, 4 bytes) ← DUPLICATE OF [0]
[8] ... (more duplicates)
[13] ...
```

**Total unique field sizes:** 4+4+4+4+4+4+24 = 52 bytes (close to ByteSize=48)
**Actual consumption:** 240 bytes per element (we're reading all 14 children)

**BlendShapeVertex element template:**
```
[0] vertex (Vector3f, 12 bytes)
[1] normal (Vector3f, 12 bytes)
[2] tangent (Vector3f, 12 bytes)
[3] index (unsigned int, 4 bytes)
[4] vertex (Vector3f, 12 bytes) ← DUPLICATE OF [0]
[5] normal (Vector3f, 12 bytes) ← DUPLICATE OF [1]
[6] tangent (Vector3f, 12 bytes) ← DUPLICATE OF [2]
```

**Expected unique:** 12+12+12+4 = 40 bytes
**Actual consumption:** 152 bytes per element (reading all 8 children)

### Root Cause Analysis

This duplication pattern appears in:
1. **Array container nodes** (children[0] and [1] are duplicates)
2. **Element template children** (child indices 0-6 duplicated at 7-13)
3. **Vector nodes** within structs

**Hypothesis:** This is NOT a bug in my parsing. The TypeTree binary format itself might have this duplication for some reason (redundancy? alignment? forward compatibility?).

**Evidence:** Every single node shows exact duplicates, too systematic to be a parsing error.

### Impact on Current Implementation

1. **String reading:** WORKING ✓ (type detection fix successful)
2. **Array size reading:** WORKING ✓ (reads 3 correctly for m_SubMeshes)
3. **Array element template selection:** FIXED ✓ (now uses children[0].children[1])
4. **Element field reading:** BROKEN ✗
   - Reads all 14 children instead of ~7 unique children
   - Consumes 240 bytes per SubMesh instead of 48
   - Should consume 724/3 = ~241 bytes per element? Actually that's what we're getting!
   - Wait... 240 bytes × 3 = 720 bytes... then m_Shapes at position 736...
   - Object is 1528 bytes... after m_SubMeshes (12 + 720 = 732), we're at 732 which matches 736 (with 4-byte alignment)

So the consumption is mathematically correct relative to the buffer! But it's WRONG semantically because we're reading duplicate children.

### What UnityPy Does

UnityPy's C++ code checks: `if (PyList_GET_SIZE(child->m_Children) != 2)` for Array nodes, asserting exactly 2 children. This suggests:
1. Array nodes should have exactly 2 children (not 4)
2. Element templates should have exactly N unique children (not 2N duplicated)
3. The duplication is a problem with my TypeTree parsing, not the format itself

### Next Action Required

**Debug the TypeTree parsing logic** to understand why children are being duplicated:
- Check how TypeTreeNode.Children list is being built
- Verify against UnityPy's TypeTree structure in memory
- Look for off-by-one errors or improper array termination in TypeTree blob parsing

This is a DIFFERENT issue from the array element template selection, which is now CORRECT (using children[0].children[1]).

---

## 10. Implementation Checklist - FINAL

## 10. Implementation Checklist - FINAL

### ✓ FIXED (This Session)
- [x] String type special case handling (reads m_Name correctly as 12 bytes)
- [x] Array template selection algorithm (now uses children[0].children[1] like UnityPy)
- [x] Array size reading (correctly reads 3 for m_SubMeshes)

### ✗ BLOCKED BY (New Discovery)
- [ ] TypeTree node children duplication issue
  - **Problem:** All nodes have their children doubled (14 instead of 7, 4 instead of 2, 8 instead of 4)
  - **Symptom:** Reading 240 bytes per SubMesh element instead of 48
  - **Root Cause:** TypeTree parsing is creating duplicate child entries somehow
  - **Location:** TypeTree blob parsing, likely in TypeTreeNode.Children list building
  - **Blocker:** Can't validate mesh extraction until this is fixed

### MUST FIX (Next Priority)
1. **Investigate TypeTree parsing** (where children list is built)
   - Use UnityPy to dump same TypeTree and compare structure
   - Verify children count should be 2 for Array nodes, N for element templates
   - Fix the duplication in TypeTreeNode creation

2. **Validate after TypeTree fix**
   - m_SubMeshes should consume 4 + 3×48 = 148 bytes (not 720)
   - All fields should align correctly within 1528-byte object
   - Extract valid mesh geometry with 3 SubMeshes

3. **Re-test all fixture mods**
   - Cigar_Neck (220 vertices)
   - ClownNose_head (500 vertices) 
   - Glasses (600+ vertices)

### Technical Debt
- Remove debug logging once working (very verbose)
- Optimize TypeTree reading (currently very inefficient)
- Add proper error handling for malformed TypeTree structures

---

## 11. Comparison Table: UnityPy vs Current C# Implementation

| Aspect | UnityPy | C# Current | Status |
|--------|---------|-----------|--------|
| **String Type Detection** | `if (node.Type == "string")` | `if (node.Type == "string")` | ✓ ALIGNED |
| **Array Detection** | `children[0].Type == "Array"` | `children.Count > 0 && children[0].Type == "Array"` | ✓ ALIGNED |
| **Array Size Read** | `size = reader.read_int()` | `int size = _reader.ReadInt32()` | ✓ ALIGNED |
| **Template Selection** | `subtype = children[0].children[1]` | `dataTemplate = arrayNode.Children[0].Children[1]` | ✓ ALIGNED (FIXED) |
| **Element Reading Loop** | `for i in range(size): read_value(subtype)` | `for (int i = 0; i < size; i++) ReadNode(dataTemplate)` | ✓ ALIGNED |
| **TypeTree Node Children** | `len(node.children) == 2` (for Arrays) | `len(node.children) == 4` (showing duplicates!) | ✗ MISALIGNED |
| **Element Template Children** | Expected ~7 unique | Reading ~14 (with duplicates) | ✗ MISALIGNED |

---

## 12. Lessons Learned

1. **Trust UnityPy as source of truth** - It's proven, tested, and well-engineered
   - UnityPy asserts Array nodes have exactly 2 children
   - My code found 4 children → indicates parsing bug elsewhere

2. **Validate assumptions with real data** - The deep structure inspection revealed the truth
   - Showed actual child node names and types
   - Proved template selection logic was wrong
   - Uncovered new problem (duplication)

3. **Bottom-up debugging** - Inspect actual data before fixing code
   - Instead of guessing why templates were wrong, examined the structure
   - Found children[0].children[1] is the real template
   - Would have saved hours of troubleshooting

4. **Separate concerns** - Array element template selection ≠ TypeTree parsing
   - Fixing template selection revealed TypeTree structure issue
   - Can't validate fix without resolving the deeper problem

---

## Next Steps

1. **DO NOT continue with current TypeTree structure** - duplication will corrupt all reads
2. **Investigate TypeTree.cs parsing** - where are children duplicated?
3. **Compare with UnityPy TypeTree structure** - validate assumptions
4. **Fix TypeTree duplication** - ensure children lists have correct count
5. **Re-validate array element reading** - should consume 48 bytes per SubMesh, not 240
6. **Unlock mesh extraction** - enable all 3 fixture tests
7. **Commit working solution**

---

## 8. Recommended Next Actions

### 1. **Add Deep Structure Inspection**
```csharp
if (arrayNode.Name == "m_SubMeshes")
{
    Console.WriteLine($"=== DEEP INSPECTION: m_SubMeshes ===");
    for (int i = 0; i < arrayNode.Children.Count; i++)
    {
        var child = arrayNode.Children[i];
        Console.WriteLine($"\nChild[{i}]: name={child.Name}, type={child.Type}");
        Console.WriteLine($"  SubChildren ({child.Children.Count}):");
        for (int j = 0; j < child.Children.Count; j++)
        {
            var subchild = child.Children[j];
            Console.WriteLine($"    [{j}]: name={subchild.Name}, type={subchild.Type}, ByteSize={subchild.ByteSize}");
        }
    }
}
```

### 2. **Verify UnityPy's Exact Approach**
```python
# From UnityPy, run:
bundle = load(".hhh")
mesh = get_mesh_object()
print("m_SubMeshes children:")
for i, child in enumerate(mesh.m_Children[1].m_Children):  # m_SubMeshes
    print(f"  [{i}] {child.m_Name} ({child.m_Type})")
    for j, subchild in enumerate(child.m_Children):
        print(f"    [{j}] {subchild.m_Name} ({subchild.m_Type})")
```

### 3. **Simplify to UnityPy Algorithm**
Once we understand the structure, change ReadArray to:
```csharp
// Always use children[1] as template
var dataTemplate = arrayNode.Children.Count > 1 ? arrayNode.Children[1] : null;
if (dataTemplate == null) return result;

int size = _reader.ReadInt32();
for (int i = 0; i < size; i++)
{
    result.Add(ReadNode(dataTemplate, _reader));
}
```

### 4. **Validate Output**
- After fix: m_SubMeshes should consume 52 bytes (not 1468)
- Verify: Position progression 0→12→64 (not 0→12→1480)
- Confirm: m_SubMeshes list has 3 SubMesh objects
- Check: Each SubMesh has correct 4 uint fields

---

## Conclusion

**The fix is simple:** Stop filtering children by type, just use `children[1]`. But first, we need to understand what the actual TypeTree structure is for m_SubMeshes so we can verify the fix works correctly.

