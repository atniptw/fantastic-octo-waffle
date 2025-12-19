# Thunderstore Proxy (Cloudflare Worker)

A Cloudflare Worker that proxies all Thunderstore API requests to bypass CORS restrictions and enable use of experimental endpoints.

## Features

- ✅ **CORS-enabled**: All responses include CORS headers for browser access
- ✅ **Universal proxy**: Supports V1 API, experimental API, and download endpoints
- ✅ **Cached**: GET requests cached for 1 hour for performance
- ✅ **Free tier**: 100,000 requests/day included
- ✅ **Global**: Deployed on Cloudflare's edge network worldwide

## Setup

### 1. Install Wrangler

```bash
npm install -g wrangler
```

### 2. Login to Cloudflare

```bash
wrangler login
```

### 3. Deploy

```bash
cd workers
wrangler deploy
```

You'll get a URL like: `https://thunderstore-proxy-abc123.workers.dev`

## Configuration

### In ModDetail.tsx

Replace the hardcoded proxy URL:

```tsx
// OLD: hardcoded cors-anywhere
const corsProxyUrl = `https://cors-anywhere.herokuapp.com/${downloadUrl}`;

// NEW: use your worker
const corsProxyUrl = `https://YOUR_WORKER_URL/${downloadUrl}`;
```

### In ThunderstoreClient

Update the base URL to use your worker:

```typescript
const client = new ThunderstoreClient({
  baseUrl: 'https://YOUR_WORKER_URL',
});
```

## API Routes

The worker transparently proxies all paths:

- `GET /api/v1/package/` - V1 package listing
- `GET /api/experimental/package/{namespace}/{name}/` - Full package details
- `GET /package/download/{namespace}/{name}/` - Download mod ZIP
- `GET /api/v1/package/` - Queries with parameters

## Example Requests

```bash
# List all packages
curl https://your-worker.workers.dev/api/v1/package/

# Get full package details
curl https://your-worker.workers.dev/api/experimental/package/BepInEx/BepInExPack/

# Download a mod
curl https://your-worker.workers.dev/package/download/Jdure-DisturbedPaintings/DisturbedPaintings/ \
  -o mod.zip
```

## Environment Variables

You can add environment variables in `wrangler.toml` if needed:

```toml
[env.production]
vars = { THUNDERSTORE_BASE_URL = "https://thunderstore.io" }
```

Then use in code:

```typescript
const THUNDERSTORE_BASE = env.THUNDERSTORE_BASE_URL;
```

## Monitoring

View logs and analytics in the [Cloudflare Dashboard](https://dash.cloudflare.com)

## Limitations

- File size: Max 128 MB per request (Cloudflare limit)
- Rate limiting: Implement on your side if needed
- Free tier: 100,000 requests/day (plenty for this use case)

## Troubleshooting

### "Worker script exceeded size limit"
- Minify any bundled code
- Keep it simple (proxy-only, no complex logic)

### "CORS still not working"
- Check the browser console for the actual error
- Verify the worker is deployed (`wrangler publish`)
- Test directly: `curl -I https://your-worker.workers.dev/api/v1/package/`

### Cache issues
- Add `?bust={timestamp}` to bypass cache
- Or clear cache in Cloudflare Dashboard
