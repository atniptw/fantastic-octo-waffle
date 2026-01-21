# Cloudflare Worker - R.E.P.O. Mod Viewer API

API proxy for the R.E.P.O. Mod Viewer Blazor application.

## Quick Start

### Prerequisites
- Node.js 20 LTS or higher
- npm (comes with Node.js)

### Local Development

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Run tests:**
   ```bash
   npm test
   ```

3. **Start the development server:**
   ```bash
   npm run dev
   ```

   The worker will be available at `http://localhost:8787`

4. **Test the endpoint:**
   ```bash
   # Basic test
   curl http://localhost:8787

   # Check headers
   curl -i http://localhost:8787/test
   ```

   Expected response:
   ```json
   {"message":"Hello World","status":"ok"}
   ```

### Available Scripts

- `npm run dev` - Start local development server (default port: 8787)
- `npm run deploy` - Deploy to Cloudflare Workers (requires authentication)
- `npm test` - Run all tests
- `npm run test:watch` - Run tests in watch mode
- `npm run test:coverage` - Generate coverage report

## Core Utilities

### `corsHeaders(env)`
Returns CORS headers for Worker responses. Uses `*` for dev mode (no `SITE_ORIGIN` set) or specific origin for production.

### `handlePreflight(env)`
Handles OPTIONS preflight requests, returning 204 with CORS headers.

### `jsonError(message, status, env)`
Creates standardized JSON error responses with CORS headers.

### `validateUpstreamUrl(urlString)`
Validates URLs against Thunderstore allowlist (`thunderstore.io`, `gcdn.thunderstore.io`).

## Testing

**Current Coverage**: 100% lines, 100% functions, 100% branches, 100% statements

Tests run using Vitest with Node.js's built-in Web Standard APIs (Response, Headers, URL), which are identical to the APIs available in Cloudflare Workers runtime.

## Development Notes

- **Port**: Default is 8787 (Wrangler standard)
- **Hot reload**: Enabled by default in dev mode
- **Configuration**: See `wrangler.toml` for worker settings
- **All utility functions are pure functions** with no side effects

## Project Status

Core CORS & error utilities completed (Issue #111). 

Next steps:
- API routing implementation
- Thunderstore proxy endpoints
- KV caching
