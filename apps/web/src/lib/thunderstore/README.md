# Thunderstore API Client

A TypeScript client library for the [Thunderstore API](https://thunderstore.io/api/docs/). Fully browser-compatible with no Node.js dependencies.

## Features

- ✅ **Type-safe** - Full TypeScript type definitions for all API endpoints
- ✅ **Browser-compatible** - Works in any modern browser, no Node.js required
- ✅ **Zero dependencies** - Uses native Fetch API
- ✅ **Comprehensive** - Covers all public Thunderstore API endpoints
- ✅ **Well-documented** - JSDoc comments for all methods
- ✅ **Easy to use** - Simple, intuitive API

## Installation

```typescript
import { createThunderstoreClient } from './lib/thunderstore';
```

## Quick Start

```typescript
// Create a client instance
const client = createThunderstoreClient();

// Get all packages efficiently using the package index
const packages = await client.getPackageIndex();

// Get a specific package
const bepisPackage = await client.getPackage('BepInEx', 'BepInExPack');

// Get package metrics
const metrics = await client.getPackageMetrics('BepInEx', 'BepInExPack');

// Get package version details
const version = await client.getPackageVersion('BepInEx', 'BepInExPack', '5.4.21');

// Get readme
const readme = await client.getPackageVersionReadme('BepInEx', 'BepInExPack', '5.4.21');
console.log(readme.markdown);
```

## API Reference

### Client Configuration

```typescript
const client = createThunderstoreClient({
  baseUrl?: string;           // Default: 'https://thunderstore.io'
  sessionToken?: string;      // Optional authentication token
  fetchImpl?: typeof fetch;   // Custom fetch implementation
});
```

### Community API

```typescript
// List all communities
const communities = await client.listCommunities({ cursor: 'next-page-cursor' });

// Get current community (based on domain)
const current = await client.getCurrentCommunity();

// Get specific community
const community = await client.getCommunity('valheim');

// List community categories
const categories = await client.listCommunityCategories('valheim');
```

### Package API

```typescript
// Get package index (recommended for bulk package data)
// Returns newline-delimited JSON with all packages
const allPackages = await client.getPackageIndex();

// Get a specific package
const pkg = await client.getPackage('namespace', 'packageName');

// Get a specific version
const version = await client.getPackageVersion('namespace', 'packageName', '1.0.0');

// Get changelog
const changelog = await client.getPackageVersionChangelog('namespace', 'packageName', '1.0.0');

// Get readme
const readme = await client.getPackageVersionReadme('namespace', 'packageName', '1.0.0');
```

### Package Metrics

```typescript
// Get package-level metrics
const metrics = await client.getPackageMetrics('BepInEx', 'BepInExPack');
// Returns: { downloads: number, rating_score: number, latest_version: string }

// Get version-level metrics
const versionMetrics = await client.getPackageVersionMetrics('BepInEx', 'BepInExPack', '5.4.21');
// Returns: { downloads: number }
```

### Wiki API

```typescript
// List all package wikis (with optional filtering)
const wikis = await client.listPackageWikis(new Date('2024-01-01'));

// Get wiki for a package
const wiki = await client.getPackageWiki('namespace', 'packageName');

// Get a specific wiki page
const page = await client.getWikiPage(123);

// Create/update wiki page (requires authentication)
const newPage = await client.upsertWikiPage('namespace', 'packageName', {
  id: 'optional-for-update',
  title: 'Getting Started',
  markdown_content: '# Hello World'
});

// Delete wiki page (requires authentication)
await client.deleteWikiPage('namespace', 'packageName', 'page-id');
```

### Markdown Rendering

```typescript
// Render markdown to HTML
const html = await client.renderMarkdown({
  markdown: '# Hello **World**'
});
console.log(html.html); // '<h1>Hello <strong>World</strong></h1>'
```

### Helper Methods

```typescript
// Build URLs programmatically
const downloadUrl = client.getPackageDownloadUrl('BepInEx', 'BepInExPack', '5.4.21');
// https://thunderstore.io/package/download/BepInEx/BepInExPack/5.4.21/

const packageUrl = client.getPackageUrl('BepInEx', 'BepInExPack');
// https://thunderstore.io/package/BepInEx/BepInExPack/

const versionUrl = client.getPackageVersionUrl('BepInEx', 'BepInExPack', '5.4.21');
// https://thunderstore.io/package/BepInEx/BepInExPack/5.4.21/
```

## Authentication

Some endpoints require authentication. Set a session token:

```typescript
const client = createThunderstoreClient({
  sessionToken: 'your-session-token'
});

// Or update it later
client.setSessionToken('new-token');
```

## Types

All API types are fully typed. Import them as needed:

```typescript
import type {
  Community,
  PackageExperimental,
  PackageIndexEntry,
  PackageMetrics,
  Wiki,
  WikiPage,
  // ... and more
} from './lib/thunderstore';
```

## Package Index Format

The package index endpoint returns newline-delimited JSON for efficiency:

```typescript
const packages = await client.getPackageIndex();
// Returns: PackageIndexEntry[]

// Each entry contains:
{
  namespace: string;
  name: string;
  version_number: string;
  file_format: string;
  file_size: number;
  dependencies: string; // Comma-separated dependency strings
}
```

**Download links are not included** - use the helper method:

```typescript
const downloadUrl = client.getPackageDownloadUrl(
  pkg.namespace,
  pkg.name,
  pkg.version_number
);
```

## Error Handling

All methods throw errors on failed requests:

```typescript
try {
  const pkg = await client.getPackage('Invalid', 'Package');
} catch (error) {
  console.error('API Error:', error.message);
  // "API request failed: 404 Not Found"
}
```

## Use Cases

### Find Cosmetic Mods for R.E.P.O.

```typescript
const packages = await client.getPackageIndex();

// Filter packages by namespace or dependencies
const repoMods = packages.filter(pkg => 
  pkg.dependencies.includes('R.E.P.O') ||
  pkg.namespace === 'REPO'
);

// Get full package details
for (const mod of repoMods) {
  const details = await client.getPackage(mod.namespace, mod.name);
  console.log(`${details.full_name}: ${details.latest.description}`);
}
```

### Download Package ZIPs

```typescript
const pkg = await client.getPackage('BepInEx', 'BepInExPack');
const downloadUrl = client.getPackageDownloadUrl(
  pkg.namespace,
  pkg.name,
  pkg.latest.version_number
);

// Download the ZIP
const response = await fetch(downloadUrl);
const blob = await response.blob();
// Process with JSZip...
```

### Cache Package Metadata

```typescript
// Get all packages efficiently
const allPackages = await client.getPackageIndex();

// Store in IndexedDB for offline access
const db = await openDB('thunderstore-cache');
const tx = db.transaction('packages', 'readwrite');
await Promise.all(allPackages.map(pkg => tx.store.put(pkg)));
await tx.done;
```

## API Coverage

### Implemented Endpoints

- ✅ Community listing and details
- ✅ Package index (bulk package data)
- ✅ Package details (experimental API)
- ✅ Package version details
- ✅ Package metrics (downloads, ratings)
- ✅ Changelog and README retrieval
- ✅ Wiki management (list, read, write, delete)
- ✅ Markdown rendering
- ✅ V1 package listing (legacy)

### Not Implemented (Authentication Required)

- ❌ Package submission/upload
- ❌ Package validation
- ❌ OAuth authentication flows
- ❌ User profile management
- ❌ Package rating
- ❌ Package reporting/moderation

## License

MIT
