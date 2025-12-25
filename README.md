# R.E.P.O. Cosmetic Catalog

A browser-based web application that fetches R.E.P.O. mods from Thunderstore, extracts cosmetic metadata from `.hhh` Unity asset bundles, and previews cosmetics with 3D rendering and GIF generation - all running in your browser.

## Features

- ⚡ **Browser-side caching** - ZIPs cached in IndexedDB for instant re-access
- 📊 **Download progress tracking** - Real-time progress display during mod downloads
- 💾 **Offline support** - Access previously downloaded mods without internet

## Technology Stack

- **Framework**: React + TypeScript
- **Build Tool**: Vite
- **ZIP Handling**: JSZip (client-side)
- **Storage**: IndexedDB
- **3D Rendering**: Three.js
- **Deployment**: GitHub Pages
- **Linting**: ESLint

## Live Site

🚀 **[Open the site on GitHub Pages](https://atniptw.github.io/fantastic-octo-waffle/)**

## Prerequisites

- Node.js 18+
- npm 8+

## Setup

```bash
# Install dependencies
npm install
```

## Development

```bash
# Start development server with hot reload
npm run dev
```

Open [http://localhost:5173](http://localhost:5173) in your browser.

## Build

```bash
# Build for production (outputs to dist/)
npm run build

# Preview production build locally
npm run preview
```

## Deploy to GitHub Pages

### Automatic Deployment (Recommended)
The application automatically deploys to GitHub Pages when changes are pushed to the `main` branch via GitHub Actions. The live site will be available at:
- https://atniptw.github.io/fantastic-octo-waffle/

### Manual Deployment
You can also manually deploy using:
```bash
# Build and deploy to gh-pages branch
npm run deploy
```

## Linting & Formatting

```bash
# Run ESLint
npm run lint

# Auto-fix ESLint issues
npm run lint:fix

# Format code with Prettier
npm run format
```

## Project Structure

```
├── src/
│   ├── App.tsx                 # Main React component
│   ├── main.tsx                # React entry point
│   ├── index.html              # HTML entry point
│   ├── styles.css              # Global styles (dark theme)
│   ├── config.ts               # App configuration (proxy base URL)
│   ├── renderer/
│   │   └── components/         # UI components (master-detail layout)
│   │       ├── AppLayout.tsx
│   │       ├── Header.tsx
│   │       ├── ModList.tsx
│   │       ├── ModListItem.tsx
│   │       ├── ModDetail.tsx
│   │       ├── CatalogView.tsx
│   ├── lib/
│   │   ├── thunderstore/       # Thunderstore API client
│   │   │   ├── client.ts
│   │   │   ├── types.ts
│   │   │   ├── cosmetic-filter.ts
│   │   │   └── index.ts
│   │   ├── useZipScanner.ts    # Web Worker-based ZIP scanning hook
│   │   └── zipScanner.ts       # ZIP scanning utilities
│   └── workers/
│       └── zipWorker.ts        # Worker entry for ZIP processing
├── workers/                    # Cloudflare Worker proxy (Thunderstore → Browser)
│   ├── src/index.ts
│   └── wrangler.toml
├── dist/                       # Build output (GitHub Pages)
├── package.json
├── tsconfig.json               # TypeScript configuration
├── vite.config.ts              # Vite configuration
└── eslint.config.js            # ESLint configuration
```

## Configuration

- Set Thunderstore proxy base URL via environment variable:

```bash
export VITE_THUNDERSTORE_PROXY_URL="https://<your-worker-subdomain>.workers.dev"
```

If unset, the app will attempt direct Thunderstore requests (may be blocked by CORS).

## How It Works

1. **Analyze**: Click "Analyze Mod" to download the mod ZIP via the proxy
2. **Extract**: ZIP is processed in-browser using a Web Worker
3. **Scan**: Files are scanned for `manifest.json`, `icon.png`, and `plugins/*/Decorations/*.hhh`
4. **Parse (Level 2)**: UnityFS `.hhh` bundles are parsed to extract meshes/textures
5. **Preview (Level 2)**: Three.js renders 3D previews; images/GIFs can be generated

## Browser Compatibility

- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Edge 90+
- ✅ Safari 14+

Requires modern browser with IndexedDB, Web Workers, and WebGL support.

## License

ISC

## Contributing & Community

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, guidelines, and the PR process.

Community health files:
- Code of Conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Security Policy: [SECURITY.md](SECURITY.md)
- Support: [SUPPORT.md](SUPPORT.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)

Note: This project is volunteer‑maintained. Response times and outcomes may vary and are not guaranteed.
