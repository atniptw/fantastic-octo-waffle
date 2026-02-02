# TypeTree Parsing Research - Complete Documentation Index

**Research Completed**: February 2, 2026  
**Status**: ✅ Complete with exact source code references and visual diagrams  
**Total Documents**: 4 comprehensive guides

---

## 📋 Document Overview

### 1. **[TYPETREE_PARSING_ANSWERS.md](TYPETREE_PARSING_ANSWERS.md)** — START HERE
**Purpose**: Direct answers to all 5 research questions with code examples

**Contains**:
- ✅ Question 1: How UnityPy parses TypeTree binary → full algorithm
- ✅ Question 2: Exact binary format of a TypeTreeNode → struct layouts
- ✅ Question 3: How children are read → bytes per node
- ✅ Question 4: How many children per node → level-based vs explicit count
- ✅ Question 5: Array "2 children" assertion → validation explanation
- 📊 Complete example: vector<SubMesh> walkthrough
- 🗂️ Summary table with file locations

**Read this if you need**: Quick answers with code

---

### 2. **[TYPETREE_BINARY_PARSING_ALGORITHM.md](TYPETREE_BINARY_PARSING_ALGORITHM.md)** — DETAILED REFERENCE
**Purpose**: Complete algorithm with line-by-line breakdown

**Contains**:
- 📐 Part 1: TypeTree Binary Format Structure (blob v/s legacy)
- 🏗️ Part 2: Stack-Based Tree Building Algorithm
- 📝 Part 3: Legacy Format Tree Building (text-based)
- 🎁 Part 4: Array Node Structure
- ✔️ Part 5: The Array Assertion (postcondition check)
- 📏 Part 6: Exact Binary Sizes (24 vs 32 bytes)
- 💻 Part 7: Complete C# Port Reference
- 📊 Part 8: Summary Table
- 🎯 Part 9: Key Takeaways for Implementation

**Read this if you need**: Deep technical understanding

---

### 3. **[TYPETREE_BINARY_PARSING_VISUAL.md](TYPETREE_BINARY_PARSING_VISUAL.md)** — VISUAL GUIDES
**Purpose**: ASCII diagrams and visual explanations

**Contains**:
- 📐 Visual 1: Blob Format Binary Layout
- 🌳 Visual 2: Tree Building Process (step-by-step)
- 📦 Visual 3: Array Node Structure
- ⚖️ Visual 4: Blob vs Legacy Comparison
- 🔍 Visual 5: How Array Assertion Works
- 📊 Visual 6: Stack State During Tree Building
- 🔢 Quick Reference: Reading Array Size

**Read this if you need**: Visual understanding

---

### 4. **[TYPETREE_PARSING_SOURCE_REFERENCES.md](TYPETREE_PARSING_SOURCE_REFERENCES.md)** — SOURCE CODE
**Purpose**: Exact UnityPy source code with line numbers

**Contains**:
- File 1: TypeTree Binary Struct Definition (TypeTreeNode.py:204-215)
- File 2: Blob Format Parsing Algorithm (TypeTreeNode.py:112-154)
- File 3: Legacy Format Parsing Algorithm (TypeTreeNode.py:79-109)
- File 4: Array Assertion (TypeTreeHelper.cpp:808-810)
- File 5: Python Array Reading (TypeTreeHelper.py:206-220)
- File 6: SerializedFile Type Parsing
- File 7: TypeTreeNode Class Definition
- File 8: String Reading from Blob Pool
- File 9: Common Strings (Predefined Pool)
- File 10: Dumping (Writing) TypeTree Back
- 📍 Summary Table: Location of Each Answer
- 🔗 Direct GitHub Links

**Read this if you need**: Exact source code references

---

## 🎯 Quick Navigation by Topic

### For Implementation (C#)

1. Start: [TYPETREE_PARSING_ANSWERS.md](TYPETREE_PARSING_ANSWERS.md#question-2-what-is-the-exact-binary-format-of-a-typetreenode) — Binary format
2. Reference: [TYPETREE_BINARY_PARSING_ALGORITHM.md](TYPETREE_BINARY_PARSING_ALGORITHM.md#part-7-complete-c-port-reference) — C# port reference
3. Diagram: [TYPETREE_BINARY_PARSING_VISUAL.md](TYPETREE_BINARY_PARSING_VISUAL.md#visual-1-blob-format-binary-layout) — Visual layout

### For Debugging (Understanding Behavior)

1. Start: [TYPETREE_PARSING_ANSWERS.md](TYPETREE_PARSING_ANSWERS.md#question-1-how-does-unitypy-parse-typetree-binary-data-to-build-the-node-tree) — Parsing algorithm
2. Diagram: [TYPETREE_BINARY_PARSING_VISUAL.md](TYPETREE_BINARY_PARSING_VISUAL.md#visual-2-tree-building-process-blob-format) — Tree building visualization
3. Reference: [TYPETREE_PARSING_SOURCE_REFERENCES.md](TYPETREE_PARSING_SOURCE_REFERENCES.md#file-2-blob-format-parsing-algorithm) — Source code

### For Arrays Specifically

1. Start: [TYPETREE_PARSING_ANSWERS.md](TYPETREE_PARSING_ANSWERS.md#question-5-if-unitypy-asserts-array-must-have-exactly-2-children-how-does-it-read-the-children-to-guarantee-getting-2) — Array assertion explanation
2. Diagram: [TYPETREE_BINARY_PARSING_VISUAL.md](TYPETREE_BINARY_PARSING_VISUAL.md#visual-3-array-node-structure) — Array structure
3. Reference: [TYPETREE_PARSING_SOURCE_REFERENCES.md](TYPETREE_PARSING_SOURCE_REFERENCES.md#file-5-python-array-reading-pure-python-no-c) — Array reading code

### For Exact Line Numbers

→ Use [TYPETREE_PARSING_SOURCE_REFERENCES.md](TYPETREE_PARSING_SOURCE_REFERENCES.md) for all UnityPy source references with GitHub links

---

## 🔑 Key Findings Summary

### The Algorithm

**Blob Format (Modern, v >= 12)**:
```
1. Read: nodeCount (int32), stringBufferSize (int32)
2. Read: nodeCount × 24-32 bytes (binary structs)
3. Read: stringBufferSize bytes (string pool)
4. Parse: Iterate structs, build tree using m_Level comparison
5. Result: Hierarchical tree with children lists populated
```

**Legacy Format (Old, v < 10)**:
```
1. Read: type (null-terminated string)
2. Read: name (null-terminated string)
3. Read: fields (various ints)
4. Read: children_count (int32) ← KEY DIFFERENCE
5. Pre-allocate children array
6. Recursively parse children
```

### Children Count

- **Blob**: Derived from m_Level values (implicit)
- **Legacy**: Explicit int32 field in binary (4 bytes per node)

### Array Validation

- **Blob**: Array nodes always have exactly 2 children (from parsing)
- **Legacy**: Same (children_count is always 2 for Array nodes)
- **Assertion**: Validates parsed structure (postcondition, not input validation)
- **Use**: Child[1] is the element template used for all array elements

### Binary Sizes

- **Blob node**: 24 bytes (v < 19) or 32 bytes (v >= 19)
- **Array size**: 4 bytes (int32 from data stream when reading)
- **Element**: Depends on element template (multiplied by count)

---

## 📚 Referenced Source Files

| File | Location | Purpose |
|------|----------|---------|
| TypeTreeNode.py | UnityPy/helpers/ | Parsing algorithms + tree structure |
| TypeTreeHelper.py | UnityPy/helpers/ | Reading data using TypeTree nodes |
| TypeTreeHelper.cpp | UnityPyBoost/ | C++ optimized array reading + assertion |
| SerializedFile.py | UnityPy/files/ | Entry point for parsing |

---

## 🚀 Implementation Checklist

Use these documents to implement TypeTree parsing in C#:

- [ ] Read binary format specification → [TYPETREE_PARSING_ANSWERS.md Q2](TYPETREE_PARSING_ANSWERS.md#question-2-what-is-the-exact-binary-format-of-a-typetreenode)
- [ ] Implement blob parser → [TYPETREE_BINARY_PARSING_ALGORITHM.md Part 7](TYPETREE_BINARY_PARSING_ALGORITHM.md#part-7-complete-c-port-reference)
- [ ] Implement tree building → [TYPETREE_BINARY_PARSING_VISUAL.md Visual 2](TYPETREE_BINARY_PARSING_VISUAL.md#visual-2-tree-building-process-blob-format)
- [ ] Implement array handling → [TYPETREE_PARSING_ANSWERS.md Q5](TYPETREE_PARSING_ANSWERS.md#question-5-if-unitypy-asserts-array-must-have-exactly-2-children-how-does-it-read-the-children-to-guarantee-getting-2)
- [ ] Test against UnityPy output → [TYPETREE_PARSING_SOURCE_REFERENCES.md](TYPETREE_PARSING_SOURCE_REFERENCES.md)
- [ ] Validate struct packing → [TYPETREE_PARSING_ANSWERS.md Q2](TYPETREE_PARSING_ANSWERS.md#question-2-what-is-the-exact-binary-format-of-a-typetreenode)

---

## 🔗 External References

- **UnityPy Repository**: https://github.com/K0lb3/UnityPy
- **TypeTreeNode.py**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeNode.py
- **TypeTreeHelper.py**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/TypeTreeHelper.py
- **SerializedFile.py**: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/files/SerializedFile.py

---

## ❓ FAQ

**Q: Which format should I implement first?**  
A: Blob format (v >= 12). Most modern games use it. Legacy format is older/rarer.

**Q: Why does blob format not store child count in binary?**  
A: Children are determined by level hierarchy. More efficient encoding.

**Q: What does the Array assertion really do?**  
A: Validates that the TypeTree was built correctly (postcondition check, not input validation).

**Q: How do I know when to use Child[1] vs Child[0] for an array?**  
A: Child[0] is metadata (size descriptor), Child[1] is the actual element template.

**Q: Do I need to implement legacy format?**  
A: Only if supporting very old Unity versions (pre-2017). Most modern projects use blob.

---

## 📝 Document Metadata

| Document | Lines | Topics | Diagrams |
|----------|-------|--------|----------|
| TYPETREE_PARSING_ANSWERS.md | ~450 | 5 research questions | Tables + code |
| TYPETREE_BINARY_PARSING_ALGORITHM.md | ~600 | Complete algorithm | Struct layouts |
| TYPETREE_BINARY_PARSING_VISUAL.md | ~400 | Visual explanations | 6 ASCII diagrams |
| TYPETREE_PARSING_SOURCE_REFERENCES.md | ~400 | Source code + refs | Summary tables |
| **Total** | ~1850 | Comprehensive | Complete |

---

## ✨ How These Documents Were Created

1. **Research Phase**:
   - Analyzed UnityPy source code (Python + C++ boost)
   - Extracted exact algorithms from TypeTreeNode.py and TypeTreeHelper.py/cpp
   - Traced parsing flow: header → nodes → tree → validation

2. **Documentation Phase**:
   - Created 4 complementary documents covering different aspects
   - Added exact line numbers and GitHub links
   - Included visual ASCII diagrams for clarity
   - Provided both theoretical and practical guidance

3. **Validation Phase**:
   - Cross-referenced all code snippets
   - Verified line numbers against GitHub master branch
   - Ensured consistency across all documents
   - Tested understanding against actual implementation patterns

---

## 🎓 Learning Path

**Beginner** (Want quick answers):
1. Read: TYPETREE_PARSING_ANSWERS.md
2. Skim: TYPETREE_BINARY_PARSING_VISUAL.md

**Intermediate** (Want to understand):
1. Read: TYPETREE_BINARY_PARSING_ALGORITHM.md
2. Reference: TYPETREE_BINARY_PARSING_VISUAL.md
3. Check: TYPETREE_PARSING_SOURCE_REFERENCES.md for exact code

**Advanced** (Want to implement):
1. Study: TYPETREE_PARSING_ANSWERS.md (all questions)
2. Reference: TYPETREE_BINARY_PARSING_ALGORITHM.md (Part 7 - C# port)
3. Debug: TYPETREE_BINARY_PARSING_VISUAL.md (Visual 6 - stack state)
4. Validate: TYPETREE_PARSING_SOURCE_REFERENCES.md (exact source)

---

**Research Complete!** All questions answered with exact source references. 🎉
