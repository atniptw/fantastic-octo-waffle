# Deployment Architecture

## Overview

This document describes how to deploy the R.E.P.O. Mod Browser to GitHub Pages and configure the Cloudflare Worker proxy.

## GitHub Pages Deployment

### Automated Deployment (Recommended)

Every push to the `main` branch automatically triggers a GitHub Actions workflow that:

1. ✅ Builds the Blazor WebAssembly app in Release mode
2. ✅ Publishes to `dist/wwwroot/` with AOT compilation and trimming
3. ✅ Configures base path for GitHub Pages (`/fantastic-octo-waffle/`)
4. ✅ Deploys to GitHub Pages environment

**Workflow File**: `.github/workflows/deploy-pages.yml`

**Live URL**: https://atniptw.github.io/fantastic-octo-waffle/

### Manual Deployment

If you need to deploy manually from your local machine:

```bash
# 1. Build and publish the Blazor app
cd src/BlazorApp
dotnet publish -c Release -o ../../dist

# 2. Add .nojekyll file (prevents Jekyll processing)
touch ../../dist/wwwroot/.nojekyll

# 3. Update base href in index.html
sed -i 's|<base href="/" />|<base href="/fantastic-octo-waffle/" />|g' ../../dist/wwwroot/index.html

# 4. Deploy to GitHub Pages branch (manual method)
cd ../../dist/wwwroot
git init
git add -A
git commit -m "Deploy to GitHub Pages"
git push -f git@github.com:atniptw/fantastic-octo-waffle.git main:gh-pages
```

**Note**: The automated workflow is preferred. Manual deployment should only be used for testing or troubleshooting.

### Configuration

#### Project Configuration
The `src/BlazorApp/BlazorApp.csproj` file contains deployment-specific settings:

```xml
<PropertyGroup>
  <!-- Deployment Configuration -->
  <PublishDir Condition="'$(Configuration)' == 'Release'">../../dist-Release/</PublishDir>
  <StaticWebAssetBasePath Condition="'$(Configuration)' == 'Release'">/fantastic-octo-waffle</StaticWebAssetBasePath>
  
  <!-- Optimization Settings (Release only) -->
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
  <RunAOTCompilation>true</RunAOTCompilation>
</PropertyGroup>
```

#### GitHub Pages Setup

In your GitHub repository settings:

1. Navigate to **Settings** → **Pages**
2. Under **Build and deployment**:
   - Source: **GitHub Actions**
3. The workflow will automatically deploy on every push to `main`

### Service Worker and Caching

The Blazor PWA template includes a service worker (`service-worker.js`) that provides:

- **Offline Support**: Cache static assets for offline access
- **Fast Loading**: Serve cached resources immediately
- **Cache-Busting**: Automatically updates when new versions are deployed

The service worker is automatically registered in `index.html` and managed by the Blazor framework.

## Cloudflare Worker Deployment

### Purpose

The Cloudflare Worker acts as a CORS proxy and cache layer for:
- Thunderstore API requests
- Mod package downloads
- API response caching using Cloudflare KV

### Configuration Files

- **Worker Source**: `cloudflare-worker/src/index.js`
- **Wrangler Config**: `cloudflare-worker/wrangler.toml`

### Deployment Steps

```bash
# 1. Navigate to worker directory
cd cloudflare-worker

# 2. Install dependencies
npm install

# 3. Login to Cloudflare (first time only)
npx wrangler login

# 4. Create KV namespaces (first time only)
npx wrangler kv:namespace create "CACHE"
npx wrangler kv:namespace create "CACHE" --preview

# 5. Update wrangler.toml with KV namespace IDs

# 6. Deploy to Cloudflare
npx wrangler deploy
```

### KV Namespace Configuration

Update `wrangler.toml` with your KV namespace IDs:

```toml
[[kv_namespaces]]
binding = "CACHE"
id = "your-production-namespace-id"
preview_id = "your-preview-namespace-id"
```

### Worker Routes

The worker is configured to handle:
- `/api/packages` → Proxies Thunderstore package list
- `/api/categories` → Proxies Thunderstore categories
- `/api/package/{namespace}/{name}/{version}/(readme|changelog)` → Package metadata
- `/api/download/{namespace}/{name}/{version}` → Package download metadata

### CORS Configuration

For development, the worker allows all origins (`Access-Control-Allow-Origin: *`).

For production, restrict to your GitHub Pages domain:

```javascript
headers: {
  'Access-Control-Allow-Origin': 'https://atniptw.github.io',
  // ... other headers
}
```

### Caching Strategy

- **Package List**: Cloudflare KV, TTL 5 minutes, key `repo:package_list`
- **Categories**: Cloudflare KV, TTL 1 hour, key `repo:categories`
- **Parsed Assets**: Browser IndexedDB (client-side), key `{mod_full_name}:{asset_filename}`

### Monitoring

Monitor worker performance and errors in the Cloudflare Dashboard:
- **Analytics** → View request counts, errors, and latency
- **Logs** → Real-time logs via `wrangler tail`

```bash
# Stream live logs
npx wrangler tail
```

## CDN & Static Assets

### Three.js Library

**Option 1: Use CDN (Recommended)**
```html
<script type="module">
  import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js';
</script>
```

**Option 2: Bundle with app**
```bash
cd src/BlazorApp/wwwroot/js
npm install three
# Import in your JS modules
```

### Cache-Busting Strategy

Blazor automatically handles cache-busting for static assets using content hashes in filenames:
- `app.{hash}.css`
- `app.{hash}.js`
- `blazor.webassembly.js`

**Manual Cache-Busting**: If you add custom static assets, use versioned filenames or query parameters:
```html
<script src="meshRenderer.js?v=1.0.0"></script>
```

## Rollback Procedure

If a deployment introduces issues:

### Via GitHub Actions
1. Navigate to **Actions** → **Deploy to GitHub Pages**
2. Find the last working deployment run
3. Click **Re-run jobs** → **Re-run all jobs**

### Via Git
```bash
# Find the last working commit
git log --oneline main

# Revert to specific commit
git revert <commit-sha>
git push origin main

# Workflow will automatically redeploy
```

## Performance Optimization

### Blazor WASM Optimizations
- ✅ **AOT Compilation**: Enabled in Release builds (`RunAOTCompilation`)
- ✅ **IL Trimming**: Removes unused code (`PublishTrimmed`, `TrimMode=full`)
- ✅ **Compression**: GitHub Pages serves files with gzip/brotli

### Expected Bundle Sizes
- **blazor.boot.json**: ~10 KB
- **dotnet.wasm**: ~2-3 MB (with AOT)
- **Total initial download**: ~5-7 MB

### Loading Performance
- **First Load**: 3-5 seconds on 3G, <1 second on broadband
- **Cached Load**: <500ms (service worker)

## Troubleshooting

### Deployment fails with "base href" errors
- Ensure `<base href="/fantastic-octo-waffle/" />` matches your repository name
- Update `StaticWebAssetBasePath` in `BlazorApp.csproj`

### 404 errors on GitHub Pages
- Verify `.nojekyll` file exists in deployment root
- Check GitHub Pages source is set to **GitHub Actions**

### Cloudflare Worker CORS errors
- Verify `Access-Control-Allow-Origin` header is set correctly
- Check worker routes match your API endpoints

### Service Worker not updating
- Hard refresh: Ctrl+Shift+R (Windows/Linux) or Cmd+Shift+R (Mac)
- Clear browser cache and service workers in DevTools

### Large bundle size
- Verify AOT compilation is enabled: check `_framework/dotnet.wasm` size
- Ensure trimming is enabled: check for unused assemblies in `_framework/`

## Security Considerations

- **HTTPS Only**: GitHub Pages and Cloudflare Workers enforce HTTPS
- **No Secrets in Client**: Never embed API keys or secrets in Blazor WASM
- **Content Security Policy**: Consider adding CSP headers in worker responses
- **SRI Hashes**: Use Subresource Integrity for CDN resources

## Additional Resources

- [Blazor WASM Deployment](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly)
- [GitHub Pages Documentation](https://docs.github.com/en/pages)
- [Cloudflare Workers Documentation](https://developers.cloudflare.com/workers/)
- [Wrangler CLI Reference](https://developers.cloudflare.com/workers/wrangler/)
