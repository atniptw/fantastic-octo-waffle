# UnityFS Bundle Specification (Normative)

This document defines the normative on-disk format of UnityFS bundle files used by this project. It is format-facing (reader- and writer-agnostic). Any implementation (reader or writer) SHALL produce/consume files that conform to this binary layout. Behavior MUST match UnityPy (`UnityPy/files/BundleFile.py`) and observed Unity-produced bundles; no reinterpretations are permitted.

## 1. Scope
1.1 This specification covers UnityFS bundle containers (`.hhh`) including header parsing, BlocksInfo parsing, compression handling, node table decoding, offset calculations, hash verification, and error conditions.
1.2 This specification excludes SerializedFile object parsing and mesh/asset decoding (covered elsewhere).

## 2. Normative References
- UnityPy source: `UnityPy/files/BundleFile.py` (and its called helpers). The Python implementation is the authoritative behavioral reference.

## 3. Terms and Conventions
- **MUST/SHALL** indicate absolute requirements.
- **SHOULD** indicates a recommended action that MAY be waived with justification.
- UnityPy reads bundle headers and BlocksInfo tables using a big-endian reader by default; this spec follows that default unless explicitly stated otherwise (e.g., LZMA property fields called out as little-endian).
- Offsets are relative to `data_offset` unless stated otherwise.

## 4. Container Layout
The UnityFS file SHALL comprise, in order:
1. Header (magic, version, sizes, flags, Unity version strings)
2. BlocksInfo (compressed or uncompressed) describing storage blocks and node entries
3. Data blocks (payload area), optionally compressed per block
4. Node payloads located within the data blocks, addressed relative to `data_offset`

## 5. Header Fields
Binary order in file (all fields big-endian, matching UnityPy’s default reader, unless noted):
1. `Signature`: null-terminated ASCII string. Valid value for this spec: `UnityFS`.
2. `Version`: uint32 (commonly 6 or 7).
3. `UnityVersion`: null-terminated ASCII string.
4. `UnityRevision`: null-terminated ASCII string.
5. `Size`: int64 — total bundle file size in bytes as authored.
6. `CompressedBlocksInfoSize`: uint32 — byte length of the compressed BlocksInfo blob.
7. `UncompressedBlocksInfoSize`: uint32 — byte length of the BlocksInfo after decompression.
8. `Flags`: uint32 — format/control bits (see Section 6).

`header_end` is defined as the byte position immediately after `Flags` (before any alignment).

## 6. Flags and Compression Mapping
6.1 Bundle-level compression for BlocksInfo: `Flags & 0x3F` SHALL encode:
- `0` → None
- `1` → LZMA
- `2` → LZ4
- `3` → LZ4HC
- `4` → LZHAM (observed in AssetStudio). Writers MAY emit only {0,1,2,3}; readers SHALL fail fast on unimplemented values.
6.2 BlocksInfo placement: `Flags & 0x80` (bit 7): set = BlocksInfo located at end of file (“streamed”); clear = BlocksInfo immediately after header (“embedded”).
6.3 BlocksInfo padding: `Flags & 0x200` (bit 9, `BlockInfoNeedPaddingAtStart`): when set and `Version ≥ 7`, a pre-BlocksInfo alignment requirement exists (Section 8).
6.4 Reserved: All other bits are reserved; writers SHALL set them to zero; readers SHALL treat non-zero reserved bits as an error unless a compatibility mode explicitly allows them.

### 6.5 StorageBlock flags (per-block entries in BlocksInfo)
- `flags & 0x3F` → compression type (same mapping as 6.1).
- `flags & 0x40` → Streamed (per AssetStudio definition). Other bits reserved; SHALL be zero.

## 7. BlocksInfo Payload Layout
BlocksInfo is a binary blob of length `CompressedBlocksInfoSize` (compressed) and `UncompressedBlocksInfoSize` (after decompression). Its internal layout SHALL be exactly:

1. `uncompressedDataHash`: 16 bytes (`Hash128`). UnityPy reads and preserves these bytes but does not verify them. Implementations following this spec SHALL read/preserve the 16-byte hash and MAY perform optional verification if an external reference hash is provided; no hash check is required by default.
2. `blockCount`: int32.
3. `StorageBlock[blockCount]` table, each entry:
  - `uncompressedSize`: uint32
  - `compressedSize`: uint32
  - `flags`: uint16 (see 6.5)
4. Alignment padding: apply 4-byte alignment to the next field (pad with zero bytes) after the block table.
5. `nodeCount`: int32.
6. `Node[nodeCount]` table, each entry:
  - `offset`: int64 (relative to `data_offset` per Section 9)
  - `size`: int64
  - `flags`: int32 (reserved; MUST be preserved when writing; readers SHALL retain value)
  - `path`: null-terminated UTF-8 string (byte sequence terminated by 0x00)

No additional padding follows a node entry unless required to reach the start of the next entry; strings are immediately followed by the next field. There is no trailing alignment after the final node unless explicitly present in source files (treat any such padding as reserved/ignored).

## 8. Alignment Requirements
8.1 Pre-BlocksInfo alignment (between header and BlocksInfo location):
- If `Version ≥ 7` and (`Flags & 0x200`) is set: align the file position to the next 16-byte boundary before the first byte of BlocksInfo.
- Otherwise: align to the next 4-byte boundary if UnityPy does so for the given version/signature (UnityFS v6/v7: 4-byte align is typical). Writers SHALL emit the required padding bytes (0x00). Readers SHALL accept only correctly aligned layouts.

8.2 Intra-BlocksInfo alignment: after the StorageBlock table (after `blockCount` entries), align to the next 4-byte boundary before `nodeCount`. Padding bytes SHALL be zero.

8.3 No other alignment is permitted. Node entries are tightly packed; `path` strings are null-terminated and immediately followed by the next entry.

## 9. Offset Calculation
Let:
- `header_end` = position after `Flags`.
- `blocks_info_size` = `CompressedBlocksInfoSize`.
- `blocks_info_pos` = start position of BlocksInfo blob (compressed form).
- `data_offset` = base offset for node-relative addressing.

9.1 Embedded BlocksInfo (`(Flags & 0x80) == 0`):
- Apply pre-BlocksInfo alignment (Section 8) to `header_end` → `blocks_info_pos`.
- `data_offset = blocks_info_pos + blocks_info_size` (no extra alignment between BlocksInfo and data region unless the bundle author inserted padding; writers SHOULD omit padding; readers SHALL treat unexpected padding as reserved but include it in `data_offset` computation by using actual positions).

9.2 Streamed BlocksInfo (`(Flags & 0x80) != 0`):
- Apply pre-BlocksInfo alignment to header to determine the start of data region: `data_offset = header_end_aligned`.
- `blocks_info_pos = file_length - blocks_info_size` (BlocksInfo resides at EOF). Reading BlocksInfo MUST NOT change `data_offset`.

9.3 Node offsets are relative to `data_offset`: `absolute_offset = data_offset + node.offset`.

9.4 Validity constraints: `0 ≤ node.offset`, `0 ≤ node.size`, and `node.offset + node.size ≤ total_uncompressed_data_span`, where `total_uncompressed_data_span` is the sum of all StorageBlock `uncompressedSize` values. Overlapping node ranges are invalid.

## 10. Decompression Requirements
10.1 BlocksInfo Decompression (per Section 6 mapping):
- LZMA: Use raw LZMA decoding with the properties/dictionary size provided by the Unity stream; MUST supply the expected uncompressed size; MUST fail on size mismatch.
- LZ4/LZ4HC: Use raw block decompression (not framed) with the expected uncompressed size; MUST fail on size mismatch.
 - LZHAM: Only if implemented; otherwise MUST throw `UnsupportedCompression` immediately upon detection.
10.2 Data Blocks: Though not fully consumed here, per-block flags impact downstream reads; sizes MUST be honored for offset correctness.

## 11. Node Directory and Intra-Bundle References
11.1 Node table defines the virtual files contained in the bundle. Each `Node.path` SHALL be unique within the bundle; duplicate paths render the bundle nonconformant.
11.2 The data region is the concatenation of all storage blocks (after per-block decompression). Each node occupies a contiguous slice `[data_offset + node.offset, data_offset + node.offset + node.size)` within that region. Writers SHALL place node payload bytes exactly at that slice; readers SHALL enforce bounds as in 9.4.
11.3 References between files inside the bundle (e.g., `StreamingInfo.path` strings in SerializedFile objects pointing to `.resS` resources) SHALL be resolved by matching the string to a `Node.path`. Writers SHALL ensure referenced resources exist as nodes and the payload bytes at the node’s slice contain the referenced data. Readers SHALL treat unresolved internal references as an error in higher-level validation (outside this container spec) but SHALL still expose the node table as-is.
11.4 Node ordering in the table is non-semantic; offsets and sizes alone define placement. Padding between nodes inside the data region is permitted only if represented as distinct nodes or unused gaps; unused gaps SHALL be treated as reserved.
11.5 Paths are opaque byte strings (UTF-8, null-terminated). Directory separators may appear; no normalization is implied by this spec.
11.6 Unity-produced bundles commonly name resource nodes using CAB hashes (e.g., `archive:/CAB-<hash>/CAB-<hash>.resS`). Writers SHOULD emit stable, unique paths; readers resolving `StreamingInfo.path` SHALL match the full path string to a node path (case-sensitive, byte-exact). If multiple nodes share the same prefix but differ in full path, only an exact match satisfies the reference.

## 12. Resource Streams (.resS)
12.1 `.resS` nodes are ordinary nodes whose payload bytes reside in the data region like any other node; they are not a separate compression domain. Their content MAY be referenced by `StreamingInfo` records inside SerializedFiles.
12.2 `StreamingInfo` SHALL provide `(path, offset, size)` into a `.resS` node. The `offset` and `size` are relative to the start of that node’s payload: `absolute_offset = data_offset + node.offset + streaming.offset`.
12.3 Writers SHALL ensure that `(streaming.offset + streaming.size)` does not exceed the referenced `.resS` node size. Readers SHALL treat overruns as invalid.
12.4 `.resS` naming: commonly `archive:/CAB-<hash>/CAB-<hash>.resS`; exact matching is required (see 11.6). This spec does not constrain the hash algorithm; only the byte-exact path matters.
12.5 `.resS` contents are opaque to the bundle container format: no internal headers or structure are mandated at the container level. Interpretation of `.resS` slices (e.g., Mesh `VertexData` layouts, index buffers) is defined by higher-level object-type specifications external to this document. Implementations SHALL extract the correct byte range per 12.2-12.3; payload parsing is governed by separate normative documents (e.g., Mesh specification, SerializedFile object layout).

## 13. Path Encoding
Node `path` strings SHALL be read as null-terminated UTF-8, preserving bytes exactly as emitted. Implementations MUST NOT trim or re-encode.

## 14. Hash Handling
UnityPy does not verify the BlocksInfo hash; it reads and ignores the 16-byte `Hash128`. This spec aligns with UnityPy: implementations SHALL read and preserve the 16-byte hash but SHALL NOT fail on mismatch because no verification is performed by default. An implementation MAY offer an opt-in verification mode (e.g., compute SHA1 or compare against an externally supplied hash) provided it is disabled by default.

## 15. Implementation Algorithms (Normative)

### 15.1 Null-Terminated String Reading
```
function read_string_to_null(stream):
    bytes = []
    while true:
        b = read_byte(stream)
        if b == 0x00:
            break
        bytes.append(b)
        if len(bytes) > 65536:  // safety limit
            throw StringTooLong
    return decode_utf8(bytes)
```

### 15.2 Alignment Calculation
```
function align(position, boundary):
    return (position + boundary - 1) & ~(boundary - 1)
```

### 15.3 Header Parsing
```
function parse_header(stream):
    header.signature = read_string_to_null(stream)
    if header.signature != "UnityFS":
        throw InvalidSignature
    
    header.version = read_uint32_be(stream)
    header.unity_version = read_string_to_null(stream)
    header.unity_revision = read_string_to_null(stream)
    header.size = read_int64_be(stream)
    header.compressed_blocks_info_size = read_uint32_be(stream)
    header.uncompressed_blocks_info_size = read_uint32_be(stream)
    header.flags = read_uint32_be(stream)
    
    header_end = stream.position
    return (header, header_end)
```

### 15.4 BlocksInfo Location and Decompression
```
function read_blocks_info(stream, header, header_end):
    // Apply pre-BlocksInfo alignment
    if header.version >= 7 and (header.flags & 0x200):
        blocks_info_pos = align(header_end, 16)
    else:
        blocks_info_pos = align(header_end, 4)
    
    // Determine BlocksInfo location
    if header.flags & 0x80:  // streamed at EOF
        data_offset = blocks_info_pos  // data starts after aligned header
        stream.seek(stream.length - header.compressed_blocks_info_size)
        blocks_info_compressed = stream.read(header.compressed_blocks_info_size)
    else:  // embedded
        stream.seek(blocks_info_pos)
        blocks_info_compressed = stream.read(header.compressed_blocks_info_size)
        data_offset = blocks_info_pos + header.compressed_blocks_info_size
    
    // Decompress BlocksInfo
    compression_type = header.flags & 0x3F
    if compression_type == 0:  // None
        blocks_info_data = blocks_info_compressed
    else if compression_type == 1:  // LZMA
        blocks_info_data = decompress_lzma(
            blocks_info_compressed,
            expected_size=header.uncompressed_blocks_info_size
        )
    else if compression_type == 2 or compression_type == 3:  // LZ4/LZ4HC
        blocks_info_data = decompress_lz4(
            blocks_info_compressed,
            expected_size=header.uncompressed_blocks_info_size
        )
    else if compression_type == 4:  // LZHAM
        throw UnsupportedCompression("LZHAM")
    else:
        throw UnsupportedCompression(compression_type)
    
    if len(blocks_info_data) != header.uncompressed_blocks_info_size:
        throw DecompressionSizeMismatch
    
    return (blocks_info_data, data_offset)
```

### 15.5 BlocksInfo Payload Parsing
```
function parse_blocks_info(blocks_info_data):
    reader = BinaryReader(blocks_info_data)
    
    // Hash (16 bytes, preserved only)
    uncompressed_data_hash = reader.read(16)
    
    // Storage blocks
    block_count = reader.read_int32_be()
    storage_blocks = []
    for i in 0..block_count-1:
        block.uncompressed_size = reader.read_uint32_be()
        block.compressed_size = reader.read_uint32_be()
        block.flags = reader.read_uint16_be()
        storage_blocks.append(block)
    
    // Align after block table
    reader.position = align(reader.position, 4)
    
    // Nodes
    node_count = reader.read_int32_be()
    nodes = []
    for i in 0..node_count-1:
        node.offset = reader.read_int64_be()
        node.size = reader.read_int64_be()
        node.flags = reader.read_int32_be()
        node.path = read_string_to_null(reader)
        nodes.append(node)
    
    return (uncompressed_data_hash, storage_blocks, nodes)
```

### 15.6 Hash Handling (optional)
```
function optionally_verify_blocks_info_hash(blocks_info_data, expected_hash):
    // Not performed by default (UnityPy behavior). If enabled and expected_hash provided, compute SHA1 over bytes[16:].
    if expected_hash is None:
        return  // nothing to compare

    payload = blocks_info_data[16:]
    computed_hash = sha1(payload)
    if computed_hash != expected_hash:
        throw HashMismatch
```

### 15.7 Data Region Reconstruction
```
function reconstruct_data_region(stream, data_offset, storage_blocks):
    stream.seek(data_offset)
    data_region = new_stream()  // in-memory or temp file
    
    for block in storage_blocks:
        compression_type = block.flags & 0x3F
        compressed_data = stream.read(block.compressed_size)
        
        if compression_type == 0:  // None
            uncompressed_data = compressed_data
        else if compression_type == 1:  // LZMA
            uncompressed_data = decompress_lzma(
                compressed_data,
                expected_size=block.uncompressed_size
            )
        else if compression_type == 2 or compression_type == 3:  // LZ4/LZ4HC
            uncompressed_data = decompress_lz4(
                compressed_data,
                expected_size=block.uncompressed_size
            )
        else:
            throw UnsupportedCompression(compression_type)
        
        if len(uncompressed_data) != block.uncompressed_size:
            throw DecompressionSizeMismatch
        
        data_region.write(uncompressed_data)
    
    data_region.seek(0)
    return data_region
```

### 15.8 Node Extraction
```
function extract_node(data_region, node):
    if node.offset < 0 or node.size < 0:
        throw InvalidNodeBounds
    
    data_region.seek(node.offset)
    node_data = data_region.read(node.size)
    
    if len(node_data) != node.size:
        throw TruncatedNode
    
    return node_data
```

### 15.9 StreamingInfo Resolution
```
function resolve_streaming_info(nodes, data_region, streaming_info):
    // streaming_info: { path: string, offset: uint64, size: uint64 }
    
    // Find matching node
    target_node = null
    for node in nodes:
        if node.path == streaming_info.path:  // byte-exact, case-sensitive
            target_node = node
            break
    
    if target_node == null:
        throw UnresolvedReference(streaming_info.path)
    
    // Extract slice from node payload
    node_data = extract_node(data_region, target_node)
    
    if streaming_info.offset + streaming_info.size > len(node_data):
        throw StreamingInfoOverrun
    
    return node_data[streaming_info.offset : streaming_info.offset + streaming_info.size]
```

### 15.10 LZMA Decompression Details
```
function decompress_lzma(compressed_data, expected_size):
    // Read LZMA header: 1 byte props + 4 bytes dict_size (little-endian)
    if len(compressed_data) < 5:
        throw InvalidLzmaHeader
    
    props_byte = compressed_data[0]
    dict_size = read_uint32_le_from(compressed_data, 1)
    
    // Props byte encoding (LZMA standard):
    // props = (lc + lp * 9) * 5 + pb
    // where lc = [0..8] (literal context bits)
    //       lp = [0..4] (literal position bits)
    //       pb = [0..4] (position bits)
    // Typically: lc=3, lp=0, pb=2 → props=91 (0x5B)
    
    // Use language-specific LZMA decoder:
    // - .NET: SevenZipHelper or SharpCompress LZMA with props + dict_size
    // Pass props_byte and dict_size to decoder; decoder extracts lc, lp, pb internally
    
    decoder = new_lzma_decoder_with_props(props_byte, dict_size)
    uncompressed = decoder.decompress(
        input_stream=ByteStream(compressed_data[5:]),
        output_size=expected_size
    )
    
    if len(uncompressed) != expected_size:
        throw LzmaDecompressionSizeMismatch
    
    return uncompressed
```

### 15.11 LZ4 Decompression Details
```
function decompress_lz4(compressed_data, expected_size):
    // Use raw LZ4 block decompression (NOT LZ4 frame format).
    // Input: raw compressed bytes; output: raw decompressed bytes.
    // No LZ4 frame header or magic bytes.
    
    // Recommended C# library: K4os.Compression.LZ4
    // API: LZ4Codec.Decode(byte[] source, byte[] target) → decompressed length
    
    uncompressed = new_byte_array(expected_size)
    decoded_length = lz4_decode(
        source=compressed_data,
        target=uncompressed,
        target_length=expected_size
    )
    
    if decoded_length != expected_size:
        throw Lz4DecompressionSizeMismatch
    
    return uncompressed[0:decoded_length]
```

### 15.12 Parsing Sequence (State Machine)
The following sequence SHALL be followed to ensure correct parsing:

```
State: START
  │
  ├─→ Parse header (Section 15.3)
  │   - If signature invalid → ERROR (InvalidSignature)
  │   - Save header_end position
  │
State: HEADER_VALID
  │
  ├─→ Compute BlocksInfo location and read compressed blob (Section 15.4)
  │   - If version ≥7 and (flags & 0x200) → align to 16 bytes
  │   - Else align to 4 bytes
  │   - If (flags & 0x80) → seek to EOF - compressed_size
  │   - Else read from aligned header_end
  │   - Save data_offset for later
  │
State: BLOCKS_INFO_COMPRESSED_READ
  │
  ├─→ Decompress BlocksInfo (Section 15.4)
  │   - Check compression type (flags & 0x3F)
  │   - If unsupported → ERROR (UnsupportedCompression)
  │   - Decompress to expected_size
  │   - If size mismatch → ERROR (DecompressionSizeMismatch)
  │
State: BLOCKS_INFO_DECOMPRESSED
    │
    ├─→ Optionally verify hash (Section 15.6)
    │   - Default: skip (UnityPy behavior); if enabled, compute SHA1 of bytes [16:] and compare to provided reference
    │   - On mismatch (when enabled) → ERROR (HashMismatch)
    │
State: HASH_HANDLED
  │
  ├─→ Parse BlocksInfo payload (Section 15.5)
  │   - Extract storage blocks table
  │   - Extract node table
  │   - If truncated → ERROR (TruncatedBlocksInfo)
  │
State: BLOCKS_INFO_PARSED
  │
  ├─→ Validate nodes (see Section 15.13)
  │   - Check each node.offset >= 0, node.size >= 0
  │   - Check node.offset + node.size <= total_uncompressed_span
  │   - Validate unique node.path entries
  │   - If invalid → ERROR (InvalidNodeMetadata)
  │
State: NODES_VALIDATED
  │
  ├─→ Reconstruct data region (Section 15.7)
  │   - For each storage block, read and decompress per block.flags
  │   - Concatenate to single data region
  │   - If any block fails → ERROR (BlockDecompressionFailed)
  │
State: DATA_REGION_READY
  │
  └─→ SUCCESS: Return (header, storage_blocks, nodes, data_region)
```

### 15.13 Node Validation
```
function validate_nodes(storage_blocks, nodes):
    // Calculate total uncompressed data span
    total_uncompressed_span = sum(block.uncompressed_size for block in storage_blocks)
    
    // Track seen paths
    seen_paths = set()
    
    for node in nodes:
        // Check offset and size are non-negative
        if node.offset < 0:
            throw NegativeNodeOffset
        if node.size < 0:
            throw NegativeNodeSize
        
        // Check bounds within data region
        if node.offset + node.size > total_uncompressed_span:
            throw NodeOutOfBounds
        
        // Check path uniqueness
        if node.path in seen_paths:
            throw DuplicateNodePath(node.path)
        seen_paths.add(node.path)
    
    // Check for overlapping nodes (optional but recommended)
    sorted_nodes = sort(nodes, key=node.offset)
    for i in 0..len(sorted_nodes)-2:
        if sorted_nodes[i].offset + sorted_nodes[i].size > sorted_nodes[i+1].offset:
            warn("Overlapping nodes detected")  // or throw OverlappingNodes
```

### 15.14 Storage Block Flags Handling
```
function process_storage_block(block):
    compression_type = block.flags & 0x3F
    is_streamed = (block.flags & 0x40) != 0
    
    // compression_type: 0=None, 1=LZMA, 2=LZ4, 3=LZ4HC, 4=LZHAM
    // is_streamed: indicates block may be loaded on-demand (informational; 
    //              all blocks are decompressed sequentially in this implementation)
    
    // Reserved bits (block.flags & 0xFF80): MUST be zero. If non-zero, warn or error.
    reserved = block.flags & 0xFF80
    if reserved != 0:
        warn(f"Non-zero reserved bits in block flags: {hex(reserved)}")
```

### 15.15 Node Flags Preservation
```
// Node.flags is reserved. Implementations SHALL:
// - Read and store the value as-is
// - Not interpret or act on bits in node.flags
// - Preserve when serializing/writing (for round-trip fidelity)
// - Treat non-zero values as benign (no error)
```

### 15.16 StreamingInfo Seek Validation
```
function validate_streaming_info_seek(blocks_info_pos, data_offset, block_count):
    // For streamed BlocksInfo (flags & 0x80), ensure data region and BlocksInfo don't overlap
    // data_offset: start of uncompressed data
    // blocks_info_pos: file position of compressed BlocksInfo (at EOF)
    // Constraint: data_offset should not exceed blocks_info_pos in file
    
    // This is checked implicitly if parsing is correct:
    // - header_end → data_offset (< blocks_info_pos at EOF)
    
    if data_offset > blocks_info_pos:
        throw DataAndBlocksInfoOverlap
```

## 16. C# Implementation Specifics (Normative for .NET Port)

### 16.1 Exception Hierarchy
Define the following exception classes (inherit from `Exception`):

```csharp
namespace UnityAssetParser.Bundle
{
    public class BundleException : Exception
    {
        public BundleException(string message) : base(message) { }
    }
    
    public class InvalidSignatureException : BundleException { }
    public class UnsupportedCompressionException : BundleException { }
    public class HashMismatchException : BundleException { }
    public class DecompressionSizeMismatchException : BundleException { }
    public class TruncatedBlocksInfoException : BundleException { }
    public class InvalidNodeMetadataException : BundleException { }
    public class DuplicateNodePathException : BundleException { }
    public class NegativeNodeOffsetException : BundleException { }
    public class NegativeNodeSizeException : BundleException { }
    public class NodeOutOfBoundsException : BundleException { }
    public class LzmaDecompressionException : BundleException { }
    public class Lz4DecompressionException : BundleException { }
    public class StringTooLongException : BundleException { }
    public class DataAndBlocksInfoOverlapException : BundleException { }
}
```

### 16.2 Compression Library Recommendations

**LZMA:**
- Recommended: `SharpCompress` NuGet package (MIT license, well-maintained)
- API: `SharpCompress.Compressors.LZMA.LzmaStream`
- Alternative: `SevenZipSharp` (wraps 7-Zip native library; requires unmanaged DLL)

**LZ4:**
- Recommended: `K4os.Compression.LZ4` NuGet package (BSD license, actively maintained)
- API: `K4os.Compression.LZ4.LZ4Codec.Decode(byte[] source, byte[] target)`
- Alternative: `DamienG.Security.Cryptography` (older, less frequent updates)

**Hash128 handling:**
- Default behavior: read/preserve 16-byte hash; no verification required (UnityPy behavior).
- Optional verification (if enabled): `System.Security.Cryptography.SHA1` (`SHA1.Create().ComputeHash(byte[])`) over `blocks_info_data[16:]` compared to an externally supplied reference.

### 16.3 Concrete Test Case (Hex Dump)

**Input: Small UnityFS header (uncompressed BlocksInfo)**

```
Header:
  Signature: "UnityFS" (0x556E6974794653 + 0x00)
  Version: 6 (0x06 0x00 0x00 0x00)
  UnityVersion: "2022.3.0f1" (0x... + 0x00)
  UnityRevision: "..." (0x... + 0x00)
  Size: 0x1000 (0x00 0x10 0x00 0x00 0x00 0x00 0x00 0x00)
  CompressedBlocksInfoSize: 0x100 (0x00 0x01 0x00 0x00)
  UncompressedBlocksInfoSize: 0x100 (0x00 0x01 0x00 0x00)
  Flags: 0x00 (no compression, embedded BlocksInfo)

BlocksInfo (offset 0x?? after strings, 256 bytes):
    uncompressedDataHash: [16 bytes Hash128]
  blockCount: 1 (0x01 0x00 0x00 0x00)
  StorageBlock[0]:
    uncompressedSize: 0x900 (0x00 0x09 0x00 0x00)
    compressedSize: 0x900 (0x00 0x09 0x00 0x00)
    flags: 0x00 (no compression, not streamed)
  [4-byte align padding]
  nodeCount: 1 (0x01 0x00 0x00 0x00)
  Node[0]:
    offset: 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 (relative to data_offset)
    size: 0x00 0x09 0x00 0x00 0x00 0x00 0x00 0x00
    flags: 0x00 0x00 0x00 0x00 (reserved)
    path: "assets/mesh.serialized" (0x... + 0x00)

Data Region (offset 0x??):
  [0x900 bytes of decompressed data for node 0]
```

Expected parsed output:
```json
{
  "header": {
    "signature": "UnityFS",
    "version": 6,
    "unity_version": "2022.3.0f1",
    "unity_revision": "...",
    "size": 4096,
    "compressed_blocks_info_size": 256,
    "uncompressed_blocks_info_size": 256,
    "flags": 0
  },
  "storage_blocks": [
    {
      "uncompressed_size": 2304,
      "compressed_size": 2304,
      "flags": 0
    }
  ],
  "nodes": [
    {
      "offset": 0,
      "size": 2304,
      "flags": 0,
      "path": "assets/mesh.serialized"
    }
  ],
  "data_offset": 272
}
```

### 16.4 Version-Specific Behavior

**Version < 6:**
Not covered by this spec; treat as error or legacy path (out of scope for UnityFS).

**Version 6:**
- Standard UnityFS layout
- Align: 4-byte default (ignore 0x200 flag)
- No 16-byte alignment

**Version 7:**
- Enhanced UnityFS
- Align: check (Flags & 0x200) for 16-byte alignment
- New metadata fields may be present; implementation may skip them if unrecognized

**Version > 7:**
- Treat as unsupported unless explicitly handled
- Recommendation: log warning and attempt to parse as version 7

## 17. Error Conditions (Mandatory Failures)
Implementations SHALL reject the bundle when any of the following occur:
- Invalid signature (not `UnityFS`).
- Unsupported compression flag (Section 6.3).
- Hash verification failure (Section 14) when verification is enabled.
- Decompression failure or size mismatch (Section 10).
- Truncated BlocksInfo preventing complete reads (block or node tables).
- Negative or overflow offsets/sizes, or clearly overlapping node regions.

## 18. Conformance Test Matrix
Implementations SHALL be validated against fixtures covering at minimum:
- Valid, uncompressed BlocksInfo and data blocks.
- Valid, LZMA-compressed BlocksInfo.
- Valid, LZ4/LZ4HC-compressed BlocksInfo.
- Multi-node bundles including external `.resS`.
- Wrong magic/signature.
- Hash mismatch case (only relevant if optional verification is enabled).
- Truncated BlocksInfo (early EOF).
- Unsupported compression flag (e.g., `0x04`).
- Alignment sanity: parsed offsets match UnityPy JSON output for Header + Nodes.

## 19. Validation Method
19.1 Parse each fixture with the C# implementation; serialize Header + Nodes to JSON.
19.2 Parse the same fixture with UnityPy; serialize Header + Nodes to JSON.
19.3 The JSON outputs SHALL match exactly (all integral and string fields). Any divergence SHALL be treated as non-conformant.

## 20. Porting Checklist (Normative)
- [ ] Header read order and big-endian interpretation match UnityPy.
- [ ] 4-byte alignment applied exactly where UnityPy aligns.
- [ ] Compression flag mapping (`Flags & 0x3F`, `Flags & 0x80`) implemented with rejection of unsupported values.
- [ ] Raw LZMA decoder used with props/dict size; raw LZ4 block decoder used with expected size.
- [ ] `data_offset` computed identically to UnityPy for both embedded and streamed BlocksInfo.
- [ ] Node paths read as null-terminated UTF-8.
- [ ] Hash128 read/preserve behavior matches UnityPy (no default verification; optional SHA1 check is opt-in).
- [ ] Explicit exceptions raised for all error conditions in Section 17.
- [ ] JSON parity with UnityPy confirmed for all fixtures in Section 18.

## 21. Status
This specification is normative and SHALL be treated as the authoritative contract for all BundleFile parser implementations in this project.
