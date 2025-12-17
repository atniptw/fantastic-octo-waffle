# R.E.P.O. Cosmetic Catalog

A browser-based web application that lets users upload Thunderstore mod ZIP files for R.E.P.O., extract cosmetic metadata from `.hhh` Unity asset bundles, and preview cosmetics with 3D rendering and GIF generation - all running in your browser.

## Features

- 📦 **Upload mod ZIP files** directly in your browser
- 🔍 **Extract cosmetic metadata** from `.hhh` Unity asset bundles
- 💾 **Store locally** in IndexedDB (privacy-friendly, no server needed)
- 🔎 **Search and filter** cosmetics catalog
- 🎨 **3D preview rendering** using Three.js/WebGL
- 🖼️ **Generate preview images** (PNG/WebP)
- 🎬 **Create animated GIFs** of cosmetics
- 📱 **Works on any device** with a modern browser
- 🌐 **Hosted on GitHub Pages** - no installation required

## Technology Stack

- **Framework**: React + TypeScript
- **Build Tool**: Vite
- **ZIP Handling**: JSZip (client-side)
- **Storage**: IndexedDB
- **3D Rendering**: Three.js
- **Deployment**: GitHub Pages
- **Linting**: ESLint

## Live Demo

🚀 **[Try it now on GitHub Pages](https://atniptw.github.io/fantastic-octo-waffle/)**

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
│   ├── components/         # React UI components
│   │   ├── FileUpload.tsx  # ZIP file upload
│   │   ├── CatalogView.tsx # Cosmetics catalog
│   │   └── PreviewViewer.tsx # 3D preview viewer
│   ├── lib/                # Core libraries
│   │   ├── zipScanner.ts   # ZIP extraction logic
│   │   ├── indexedDB.ts    # IndexedDB wrapper
│   │   ├── unityParser.ts  # UnityFS .hhh parser
│   │   └── previewGenerator.ts # Image/GIF generation
│   ├── App.tsx             # Main React component
│   ├── main.tsx            # React entry point
│   └── index.html          # HTML entry point
├── public/                 # Static assets
├── dist/                   # Build output (GitHub Pages)
├── package.json
├── tsconfig.json           # TypeScript configuration
├── vite.config.ts          # Vite configuration
└── eslint.config.js        # ESLint configuration
```

## How It Works

1. **Upload**: Select mod ZIP files using the file picker or drag-and-drop
2. **Extract**: JSZip extracts files in-browser using Web Workers
3. **Scan**: App scans for `manifest.json`, `icon.png`, and `.hhh` cosmetic files
4. **Parse**: UnityFS parser extracts meshes and textures from `.hhh` bundles
5. **Store**: Metadata and assets stored in IndexedDB (local to your browser)
6. **Preview**: Three.js renders 3D cosmetics with interactive controls
7. **Export**: Generate and download preview images or animated GIFs

## Browser Compatibility

- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Edge 90+
- ✅ Safari 14+

Requires modern browser with IndexedDB, Web Workers, and WebGL support.

## License

ISC
