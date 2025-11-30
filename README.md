# R.E.P.O. Cosmetic Catalog

A Windows desktop application that scans Thunderstore mod ZIP files for R.E.P.O., extracts cosmetic metadata, stores them in SQLite, and provides a searchable UI.

## Features

- Import Thunderstore mod ZIP files
- Scan for cosmetic files (`.hhh` Unity asset bundles)
- Store metadata in SQLite database
- Searchable catalog UI

## Technology Stack

- **Framework**: Electron
- **Frontend**: React + TypeScript
- **Build Tool**: Vite
- **Linting**: ESLint + Prettier

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
# Run the development server (renderer only)
npm run dev:renderer

# Build main process (required before running Electron)
npm run build:main

# Start Electron app (after building)
npm run start
```

## Build

```bash
# Build both main and renderer
npm run build

# Build only the main process
npm run build:main

# Build only the renderer
npm run build:renderer
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
│   ├── main/           # Electron main process
│   │   ├── main.ts     # Main entry point
│   │   └── preload.ts  # Preload script for IPC
│   └── renderer/       # React frontend
│       ├── App.tsx     # Main React component
│       ├── main.tsx    # React entry point
│       ├── styles.css  # Global styles
│       └── components/ # React components
│           └── ImportButton.tsx
├── dist/               # Build output
├── package.json
├── tsconfig.json       # TypeScript config for renderer
├── tsconfig.main.json  # TypeScript config for main process
├── vite.config.ts      # Vite configuration
└── .eslintrc.json      # ESLint configuration
```

## License

ISC
