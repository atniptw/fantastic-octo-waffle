# Scripts

Utility scripts for the R.E.P.O. Mod Browser project.

## generate_reference_json.py

Generates reference JSON outputs from UnityFS bundle files using UnityPy for validation against the C# implementation.

## dump_unitypy_object_tree.py

Dumps full UnityPy object trees (TypeTree output) to JSON for comparison with the C# parser.

### Usage

```bash
pip install UnityPy
python scripts/dump_unitypy_object_tree.py path/to/bundle.hhh -o bundle_unitypy_tree.json
```

## dump_csharp_object_tree.csx

Dumps full object trees from the C# `SerializedFile` parser using `TypeTreeReader`.

### Usage

```bash
dotnet script scripts/dump_csharp_object_tree.csx path/to/bundle.hhh -o bundle_csharp_tree.json
```

## compare_object_trees.py

Compares UnityPy vs C# object tree JSON outputs and prints the first N diffs.

### Usage

```bash
python scripts/compare_object_trees.py bundle_unitypy_tree.json bundle_csharp_tree.json
```

### Requirements

```bash
pip install UnityPy
```

### Usage

Process a single bundle file:
```bash
python scripts/generate_reference_json.py path/to/bundle.hhh
# Output: path/to/bundle_expected.json
```

Process all fixtures:
```bash
python scripts/generate_reference_json.py --all
# Processes all .hhh files in Tests/UnityAssetParser.Tests/Fixtures/
```

Custom output path:
```bash
python scripts/generate_reference_json.py bundle.hhh -o output.json
```

### Output Format

The script generates JSON matching the C# `BundleMetadata` schema:

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
      "path": "CAB-abc123/CAB-abc123"
    }
  ],
  "data_offset": 300
}
```

### Integration with Tests

The generated JSON files are used by integration tests to validate that the C# parser produces identical output to UnityPy:

```csharp
var bundle = BundleFile.Parse(stream);
var actualJson = bundle.ToJson();
var expectedJson = File.ReadAllText("fixture_expected.json");
// Compare actualJson with expectedJson
```

### CI Integration

Add to CI workflow to regenerate reference JSONs before tests.

**Note:** Pin UnityPy to a specific trusted version to mitigate supply-chain risks.

```yaml
- name: Generate reference JSONs
  run: |
    pip install UnityPy==1.10.14  # Pin to trusted version
    python scripts/generate_reference_json.py --all
```
