# Dev Container for R.E.P.O. Mod Browser

This directory contains the development container configuration for the R.E.P.O. Mod Browser project.

## Quick Start

### Using VS Code Remote Containers

1. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Open the project in VS Code
3. Click the **Remote Containers** icon (bottom-left corner) → **Reopen in Container**
4. Wait for the container to build and start (~2-3 minutes on first run)

### Using Docker Compose (Manual)

```bash
docker compose up -d
docker exec -it repo-mod-browser bash
```

## What's Installed

- **.NET 10.0 SDK** with Blazor WebAssembly workload
- **Node.js 20** with npm (for Cloudflare Workers, vitest)
- **Python 3.11** with UnityPy and validation tools
- **VS Code Extensions**: C#, TypeScript, Python, ESLint, Prettier, Test Explorer
- **Cloudflare Wrangler CLI** for worker development

## Environment Details

### Directories

```
/workspace                 # Project root
  /src/BlazorApp          # Blazor WASM app (.NET 10.0)
  /cloudflare-worker      # Worker proxy (Node.js)
  /Tests                  # Unit & E2E tests
  /scripts                # Validation scripts (Python)
```

### Port Forwarding

| Port | Service | URL |
|------|---------|-----|
| 5000 | Blazor (HTTP) | http://localhost:5000 |
| 5001 | Blazor (HTTPS) | https://localhost:5001 |
| 8787 | Wrangler Dev | http://localhost:8787 |

### Environment Variables

- `DOTNET_CLI_TELEMETRY_OPTOUT=1` — Disable .NET telemetry
- `DOTNET_TRY_CLI_TELEMETRY_OPTOUT=1` — Disable interactive telemetry

## Common Commands

### Blazor Development

```bash
cd src/BlazorApp
dotnet watch run                        # Hot reload dev server
dotnet build                            # Debug build
dotnet build -c Release                 # Release build
dotnet publish -c Release               # Publish for GitHub Pages
```

### Testing

```bash
dotnet test                             # Run all tests
dotnet test Tests/UnityAssetParser.Tests/
dotnet test --collect:"XPlat Code Coverage"
```

### Cloudflare Worker

```bash
cd cloudflare-worker
wrangler dev                            # Local dev server
npm test                                # Run vitest suite
wrangler deploy                         # Deploy to Cloudflare
```

### Python Validation

```bash
cd scripts
python generate_reference_json.py       # Generate UnityPy reference
python compare_object_trees.py          # Validate C# vs Python parsing
```

## Rebuilding the Container

If you update `devcontainer.json` or `post-create.sh`:

```bash
# VS Code Remote Containers:
Ctrl+Shift+P → Remote Containers: Rebuild Container

# Docker CLI:
docker compose build --no-cache
docker compose up -d
```

## Troubleshooting

### Container won't start

```bash
docker system prune -a    # Remove dangling images
docker compose down -v    # Remove volumes
docker compose up -d      # Rebuild
```

### .NET workload installation fails

```bash
dotnet workload repair
dotnet workload install wasm-tools
```

### npm modules not found

```bash
cd cloudflare-worker && npm ci && cd ..
```

### Python dependencies missing

```bash
pip install --user UnityPy pyyaml jsonschema
```

## Notes

- SSH keys are mounted from `~/.ssh` for GitHub access
- The container runs as user `vscode` with sudo privileges
- VS Code extensions auto-install on first connect
- Code formatting (C#, JS, JSON) runs on save
