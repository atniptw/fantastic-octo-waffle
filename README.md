# fantastic-octo-waffle

[![PR Checks](https://github.com/atniptw/fantastic-octo-waffle/actions/workflows/pr-checks.yml/badge.svg)](https://github.com/atniptw/fantastic-octo-waffle/actions/workflows/pr-checks.yml)
[![codecov](https://codecov.io/gh/atniptw/fantastic-octo-waffle/branch/main/graph/badge.svg)](https://codecov.io/gh/atniptw/fantastic-octo-waffle)

A project to showcase browser-first cosmetic viewing for mods.

## Getting Started

### First-Time Setup

1. **Download Bootstrap** (required for styling)
   ```bash
   mkdir -p src/BlazorApp/wwwroot/lib/bootstrap/dist/css
   curl -sL https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css \
     -o src/BlazorApp/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css
   ```

2. **Configure Worker URL** (for Codespaces or local development)
   ```bash
   cp src/BlazorApp/wwwroot/appsettings.Development.json.example \
      src/BlazorApp/wwwroot/appsettings.Development.json
   ```
   Then edit `appsettings.Development.json` and replace the placeholder with your Worker URL:
   - For GitHub Codespaces: `https://your-codespace-name-unique-hash.app.github.dev`
   - For local development: `http://localhost:8787` (or your Worker port)
   
   **Note:** `appsettings.Development.json` is gitignored and should not be committed.

### Running Locally

```bash
cd src/BlazorApp
dotnet run
```

Access the app at `http://localhost:5000/`

### Building for Production

```bash
cd src/BlazorApp
dotnet publish -c Release
```

Output: `dist/` folder (configured for GitHub Pages)

**Custom Deployment:**
```bash
# Custom base path
dotnet publish -c Release /p:StaticWebAssetBasePath=/custom-path

# Custom output directory
dotnet publish -c Release /p:PublishDir=/var/www/app
```

### Documentation

For detailed documentation, see the `docs/` directory.

- [UnityFS (.hhh) → glTF/GLB → Three.js (Blazor WASM) plan](docs/UnityBundleToGltfPlan.md)

### Code Quality

This project enforces strict code quality standards through automated CI checks:
- 🔒 Secret scanning (blocks on detection)
- 📦 Bundle size monitoring (WASM performance gates)
- 🧮 Cyclomatic complexity limits (AI-maintainable code)
- 📊 Code coverage tracking (70% minimum)
- 🔨 Multi-configuration builds
- 🎨 Automated linting and formatting

See [`.github/workflows/README.md`](.github/workflows/README.md) for details.

## License

Licensed under the MIT License. See the LICENSE file for details.
