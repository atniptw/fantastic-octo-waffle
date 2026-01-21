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

2. **Start the development server:**
   ```bash
   npm run dev
   ```

   The worker will be available at `http://localhost:8787`

3. **Test the endpoint:**
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

## Development Notes

- **Port**: Default is 8787 (Wrangler standard)
- **Hot reload**: Enabled by default in dev mode
- **Configuration**: See `wrangler.toml` for worker settings

## Project Status

Currently in initial setup phase. This is a basic Hello World handler to verify tooling works correctly.

Future features will include:
- CORS handling
- API routing
- Thunderstore proxy endpoints
- KV caching
