# Contributing to fantastic-octo-waffle

Thank you for your interest in contributing! This guide explains our workflow and references.

## Philosophy

**Single Source of Truth**: All implementation details come from **validated code**, not documentation.

- **Reference Implementations**: [UnityPy](https://github.com/K0lb3/UnityPy) (Python) and [AssetStudio](https://github.com/Perfare/AssetStudio) (C#)
- **Validation**: Snapshot tests comparing C# parser output against UnityPy reference outputs
- **Documentation**: Only as guides—code and tests are authoritative

## Project Structure

```
fantastic-octo-waffle/
├── src/
│   ├── BlazorApp/              # Blazor WebAssembly frontend
│   └── UnityAssetParser/       # Core C# Unity asset parsing library
│       ├── Bundle/             # BundleFile, SerializedFile parsers
│       ├── Classes/            # Unity object classes (Mesh, etc.)
│       ├── Helpers/            # Parsing utilities
│       └── Services/           # High-level services (MeshExtraction, etc.)
├── Tests/
│   └── UnityAssetParser.Tests/
│       ├── Integration/        # Bundle parsing validation tests
│       ├── Fixtures/           # Test .hhh bundle files
│       └── SnapshotTestHelper.cs  # Snapshot testing utilities
├── scripts/
│   ├── generate_reference_json.py      # Generate UnityPy reference outputs
│   ├── dump_unitypy_object_tree.py     # Dump full object trees from UnityPy
│   ├── dump_csharp_object_tree.csx     # Dump full object trees from C#
│   └── compare_object_trees.py         # Compare UnityPy vs C# outputs
├── docs/                       # Design and implementation documentation
└── cloudflare-worker/          # CORS proxy for Thunderstore API
```

## Development Workflow

### 1. Setup

**Prerequisites:**
- .NET 10.0 SDK or later
- Python 3.8+ (for validation scripts)
- UnityPy library: `pip install UnityPy==1.10.14` (pinned for reproducibility)

**First-Time Setup:**
```bash
# Clone and restore dependencies
git clone https://github.com/atniptw/fantastic-octo-waffle.git
cd fantastic-octo-waffle
dotnet restore

# Download Bootstrap for Blazor UI
mkdir -p src/BlazorApp/wwwroot/lib/bootstrap/dist/css
curl -sL https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css \
  -o src/BlazorApp/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css

# Configure Worker URL for development
cp src/BlazorApp/wwwroot/appsettings.Development.json.example \
   src/BlazorApp/wwwroot/appsettings.Development.json
# Edit appsettings.Development.json with your Worker URL
```

### 2. Build and Run

**Run Blazor app locally:**
```bash
cd src/BlazorApp
dotnet run
# Access at http://localhost:5000/
```

**Build for production:**
```bash
cd src/BlazorApp
dotnet publish -c Release
# Output: dist/ folder
```

### 3. Testing

**Run all tests:**
```bash
dotnet test
```

**Run specific test project:**
```bash
dotnet test Tests/UnityAssetParser.Tests/
```

**Run with coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 4. Validation Against UnityPy

**Generate reference JSON for a bundle:**
```bash
python scripts/generate_reference_json.py path/to/bundle.hhh
# Output: path/to/bundle_expected.json
```

**Generate reference for all test fixtures:**
```bash
python scripts/generate_reference_json.py --all
# Processes all .hhh files in Tests/UnityAssetParser.Tests/Fixtures/
```

**Compare object trees (detailed validation):**
```bash
# Dump UnityPy tree
python scripts/dump_unitypy_object_tree.py bundle.hhh -o bundle_unitypy.json

# Dump C# tree
dotnet script scripts/dump_csharp_object_tree.csx bundle.hhh -o bundle_csharp.json

# Compare
python scripts/compare_object_trees.py bundle_unitypy.json bundle_csharp.json
```

### 5. Code Quality

**Format code:**
```bash
dotnet format
```

**Build with warnings as errors:**
```bash
dotnet build /warnaserror
```

## Implementation Guidelines

### Porting from UnityPy

When implementing Unity asset parsing logic:

1. **DO NOT** reverse-engineer Unity formats from scratch
2. **DO** port logic directly from UnityPy reference implementation
3. **DO** validate C# output against Python reference outputs (JSON diff)
4. **DO** handle alignment, external resources, and compression as per UnityPy

**Key UnityPy files to reference:**
- `UnityPy/files/BundleFile.py` → `UnityAssetParser/Bundle/BundleFile.cs`
- `UnityPy/classes/Mesh.py` → `UnityAssetParser/Classes/Mesh.cs`
- `UnityPy/helpers/PackedBitVector.py` → `UnityAssetParser/Helpers/PackedBitVector.cs`
- `UnityPy/helpers/MeshHelper.py` → `UnityAssetParser/Helpers/MeshHelper.cs`

### Critical Parsing Rules

- ✅ Maintain 4-byte alignment after byte arrays and bool triplets
- ✅ Handle external `.resS` resources (vertex data is NOT inline)
- ✅ Use `PackedBitVector` decompression for indices/attributes
- ❌ Don't skip alignment (causes data corruption)
- ❌ Don't treat `m_DataSize` as a length field (it's raw binary data)

### Testing Requirements

Every new feature or parser enhancement MUST:

1. Add unit tests for low-level parsing logic
2. Add integration tests with real `.hhh` bundle fixtures
3. Generate and validate against UnityPy reference outputs
4. Pass snapshot tests comparing C# vs Python outputs

**Example test workflow:**
```csharp
// Integration test
var bundle = BundleFile.Parse(stream);
var actualJson = bundle.ToJson();
var expectedJson = File.ReadAllText("fixture_expected.json");
// Compare actualJson with expectedJson
```

## Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <summary up to 72 chars>

<body explaining what and why, wrapped at ~72 cols>

<optional footers: Closes #123 | BREAKING CHANGE: ...>
```

**Types:** feat, fix, docs, test, build, ci, refactor, perf, style, chore, revert  
**Scopes:** blazor, worker, parser, tests, docs, ci

**Examples:**
- `feat(parser): add Mesh StreamingInfo .resS resolution`
- `fix(blazor): handle CORS errors from Worker proxy gracefully`
- `test: add snapshot tests for Mesh parsing`
- `docs: update CONTRIBUTING.md with UnityPy references`

See `.github/prompts/commit.prompt.md` for detailed guidelines.

## CI/CD Pipeline

Pull requests are automatically validated:

- 🔒 Secret scanning (blocks on detection)
- 📦 Bundle size monitoring (WASM performance gates)
- 🧮 Cyclomatic complexity limits (AI-maintainable code)
- 📊 Code coverage tracking (70% minimum)
- 🔨 Multi-configuration builds
- 🎨 Automated linting and formatting

See `.github/workflows/pr-checks.yml` for details.

## Reference Implementations

### UnityPy (Python)
- **Repository**: https://github.com/K0lb3/UnityPy
- **Purpose**: Primary reference for Unity asset parsing algorithms
- **Usage**: Generate validation outputs, compare object trees

### AssetStudio (C#)
- **Repository**: https://github.com/Perfare/AssetStudio
- **Purpose**: Secondary reference for C# implementation patterns
- **Usage**: Clarify edge cases and binary format details

### Thunderstore API
- **Documentation**: https://new.thunderstore.io/api/docs
- **Community**: https://new.thunderstore.io/c/repo/ (slug: `repo`)
- **Purpose**: Package metadata, downloads, categories

## Documentation

- **Project Overview**: `README.md`
- **Scripts Reference**: `scripts/README.md`
- **Design Docs**: `docs/` directory (for context, NOT as implementation specs)
  - `Architecture.md` — System overview, data flow
  - `UnityParsing.md` — Bundle structure, Mesh parsing
  - `BlazorUI.md` — UI components, routing, services
  - `TestingStrategy.md` — Testing approach and fixtures

**Note**: Documentation provides context, but **code and tests are authoritative**. When in doubt, refer to UnityPy source code and snapshot test outputs.

## Getting Help

- Open an issue for bugs or feature requests
- Check existing issues and pull requests
- Review `docs/` for design context (but validate with code)

## License

This project is licensed under the MIT License. See the LICENSE file for details.
