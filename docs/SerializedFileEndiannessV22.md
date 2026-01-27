# SerializedFile Version 22+ Endianness Architecture

## Overview

**Critical Discovery**: Unity 2022+ (SerializedFile format version 22+) uses a **dual endianness system** where the header and metadata regions may have different byte orders. This differs fundamentally from earlier versions which use single endianness throughout.

## The Problem

Early implementations assume the `Endianness` byte applies to the entire file. This causes parsing to fail for version 22+ files because:

1. The header must be readable in platform-native endianness to determine file validity
2. The `Endianness` byte only applies to the metadata region (type tree, object table, file identifiers)
3. Reading the version field with wrong endianness produces invalid values (e.g., 369098752 instead of 22)

## The Solution: Dual Endianness Architecture

### Phase 1: Header Endianness Detection (Bytes 0-19)

**Goal**: Determine the endianness used by the header itself (independent of content).

**Method**: Try both endiannesses and check if the result is valid:

```csharp
private static bool TryDetectEndianness(byte[] headerBytes, out Endian detectedEndian)
{
    // Try little-endian
    var littleVersion = BitConverter.ToUInt32(headerBytes, 12);
    if (littleVersion >= 9 && littleVersion <= 30) {
        detectedEndian = Endian.Little;
        return false; // indicates little-endian was detected
    }
    
    // Try big-endian
    var bigVersion = Reverse32(BitConverter.ToUInt32(headerBytes, 12));
    if (bigVersion >= 9 && bigVersion <= 30) {
        detectedEndian = Endian.Big;
        return true; // indicates big-endian was detected
    }
    
    throw new InvalidOperationException("Cannot determine header endianness: version out of range");
}

private static uint Reverse32(uint value) 
    => ((value & 0xFF) << 24) | 
       (((value >> 8) & 0xFF) << 16) | 
       (((value >> 16) & 0xFF) << 8) | 
       ((value >> 24) & 0xFF);
```

**Valid Version Range**: Unity uses format versions 9-30. If detected version falls outside this range, try the opposite endianness.

### Phase 2: Read Initial Header (Bytes 0-19)

Once endianness is known, read the initial header structure:

```
Offset  Size  Field
0       4     MetadataSize (uint32)
4       8     FileSize (int64)
12      4     Version (uint32)
16      8     DataOffset (int64)
Total:  24 bytes (but we only use 20 for detection)
```

**Apply detected endianness** to all these fields.

### Phase 3: Read Extended Header for Version 22+ (Bytes 20-47)

Version 22+ files have additional header fields:

```
Offset  Size  Field
20      4     MetadataSizeExtended (uint32) - actual metadata size (more accurate)
24      4     FileSizeExtended (uint32) - total file size (more accurate)
28      4     DataOffsetExtended (uint32) - data region offset (more accurate)
32      16    [Reserved/unused padding]
Total:  28 bytes (bytes 20-47)
```

**CRITICAL**: Apply the **same detected endianness** from Phase 1, NOT the `Endianness` byte.

```csharp
if (version >= 22) {
    // Use headerEndian (detected), NOT metadata endianness
    var reader = new EndianBinaryReader(stream, headerEndian);
    reader.Seek(20);
    var metadataSizeExt = reader.ReadUInt32();
    var fileSizeExt = reader.ReadUInt32();
    var dataOffsetExt = reader.ReadUInt32();
    // Use _Ext values instead of initial 20-byte values
}
```

### Phase 4: Metadata Region Starts (Byte 20 or 48)

For version >= 22: metadata region starts at byte 48 (after the 48-byte header).
For version < 22: metadata region starts at byte 20 (after the 20-byte header).

The first byte of the metadata region is the `Endianness` byte, which determines the byte order for all metadata structures.

```csharp
long metadataStart = (version >= 22) ? 48 : 20;
var endiannessByte = reader.ReadByte(metadataStart);
var metadataEndian = endiannessByte == 0 ? Endian.Little : Endian.Big;

// Switch to metadata endianness for parsing type tree, object table, etc.
var metadataReader = new EndianBinaryReader(stream, metadataEndian);
```

### Phase 5: Parse Metadata Region

**All subsequent parsing uses `metadataEndian`**:

- Type tree structure
- Object table entries
- File identifiers (externals)
- Type tree nodes and string buffers

This includes **type tree blob format**: even though bytes are consumed, the byte ordering still matters for endianness consistency if individual fields are later parsed.

## Code Pattern: Complete Header + Metadata Parsing

```csharp
public static SerializedFile Parse(ReadOnlySpan<byte> data)
{
    using (var stream = new MemoryStream(data.ToArray()))
    using (var baseReader = new BinaryReader(stream)) {
        
        // Phase 1: Detect header endianness
        var headerBytes = baseReader.ReadBytes(20);
        bool headerIsBig = TryDetectEndianness(headerBytes, out var headerEndian);
        
        // Phase 2: Read initial header (20 bytes)
        stream.Seek(0);
        var headerReader = new EndianBinaryReader(stream, headerEndian);
        uint metadataSize = headerReader.ReadUInt32();
        long fileSize = headerReader.ReadInt64();
        uint version = headerReader.ReadUInt32();
        long dataOffset = headerReader.ReadInt64();
        
        // Phase 3: Read extended header for v22+
        if (version >= 22) {
            var metadataSizeExt = headerReader.ReadUInt32();
            var fileSizeExt = headerReader.ReadUInt32();
            var dataOffsetExt = headerReader.ReadUInt32();
            // Use _Ext values for preference (more reliable)
            metadataSize = metadataSizeExt;
            fileSize = fileSizeExt;
            dataOffset = dataOffsetExt;
        }
        
        // Phase 4: Read metadata endianness byte
        long metadataStart = (version >= 22) ? 48 : 20;
        stream.Seek(metadataStart);
        byte endiannessByte = baseReader.ReadByte();
        var metadataEndian = endiannessByte == 0 ? Endian.Little : Endian.Big;
        
        // Phase 5: Parse metadata with correct endianness
        var metadataReader = new EndianBinaryReader(stream, metadataEndian);
        
        // ... rest of parsing (type tree, object table, file identifiers)
        
        return new SerializedFile { ... };
    }
}
```

## Type Tree Blob Format (Version 5.5+)

Once endianness is correct, type trees in modern Unity versions use packed blob format:

```
[nodeCount: int32]              // Number of type tree nodes
[stringBufferSize: int32]       // Total bytes in string buffer
[node0 ... nodeN]               // Fixed-size structures (24 or 32 bytes each)
[stringBuffer]                  // Variable-length string data
```

**Node Structure Size**:
- Version < 19: 24 bytes per node
- Version >= 19: 32 bytes per node

**Blob Consumption**:
```csharp
int nodeCount = metadataReader.ReadInt32();
int stringBufferSize = metadataReader.ReadInt32();
int structSize = version >= 19 ? 32 : 24;
int totalNodeBytes = structSize * nodeCount;

// Consume blob without parsing individual fields
byte[] nodeBlob = metadataReader.ReadBytes(totalNodeBytes);
byte[] stringBlob = metadataReader.ReadBytes(stringBufferSize);

// Stream position is now after type tree; ready for object table
```

## Metadata Region Boundaries

The metadata region is bounded by `MetadataSize`:
- Start: byte 20 (v<22) or byte 48 (v22+)
- End: MetadataSize bytes from start

**Important**: File identifiers (externals) may not exist if the metadata region ends early:

```csharp
try {
    var externals = ParseFileIdentifiers(metadataReader, version);
} catch (EndOfStreamException) {
    externals = new List<FileIdentifier>(); // Graceful fallback
}
```

## Test Fixture: Cigar_neck.hhh (Version 22 Example)

From real bundle parsing:

```
Raw Bytes 0-19:    E8 7C 03 00 | 30 82 04 00 | 16 00 00 00 | C0 7C 03 00
                   MetadataSize | FileSize   | Version    | DataOffset

Detected: Little-endian (version=22 is valid)

Bytes 20-47:       E8 7C 03 00 | 30 82 04 00 | C0 7C 03 00 | [padding]
                   MetadataSizeExt | FileSizeExt | DataOffsetExt

Byte 48 (Endianness): 0x00 = Little-endian metadata
Bytes 49+: Type tree (nodeCount=10, stringBuffer=...)
```

## Common Parsing Errors & Fixes

| Error | Root Cause | Solution |
|-------|-----------|----------|
| `Version = 369098752` | Used wrong endianness for header | Implement endianness detection |
| `FileSize < MetadataSize` | Extended header read with wrong endianness | Use detected endianness for bytes 20-47 |
| `UTF-8 decode failed at offset X` | Attempted to parse type tree nodes individually | Consume blob format as raw bytes |
| `Stream read beyond end` | Metadata region boundary reached before externals | Wrap externals parsing in try-catch |
| `Object table misaligned` | Wrong endianness for type tree blob | Verify metadata endianness byte is correct |

## References

- **UnityPy Implementation**: [K0lb3/UnityPy](https://github.com/K0lb3/UnityPy) - Python reference implementation
- **UnityFS Bundle Spec**: See `docs/UnityFS-BundleSpec.md`
- **Type Tree Formats**: Tested with Cigar_neck.hhh (v22), FrogHatSmile.hhh, BambooCopter.hhh
