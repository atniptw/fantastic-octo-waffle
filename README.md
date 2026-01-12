# REPO Cosmetic Viewer

A static web app for browsing and rendering cosmetics from [REPO](https://thunderstore.io/c/repo/) game mods. Download a mod, parse its Unity AssetBundles (`.hhh` files) in-browser, and preview 3D cosmetics with three.js.

## Quick Start

```bash
git clone https://github.com/atniptw/fantastic-octo-waffle.git
cd fantastic-octo-waffle
pnpm install
pnpm dev
# Open http://localhost:5173
```

## Architecture

- **Frontend:** Preact + three.js (browser-based parsing & rendering)
- **Backend:** Cloudflare Worker (CORS proxy to Thunderstore API)
- **Parser:** UnityFS reader + LZ4/LZMA decompression (Web Worker)

See [docs/architecture.md](./docs/architecture.md) for detailed diagrams and data flow.

## Documentation

- [Development Setup](./docs/dev-guide.md)
- [Architecture Overview](./docs/architecture.md)
- [Worker API Spec](./docs/specs/worker-api.md)
- [Parser Spec](./docs/specs/parser-pipeline.md)
- [Testing Strategy](./docs/testing.md)
- [Roadmap](./docs/roadmap.md)
- [Architecture Decisions](./docs/adr/)

## Features (Roadmap)

| Phase | Status  | Features                                      |
| ----- | ------- | --------------------------------------------- |
| 1     | Planned | Worker proxy, mod list, search                |
| 2     | Planned | Zip download, UnityFS parsing, mesh rendering |
| 3     | Planned | Textures, materials, PBR rendering            |
| 4     | Planned | Caching, telemetry, error handling            |
| 5     | Planned | Multi-game support, mod inspector, UI polish  |

## Tech Stack

- **Build:** Vite + TypeScript
- **UI:** Preact + Preact Signals
- **3D:** three.js
- **Testing:** Vitest + Playwright
- **Compression:** lz4js + WASM LZMA
- **Zip:** fflate
- **Monorepo:** pnpm workspaces

## Development

```bash
# Start both servers
pnpm dev

# Run tests
pnpm test

# Build for production
pnpm build

# Deploy Worker to Cloudflare
cd apps/worker && wrangler deploy
```

See [docs/dev-guide.md](./docs/dev-guide.md) for full setup instructions.

## Contributing

We welcome contributions! Please:

1. Check [Roadmap](./docs/roadmap.md) for current priorities.
2. Open an issue or discussion before large changes.
3. Ensure all tests pass: `pnpm test`
4. Follow TypeScript strict mode and ESLint rules.

See [docs/dev-guide.md](./docs/dev-guide.md#contributing) for details.

## License

TBD (to be determined)

## Contact

- **Issues:** GitHub Issues for bugs and feature requests
- **Discussions:** GitHub Discussions for questions and ideas

---

**Status:** Early-stage development. Architecture and documentation in progress; code coming soon!
