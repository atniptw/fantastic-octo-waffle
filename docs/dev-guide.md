# Developer Setup Guide

This guide covers local development setup for the REPO cosmetic viewer project.

## Prerequisites

- **Node.js:** 24.x LTS (v24.11.1 or later).
- **pnpm:** `npm install -g pnpm` (or use Corepack: `corepack enable`).
- **Git:** For version control.
- **Cloudflare account (optional):** For deploying Worker to production; local dev works without one.

## Project Structure

```
fantastic-octo-waffle/
├── apps/
│   ├── web/                    # Vite + Preact web app
│   │   ├── src/
│   │   ├── public/
│   │   └── vite.config.ts
│   └── worker/                 # Cloudflare Worker
│       ├── src/
│       ├── wrangler.toml
│       └── __tests__/
├── packages/
│   ├── unity-ab-parser/        # UnityFS parsing + decompression
│   ├── thunderstore-client/    # Typed API client
│   ├── renderer-three/         # three.js scene assembly
│   └── utils/                  # Shared types, logging
├── docs/
│   ├── README.md
│   ├── architecture.md
│   ├── specs/
│   ├── adr/
│   └── testing.md
├── pnpm-workspace.yaml
├── package.json
└── tsconfig.json
```

## Installation

```bash
# Clone repo
git clone https://github.com/atniptw/fantastic-octo-waffle.git
cd fantastic-octo-waffle

# Install dependencies
pnpm install
```

## Development Servers

### Web App (Vite)

```bash
cd apps/web
pnpm dev
# Opens http://localhost:5173
```

Hot module reload enabled; changes to Preact components reflect instantly.

### Worker (Miniflare)

```bash
cd apps/worker
pnpm dev
# Runs on http://localhost:8787
```

Miniflare emulates Cloudflare Workers locally with full environment parity.

### Both Simultaneously (Recommended)

```bash
# From repo root
pnpm dev
# Runs both in parallel; web app proxies to worker at localhost:8787
```

**Web app must be configured to point to Worker:**

- `.env.development` or `vite.config.ts`:
  ```typescript
  const API_URL =
    import.meta.env.MODE === 'development'
      ? 'http://localhost:8787'
      : 'https://worker.your-domain.workers.dev';
  ```

## Building

### Web App (Production Build)

```bash
cd apps/web
pnpm build
# Output: dist/
pnpm preview     # Test production build locally
```

### Worker (Bundle for Cloudflare)

```bash
cd apps/worker
pnpm build
# Output: dist/ (compatible with wrangler deploy)
```

## Testing

```bash
# Run all tests
pnpm test

# Unit tests only
pnpm test:unit

# Integration tests
pnpm test:integration

# E2E tests (Playwright)
pnpm test:e2e

# Watch mode (re-run on file changes)
pnpm test:watch

# Update golden files (after intentional parser changes)
pnpm test:update-goldens

# Coverage report
pnpm test:cov
# Open coverage/index.html in browser
```

## Linting & Formatting

```bash
# Lint (ESLint)
pnpm lint

# Format (Prettier)
pnpm format

# Fix linting errors automatically
pnpm lint:fix
```

## Debugging

### Web App

1. Open DevTools (F12 or Cmd+Opt+I).
2. **Sources tab:** Set breakpoints in TypeScript source.
3. **Console:** Inspect `window.APP_DEBUG` for internal state.
4. **Network tab:** Monitor Worker requests; check response headers and payloads.

### Worker

1. **Local logs:** `console.log()` in Worker source appears in terminal.
2. **Wrangler tail:** `wrangler tail` streams live logs from deployed Worker.
3. **Request inspection:** Miniflare echoes request/response in terminal.

### Parser (Web Worker)

1. **Worker debugger:** Chrome DevTools → Sources → Worker threads listed.
2. **Message logging:** Post debug info back to main thread:
   ```typescript
   postMessage({ debug: { phase: 'decompressing', progress: 0.5 } });
   ```
3. **Error capture:** Wrap parser in try-catch; return detailed error object.

## Environment Variables

### Web App

**`.env.development`:**

```
VITE_API_URL=http://localhost:8787
VITE_LOG_LEVEL=debug
```

**`.env.production`:**

```
VITE_API_URL=https://worker.your-domain.workers.dev
VITE_LOG_LEVEL=warn
```

### Worker

**`wrangler.toml`:**

```toml
[env.development]
vars = { LOG_LEVEL = "debug" }

[env.production]
vars = { LOG_LEVEL = "warn", ALLOWED_HOSTS = [...] }
```

## Deploying

### Worker to Cloudflare

```bash
# Set up Cloudflare account & associate project
cd apps/worker
wrangler login
wrangler deploy
# Output: https://your-project.your-name.workers.dev
```

### Web App to GitHub Pages

1. Build: `cd apps/web && pnpm build`
2. Push `dist/` to GitHub Pages:

   ```bash
   # Option A: Deploy via GitHub Actions (automated)
   # Configure .github/workflows/deploy.yml to run pnpm build && deploy to gh-pages branch

   # Option B: Manual deploy
   git checkout -b gh-pages
   rm -rf dist-old && mv dist dist-old
   git add dist-old && git commit -m "deploy: static build"
   git push origin gh-pages
   ```

3. Enable GitHub Pages in repo settings:
   - Settings → Pages → Build and deployment
   - Source: Deploy from a branch
   - Branch: `gh-pages` / root
4. Site will be available at `https://atniptw.github.io/fantastic-octo-waffle/`
5. Configure SPA routing in `dist/.nojekyll` (prevents Jekyll processing) and ensure 404 redirects to `index.html`.

## Troubleshooting

### "Cannot find module" errors

```bash
# Reinstall dependencies
pnpm install

# Clean cache
pnpm store prune
pnpm install --frozen-lockfile
```

### Worker not responding locally

- Ensure Miniflare is running: `cd apps/worker && pnpm dev`
- Check `VITE_API_URL` in web app `.env.development`
- Restart both servers: Ctrl+C, then `pnpm dev` again

### Parser tests failing

- Check if test fixtures exist: `packages/unity-ab-parser/fixtures/`
- Update golden files: `pnpm test:update-goldens`
- Review error in test output for clues (e.g., unsupported texture format)

### Memory issues during parsing

- Parser runs in Web Worker; if memory peaks, browser will GC.
- Check telemetry logs: `window.APP_DEBUG.telemetry`
- Consider smaller test bundles first

## Contributing

1. Create a branch: `git checkout -b feature/description`
2. Make changes; ensure tests pass: `pnpm test`
3. Commit: `git commit -m "feat: add feature"`
4. Push: `git push origin feature/description`
5. Open PR; reference related issues.

See [CONTRIBUTING.md](./CONTRIBUTING.md) (to be added) for coding standards.

## Useful Commands Summary

| Task              | Command                             |
| ----------------- | ----------------------------------- |
| Install deps      | `pnpm install`                      |
| Start dev servers | `pnpm dev`                          |
| Run tests         | `pnpm test`                         |
| Build for prod    | `pnpm build`                        |
| Lint code         | `pnpm lint`                         |
| Format code       | `pnpm format`                       |
| Deploy Worker     | `cd apps/worker && wrangler deploy` |
| Export logs       | `cd apps/worker && wrangler tail`   |

## Resources

- [Docs Overview](./docs/README.md)
- [Architecture](./docs/architecture.md)
- [Worker API Spec](./docs/specs/worker-api.md)
- [Parser Pipeline](./docs/specs/parser-pipeline.md)
- [Testing Guide](./docs/testing.md)
- [Roadmap](./docs/roadmap.md)

## Support

- **Issues:** GitHub Issues for bugs and feature requests.
- **Discussions:** GitHub Discussions for architecture questions.
- **Discord:** (to be configured) for real-time chat.
