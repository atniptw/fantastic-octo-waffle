# BundleFile API Documentation

The `BundleFile` class is the top-level orchestration layer for parsing UnityFS bundle files. It coordinates all parsing stages, enforces state machine validation, and provides a unified API for accessing bundle contents.

## Quick Start

```csharp
using UnityAssetParser.Bundle;

// Parse a bundle file
using var stream = File.OpenRead("bundle.hhh");
var bundle = BundleFile.Parse(stream);

// Access metadata
Console.WriteLine($"Bundle: {bundle.Header.Signature} v{bundle.Header.Version}");
Console.WriteLine($"Nodes: {bundle.Nodes.Count}");

// Get a specific node
var node = bundle.GetNode("CAB-abc123");
if (node != null)
{
    var data = bundle.ExtractNode(node);
    Console.WriteLine($"Node size: {data.Length} bytes");
}

// Export to JSON for validation
var json = bundle.ToJson();
File.WriteAllText("bundle_metadata.json", json);
```

## API Reference

### Parsing Methods

#### `BundleFile.Parse(Stream stream)`

Parses a UnityFS bundle using **fail-fast** semantics. Throws an exception on any error.

**Parameters:**
- `stream` - Seekable stream positioned at bundle start

**Returns:** `BundleFile` instance

**Exceptions:**
- `InvalidBundleSignatureException` - Invalid signature (not "UnityFS")
- `UnsupportedVersionException` - Version not in {6, 7}
- `HeaderParseException` - Malformed header
- `HashMismatchException` - BlocksInfo hash verification failed
- `DuplicateNodeException` - Duplicate node paths
- `BoundsException` - Invalid node bounds
- `NodeOverlapException` - Overlapping nodes
- `UnsupportedCompressionException` - Unsupported compression type
- `BundleException` - Other parsing errors

**Example:**
```csharp
try
{
    var bundle = BundleFile.Parse(stream);
    // Success
}
catch (HashMismatchException ex)
{
    Console.WriteLine($"Corrupt bundle: {ex.Message}");
}
```

#### `BundleFile.TryParse(Stream stream)`

Parses a UnityFS bundle using **error collection** semantics. Never throws, collects all errors.

**Parameters:**
- `stream` - Seekable stream positioned at bundle start

**Returns:** `ParseResult` with `Success`, `Bundle`, `Warnings`, `Errors`

**Example:**
```csharp
var result = BundleFile.TryParse(stream);

if (result.Success)
{
    Console.WriteLine("Parsed successfully");
    // Use result.Bundle
}
else
{
    Console.WriteLine("Parse failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}
```

### Node Access Methods

#### `GetNode(string path)`

Gets a node by exact path match (case-sensitive).

**Parameters:**
- `path` - Node path to search for

**Returns:** `NodeInfo?` - Node if found, null otherwise

**Example:**
```csharp
var node = bundle.GetNode("CAB-abc123/CAB-abc123");
if (node != null)
{
    Console.WriteLine($"Found node at offset {node.Offset}, size {node.Size}");
}
```

#### `ExtractNode(NodeInfo node)`

Extracts a node's payload data from the data region.

**Parameters:**
- `node` - Node to extract

**Returns:** `ReadOnlyMemory<byte>` - Node payload

**Exceptions:**
- `BoundsException` - If node bounds are invalid

**Example:**
```csharp
var node = bundle.Nodes[0];
var data = bundle.ExtractNode(node);

// Write to file
using var file = File.Create($"{node.Path}.dat");
file.Write(data.Span);
```

#### `GetMetadataNode()`

Gets the metadata node (conventionally Node 0 in Unity bundles, contains SerializedFile).

**Returns:** `NodeInfo?` - First node if present, null otherwise

**Example:**
```csharp
var metadataNode = bundle.GetMetadataNode();
if (metadataNode != null)
{
    var serializedFileData = bundle.ExtractNode(metadataNode);
    // Parse SerializedFile from data
}
```

#### `ResolveStreamingInfo(StreamingInfo info)`

Resolves a StreamingInfo reference to byte data (used for external .resS resources).

**Parameters:**
- `info` - StreamingInfo reference from mesh or asset

**Returns:** `ReadOnlyMemory<byte>` - Referenced data slice

**Exceptions:**
- `StreamingInfoException` - If path not found or bounds invalid

**Example:**
```csharp
// From a parsed mesh
var streamingInfo = new StreamingInfo
{
    Path = "CAB-abc123.resS",
    Offset = 0,
    Size = 16384
};

var vertexData = bundle.ResolveStreamingInfo(streamingInfo);
```

### Serialization Methods

#### `ToMetadata()`

Converts the bundle to metadata for JSON serialization (excludes binary data).

**Returns:** `BundleMetadata` - Metadata DTO

**Example:**
```csharp
var metadata = bundle.ToMetadata();
Console.WriteLine($"Header version: {metadata.Header.Version}");
Console.WriteLine($"Storage blocks: {metadata.StorageBlocks.Count}");
Console.WriteLine($"Nodes: {metadata.Nodes.Count}");
```

#### `ToJson()`

Serializes the bundle to JSON string for validation.

**Returns:** `string` - Formatted JSON

**Example:**
```csharp
var json = bundle.ToJson();
File.WriteAllText("bundle.json", json);

// Compare with UnityPy reference
var expectedJson = File.ReadAllText("bundle_expected.json");
Assert.Equal(expectedJson, json);
```

### Properties

#### `Header` (UnityFSHeader)

Parsed header metadata including signature, version, Unity version, size, flags.

```csharp
Console.WriteLine($"Signature: {bundle.Header.Signature}");
Console.WriteLine($"Version: {bundle.Header.Version}");
Console.WriteLine($"Unity: {bundle.Header.UnityVersion} ({bundle.Header.UnityRevision})");
Console.WriteLine($"Size: {bundle.Header.Size} bytes");
Console.WriteLine($"Compression: {bundle.Header.CompressionType}");
Console.WriteLine($"Streamed: {bundle.Header.BlocksInfoAtEnd}");
```

#### `BlocksInfo` (BlocksInfo)

Parsed BlocksInfo containing storage blocks and nodes.

```csharp
Console.WriteLine($"Storage blocks: {bundle.BlocksInfo.Blocks.Count}");
Console.WriteLine($"Total uncompressed: {bundle.BlocksInfo.TotalUncompressedDataSize} bytes");
```

#### `DataRegion` (DataRegion)

Reconstructed data region (decompressed and concatenated blocks).

```csharp
Console.WriteLine($"Data region size: {bundle.DataRegion.Length} bytes");
```

#### `DataOffset` (long)

Absolute file offset where data region begins.

```csharp
Console.WriteLine($"Data offset: {bundle.DataOffset}");
```

#### `Nodes` (IReadOnlyList&lt;NodeInfo&gt;)

List of nodes (virtual files) in the bundle.

```csharp
foreach (var node in bundle.Nodes)
{
    Console.WriteLine($"Node: {node.Path}");
    Console.WriteLine($"  Offset: {node.Offset}, Size: {node.Size}, Flags: {node.Flags}");
}
```

## State Machine

The parsing process follows a strict state machine (§15.12 of spec):

```
START
  ↓
HEADER_VALID (Parse header)
  ↓
BLOCKS_INFO_DECOMPRESSED (Decompress BlocksInfo)
  ↓
HASH_VERIFIED (Verify SHA1)
  ↓
NODES_VALIDATED (Validate bounds, uniqueness, overlaps)
  ↓
DATA_REGION_READY (Reconstruct data region)
  ↓
SUCCESS
```

Each step must complete successfully before the next step begins. If any step fails, the state machine transitions to `Failed` and throws an exception (or collects error in `TryParse`).

## JSON Schema

The `ToJson()` method produces output matching this schema (compatible with UnityPy):

```json
{
  "header": {
    "signature": "UnityFS",
    "version": 6,
    "unity_version": "2020.3.48f1",
    "unity_revision": "b805b124c6b7",
    "size": 1234567,
    "compressed_blocks_info_size": 256,
    "uncompressed_blocks_info_size": 512,
    "flags": 2
  },
  "storage_blocks": [
    {
      "uncompressed_size": 16384,
      "compressed_size": 8192,
      "flags": 2
    }
  ],
  "nodes": [
    {
      "offset": 0,
      "size": 16384,
      "flags": 0,
      "path": "CAB-abc123"
    }
  ],
  "data_offset": 300
}
```

## Error Handling Best Practices

### Use Parse() for:
- Critical operations where failure should halt execution
- Simple error handling (catch specific exception types)
- Interactive tools where user can fix issues

### Use TryParse() for:
- Batch processing where you want to report all errors at once
- Validation tools that need comprehensive diagnostics
- Non-critical operations where partial results are acceptable

**Example - Batch Validation:**
```csharp
var results = new List<(string Path, bool Success, List<string> Errors)>();

foreach (var bundlePath in bundlePaths)
{
    using var stream = File.OpenRead(bundlePath);
    var result = BundleFile.TryParse(stream);
    results.Add((bundlePath, result.Success, result.Errors.ToList()));
}

// Generate report
foreach (var (path, success, errors) in results)
{
    if (!success)
    {
        Console.WriteLine($"❌ {path}:");
        foreach (var error in errors)
        {
            Console.WriteLine($"   {error}");
        }
    }
    else
    {
        Console.WriteLine($"✓ {path}");
    }
}
```

## Performance Considerations

See [BundleFilePerformance.md](BundleFilePerformance.md) for details.

**Summary:**
- Current implementation loads entire data region into memory
- Suitable for bundles < 100MB (typical R.E.P.O. mods)
- Parse time: ~50ms per 1MB, ~3.7s for 100MB
- Memory usage: ~1.5x bundle size during parsing

## Related Documentation

- [UnityFS-BundleSpec.md](UnityFS-BundleSpec.md) - Format specification
- [BundleFilePerformance.md](BundleFilePerformance.md) - Performance analysis
- [TestingStrategy.md](TestingStrategy.md) - Testing approach
- [scripts/README.md](../scripts/README.md) - Validation scripts
