# Parser Oracle Workflow (UnityPy Reference)

Use UnityPy as a reference implementation when parser behavior is ambiguous or when fixture expectations need independent validation.

## Objective

Compare this repo's parser output against deterministic UnityPy-derived outputs for the same fixture.

## Deterministic comparison contract

For each fixture under investigation, compare:

1. Entry paths discovered.
2. Entry sizes.
3. SHA-256 hashes of key payloads.
4. Decompression output sizes/hashes.
5. Selected serialized metadata (header version/endian/object count/type hints).

Normalize output ordering and avoid timestamps in all artifacts.

## Oracle process

1. Select fixture and record fixture hash.
2. Generate UnityPy manifest output.
3. Run local parser tests/commands.
4. Diff parser output vs oracle output.
5. Categorize delta:
   - parser bug,
   - fixture expectation bug,
   - unsupported feature (document explicitly).

## Output format recommendation

Prefer JSON with this shape:

```json
{
  "schemaVersion": 1,
  "fixture": {
    "path": "...",
    "sha256": "..."
  },
  "entries": [
    {
      "path": "...",
      "kind": "asset|bundle|web|resource|unknown",
      "size": 0,
      "sha256": "..."
    }
  ],
  "decompression": [
    {
      "context": "...",
      "algorithm": "lz4|lzma|gzip|brotli|none",
      "compressedSize": 0,
      "uncompressedSize": 0,
      "sha256": "..."
    }
  ],
  "serialized": [
    {
      "name": "...",
      "headerVersion": 0,
      "endian": "<|>",
      "objectCount": 0,
      "externalCount": 0
    }
  ]
}
```

## Prompts for UnityPy-focused AI agent

### 1) Fixture manifest oracle

```
Using UnityPy, load fixture <FIXTURE_PATH> and emit deterministic JSON with:
- detected top-level file type(s)
- every internal entry path
- entry kind classification (asset, bundle, web, resource, unknown)
- entry size in bytes
- SHA-256 per entry payload

Sort entries lexicographically by path, include UnityPy version, and do not include timestamps.
```

### 2) Unitypackage asset-entry oracle

```
Parse unitypackage fixture <FIXTURE_PATH> with UnityPy and output all tar entries ending in /asset.
For each include: tar path, size, SHA-256, and UnityPy-detected payload classification.
Return strict JSON only.
```

### 3) Compression parity oracle

```
For fixture <FIXTURE_PATH>, identify all compressed blocks UnityPy processes.
For each block output: context path/index, algorithm flag, compressed size, uncompressed size, and SHA-256 of decompressed bytes.
Return deterministic JSON sorted by context.
```

### 4) Serialized metadata oracle

```
For fixture <FIXTURE_PATH>, output per SerializedFile:
- file name/path
- header version
- endian
- object count
- externals count
- first 25 objects: path_id, class_id/type name, byte_size, and name hint if available

Return deterministic JSON only.
```

### 5) Typetree spot-check oracle

```
For fixture <FIXTURE_PATH> and class IDs <CLASS_IDS>, resolve typetree nodes with UnityPy and emit normalized dumps containing:
- type
- name
- byte size
- meta flag
- level/index

Include Unity version used for resolution and return JSON only.
```

### 6) Failure reproduction pack

```
Given fixture <FIXTURE_PATH> and symptom <SYMPTOM>, run UnityPy parse with diagnostics and return:
- parse phase reached
- failing context (entry/object/block)
- stack trace
- minimal reproducible Python script
- output artifact paths
```

### 7) Differential artifact generator

```
Generate two deterministic artifacts for fixture <FIXTURE_PATH>:
1) full JSON manifest (schemaVersion, fixture hash, entries, decompression, serialized metadata)
2) diff-friendly TSV columns: path, kind, size, sha256

Sort lexicographically and avoid timestamps.
```

### 8) C# contract emitter

```
Produce a machine-readable contract for fixture <FIXTURE_PATH> that .NET tests can consume directly.
Include: schemaVersion, fixture hash, expected entry paths/sizes/hashes, decompression checks, and selected serialized metadata expectations.
Return JSON only.
```