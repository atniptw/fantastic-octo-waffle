# Thunderstore API Integration Guide

This guide explains how to integrate the Thunderstore API client into the R.E.P.O. Cosmetic Catalog application.

## Overview

The Thunderstore API client provides a type-safe, browser-compatible interface for fetching mod packages, metadata, and statistics from Thunderstore.

## Key Use Cases for R.E.P.O.

### 1. Discover R.E.P.O. Cosmetic Mods

Automatically find all mods that contain cosmetic decorations for R.E.P.O.:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';

const client = createThunderstoreClient();

// Get all packages
const allPackages = await client.getPackageIndex();

// Filter for R.E.P.O.-related mods
const repoMods = allPackages.filter(pkg => {
  // Check dependencies
  if (pkg.dependencies.toLowerCase().includes('repo')) return true;
  
  // Check namespace
  if (pkg.namespace.toLowerCase() === 'repo') return true;
  
  // Check for cosmetic keywords
  const name = pkg.name.toLowerCase();
  return name.includes('decoration') || 
         name.includes('cosmetic') || 
         name.includes('hat') ||
         name.includes('skin');
});

console.log(`Found ${repoMods.length} R.E.P.O. cosmetic mods`);
```

### 2. Auto-Download and Import Mods

Combine with the ZIP scanner to automatically download and process mods:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';
import { scanZipFile } from '@/lib/zipScanner';

async function downloadAndImportMod(namespace: string, name: string) {
  const client = createThunderstoreClient();
  
  // Get package info
  const pkg = await client.getPackage(namespace, name);
  
  // Build download URL
  const downloadUrl = client.getPackageDownloadUrl(
    namespace,
    name,
    pkg.latest.version_number
  );
  
  // Download the ZIP
  const response = await fetch(downloadUrl);
  const blob = await response.blob();
  
  // Convert to File for ZIP scanner
  const file = new File([blob], `${pkg.full_name}.zip`, {
    type: 'application/zip'
  });
  
  // Scan the ZIP for cosmetics
  const scanResult = await scanZipFile(file);
  
  if (scanResult.hasFatalError) {
    console.error('Failed to scan mod:', scanResult.errors);
    return null;
  }
  
  return {
    package: pkg,
    cosmetics: scanResult.cosmetics,
    manifest: scanResult.manifest,
    icon: scanResult.icon
  };
}

// Usage
const modData = await downloadAndImportMod('AuthorName', 'CosmeticPack');
```

### 3. Display Mod Information in Catalog

Show Thunderstore metadata alongside cosmetics:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';

async function enrichCosmeticWithModInfo(namespace: string, name: string) {
  const client = createThunderstoreClient();
  
  // Get package and metrics
  const [pkg, metrics] = await Promise.all([
    client.getPackage(namespace, name),
    client.getPackageMetrics(namespace, name)
  ]);
  
  return {
    modName: pkg.full_name,
    author: pkg.owner,
    description: pkg.latest.description,
    version: pkg.latest.version_number,
    downloads: metrics.downloads,
    rating: metrics.rating_score,
    thunderstoreUrl: client.getPackageUrl(namespace, name),
    downloadUrl: client.getPackageDownloadUrl(namespace, name, pkg.latest.version_number),
    isDeprecated: pkg.is_deprecated,
    dateUpdated: pkg.date_updated
  };
}
```

### 4. Bulk Import Popular Mods

Import the top cosmetic mods automatically:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';

async function importPopularCosmetics() {
  const client = createThunderstoreClient();
  
  // Get all packages
  const packages = await client.getPackageIndex();
  
  // Filter and sort R.E.P.O. cosmetic mods
  const repoMods = packages
    .filter(pkg => 
      pkg.dependencies.toLowerCase().includes('repo') ||
      pkg.name.toLowerCase().includes('decoration')
    )
    .slice(0, 50); // Top 50 mods
  
  // Get detailed info for each
  const modDetails = await Promise.all(
    repoMods.map(async pkg => {
      try {
        const [details, metrics] = await Promise.all([
          client.getPackage(pkg.namespace, pkg.name),
          client.getPackageMetrics(pkg.namespace, pkg.name)
        ]);
        
        return {
          pkg,
          details,
          metrics,
          score: metrics.downloads + (metrics.rating_score * 100)
        };
      } catch (error) {
        console.error(`Failed to load ${pkg.namespace}/${pkg.name}`, error);
        return null;
      }
    })
  );
  
  // Sort by popularity score
  return modDetails
    .filter(m => m !== null)
    .sort((a, b) => b!.score - a!.score);
}
```

## Integration with Existing Components

### Update CatalogView to Show Thunderstore Data

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';
import { useState, useEffect } from 'react';

function CatalogView() {
  const [modInfo, setModInfo] = useState<Map<string, any>>(new Map());
  const client = createThunderstoreClient();
  
  // Load mod info for displayed cosmetics
  useEffect(() => {
    async function loadModInfo(cosmetics: Cosmetic[]) {
      const modMap = new Map();
      
      for (const cosmetic of cosmetics) {
        if (cosmetic.mod_namespace && cosmetic.mod_name) {
          const key = `${cosmetic.mod_namespace}/${cosmetic.mod_name}`;
          
          if (!modMap.has(key)) {
            try {
              const pkg = await client.getPackage(
                cosmetic.mod_namespace,
                cosmetic.mod_name
              );
              modMap.set(key, pkg);
            } catch (error) {
              console.error(`Failed to load mod info for ${key}`);
            }
          }
        }
      }
      
      setModInfo(modMap);
    }
    
    loadModInfo(cosmetics);
  }, [cosmetics]);
  
  return (
    <div>
      {cosmetics.map(cosmetic => (
        <div key={cosmetic.id}>
          <h3>{cosmetic.display_name}</h3>
          
          {modInfo.has(`${cosmetic.mod_namespace}/${cosmetic.mod_name}`) && (
            <div className="mod-info">
              <p>
                From: {modInfo.get(`${cosmetic.mod_namespace}/${cosmetic.mod_name}`).full_name}
              </p>
              <a 
                href={client.getPackageUrl(cosmetic.mod_namespace, cosmetic.mod_name)}
                target="_blank"
              >
                View on Thunderstore
              </a>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
```

### Add "Import from Thunderstore" Feature

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';
import { scanZipFile } from '@/lib/zipScanner';

function ImportButton() {
  const [importing, setImporting] = useState(false);
  const [progress, setProgress] = useState('');
  
  const importFromThunderstore = async (namespace: string, name: string) => {
    setImporting(true);
    setProgress('Fetching mod info...');
    
    try {
      const client = createThunderstoreClient();
      
      // Get package
      const pkg = await client.getPackage(namespace, name);
      setProgress(`Downloading ${pkg.full_name}...`);
      
      // Download
      const downloadUrl = client.getPackageDownloadUrl(
        namespace,
        name,
        pkg.latest.version_number
      );
      
      const response = await fetch(downloadUrl);
      const blob = await response.blob();
      const file = new File([blob], `${pkg.full_name}.zip`);
      
      setProgress('Scanning ZIP...');
      
      // Scan
      const scanResult = await scanZipFile(file);
      
      if (scanResult.hasFatalError) {
        throw new Error('Failed to scan ZIP');
      }
      
      setProgress('Importing to database...');
      
      // Import to IndexedDB (use existing importer logic)
      await importModToDatabase(scanResult, {
        source: 'thunderstore',
        thunderstore_namespace: namespace,
        thunderstore_name: name,
        thunderstore_version: pkg.latest.version_number
      });
      
      setProgress('Done!');
    } catch (error) {
      console.error('Import failed:', error);
      setProgress(`Error: ${error.message}`);
    } finally {
      setImporting(false);
    }
  };
  
  return (
    <div>
      <button 
        onClick={() => importFromThunderstore('Author', 'ModName')}
        disabled={importing}
      >
        {importing ? progress : 'Import from Thunderstore'}
      </button>
    </div>
  );
}
```

## Caching Strategy

To avoid hitting API rate limits and improve performance:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';

class ThunderstoreCache {
  private packageIndex: PackageIndexEntry[] | null = null;
  private packageCache = new Map<string, PackageExperimental>();
  private lastIndexUpdate: number = 0;
  private readonly CACHE_DURATION = 1000 * 60 * 60; // 1 hour
  
  private client = createThunderstoreClient();
  
  async getPackageIndex(): Promise<PackageIndexEntry[]> {
    const now = Date.now();
    
    if (!this.packageIndex || (now - this.lastIndexUpdate) > this.CACHE_DURATION) {
      this.packageIndex = await this.client.getPackageIndex();
      this.lastIndexUpdate = now;
      
      // Store in IndexedDB for offline access
      await this.storeInIndexedDB('package-index', this.packageIndex);
    }
    
    return this.packageIndex;
  }
  
  async getPackage(namespace: string, name: string): Promise<PackageExperimental> {
    const key = `${namespace}/${name}`;
    
    if (!this.packageCache.has(key)) {
      const pkg = await this.client.getPackage(namespace, name);
      this.packageCache.set(key, pkg);
      
      // Store in IndexedDB
      await this.storeInIndexedDB(`package-${key}`, pkg);
    }
    
    return this.packageCache.get(key)!;
  }
  
  private async storeInIndexedDB(key: string, data: any) {
    // Implementation using IndexedDB
    // Can use the same database as cosmetics catalog
  }
}

// Export singleton
export const thunderstoreCache = new ThunderstoreCache();
```

## Error Handling

Always handle API errors gracefully:

```typescript
import { createThunderstoreClient } from '@/lib/thunderstore';

async function safeGetPackage(namespace: string, name: string) {
  const client = createThunderstoreClient();
  
  try {
    return await client.getPackage(namespace, name);
  } catch (error) {
    if (error.message.includes('404')) {
      console.log(`Package ${namespace}/${name} not found on Thunderstore`);
      return null;
    }
    
    if (error.message.includes('403')) {
      console.error('Access denied - check authentication');
      return null;
    }
    
    if (error.message.includes('429')) {
      console.error('Rate limited - try again later');
      return null;
    }
    
    // Unknown error
    console.error('API error:', error);
    throw error;
  }
}
```

## Performance Tips

1. **Use Package Index for Discovery**: The package index endpoint is optimized for bulk data retrieval
2. **Batch Requests**: Use `Promise.all()` to fetch multiple packages in parallel
3. **Cache Aggressively**: Package data doesn't change frequently
4. **Store in IndexedDB**: Keep a local copy for offline access
5. **Lazy Load Details**: Only fetch full package details when needed

## Next Steps

1. ✅ Add "Import from Thunderstore" button to ImportButton component
2. ✅ Display Thunderstore metadata in CatalogView
3. ✅ Implement caching layer in IndexedDB
4. ✅ Add "Discover Mods" feature to find new cosmetics
5. ✅ Show mod popularity/ratings in UI
6. ✅ Add "Check for Updates" feature to notify when mod versions change

## API Reference

For complete API documentation, see [src/lib/thunderstore/README.md](./thunderstore/README.md)
