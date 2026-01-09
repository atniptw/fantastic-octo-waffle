# Pipeline Package

Complete pipeline for downloading mod ZIPs, extracting metadata, and processing cosmetics.

## Architecture

The pipeline is organized into three layers:

```
src/lib/pipeline/
├── types.ts                 # Shared type definitions
├── core/                    # Core functions (pure)
│   ├── scanner.ts          # ZIP scanning & metadata extraction
│   ├── cosmetic.ts         # Cosmetic utilities (display names, types, image conversion)
│   └── index.ts            # Core exports
├── hooks/                   # React hooks
│   ├── useModAnalyzer.ts   # ⭐ PRIMARY HIGH-LEVEL API
│   ├── useZipDownloader.ts # Download & cache management
│   ├── useScanWorker.ts    # Web Worker scanning
│   └── index.ts            # Hook exports
└── index.ts                # Main package export
```

## Primary API: `useModAnalyzer`

**For most use cases, use this high-level hook:**

```typescript
import { useModAnalyzer } from '@/lib/pipeline';

function ModAnalyzer({ modUrl, namespace, name }) {
  const { analyze, isAnalyzing, error, cancel } = useModAnalyzer();
  const [cosmetics, setCosmetics] = useState([]);

  const handleAnalyze = async () => {
    try {
      const result = await analyze(modUrl, namespace, name, {
        onProgress: (p) => console.log(`${p.stage}: ${p.percent}%`),
      });
      
      setCosmetics(result.cosmetics);
      console.log('Mod info:', result.mod);
      console.log('Warnings:', result.warnings);
    } catch (err) {
      console.error('Analysis failed:', err);
    }
  };

  return (
    <div>
      <button onClick={handleAnalyze} disabled={isAnalyzing}>
        {isAnalyzing ? 'Analyzing...' : 'Analyze Mod'}
      </button>
      {error && <p className="error">{error}</p>}
      <div className="cosmetics-grid">
        {cosmetics.map((cosmetic) => (
          <div key={cosmetic.hash}>
            <img src={URL.createObjectURL(cosmetic.image)} alt={cosmetic.displayName} />
            <p>{cosmetic.displayName}</p>
            <span>{cosmetic.type}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
```

### Result Types

```typescript
interface CosmeticPreview {
  image: Blob;              // PNG/JPEG blob ready to display
  displayName: string;      // "Cool Hat"
  type: string;             // "hat", "glasses", "head", etc.
  hash: string;             // SHA256 hash
  filename: string;         // Original filename
  internalPath: string;     // Path within ZIP
}

interface AnalyzeResult {
  cosmetics: CosmeticPreview[];
  mod: {
    name: string;
    author: string;
    version: string;
    icon?: Blob;
  };
  warnings: string[];       // Non-fatal errors
}
```

## Advanced APIs

Use these when you need more control or specific features:

### Download & Cache Management

```typescript
import { useZipDownloader } from '@/lib/pipeline';

function MyComponent() {
  const { download, getCached, listCached, deleteCached } = useZipDownloader();
  
  const handleDownload = async () => {
    const buffer = await download(url, namespace, name);
    // ... use buffer
  };
  
  return <button onClick={handleDownload}>Download</button>;
}
```

### Web Worker Scanning

```typescript
import { useScanWorker } from '@/lib/pipeline';

function MyComponent() {
  const { scanFile, isScanning } = useScanWorker();
  
  const handleScan = async (file: File) => {
    const result = await scanFile(file, {
      onProgress: ({ progress }) => console.log(`${progress}%`),
    });
    console.log('Scan complete!', result);
  };
  
  return <button onClick={handleScan} disabled={isScanning}>Scan</button>;
}
```

### Core Functions (Pure)

```typescript
import { scanZip, generateDisplayName, inferCosmeticType } from '@/lib/pipeline';

// Scan ZIP buffer directly
const result = await scanZip(zipBuffer);

// Utility functions
const displayName = generateDisplayName('cool_hat.hhh'); // "Cool Hat"
const type = inferCosmeticType('awesome_glasses.hhh');   // "glasses"
```

## Features

- ✅ **High-level API**: Simple `useModAnalyzer` for most use cases
- ✅ **Download & Cache**: Automatic IndexedDB caching
- ✅ **Browser-compatible**: Web Crypto API, no Node.js dependencies
- ✅ **Web Worker support**: Non-blocking for large files (100MB+)
- ✅ **Error handling**: Graceful handling of corrupt ZIPs
- ✅ **Progress tracking**: Real-time progress callbacks
- ✅ **TypeScript**: Full type safety
- ✅ **Image conversion**: Placeholder images (3D rendering coming soon)

## Batch Scanning

```typescript
import { scanMultipleZipFiles } from '@/lib/pipeline';

const files: File[] = [...];

const result = await scanMultipleZipFiles(
  files.map(file => ({ path: file.name, file }))
);

console.log(`Scanned ${result.total} files`);
console.log(`Successful: ${result.successful.length}`);
console.log(`Failed: ${result.failed.length}`);
console.log(`Total cosmetics: ${result.totalCosmetics}`);
```

## API Reference

### Primary Hook: `useModAnalyzer`

#### `useModAnalyzer(): UseModAnalyzerResult`

High-level hook for complete mod analysis workflow.

**Returns:**
- `analyze(modUrl, namespace, name, options?): Promise<AnalyzeResult>` - Analyze mod
- `isAnalyzing: boolean` - Analysis in progress
- `error: string | null` - Error message
- `cancel(): void` - Cancel analysis

**Analyze Options:**
- `onProgress?: (progress: AnalyzeProgress) => void` - Progress callback
- `signal?: AbortSignal` - Abort signal
- `metadataOnly?: boolean` - Skip image conversion

**Progress Stages:**
- `download` (0-40%) - Downloading ZIP
- `extract` (40-70%) - Extracting & scanning
- `convert` (70-95%) - Converting to images
- `complete` (100%) - Done

### Downloader Hook

#### `useZipDownloader(): UseZipDownloaderResult`

Returns an object with:
- `download(url, namespace, name, options?): Promise<ArrayBuffer>` - Download and cache ZIP
- `getCached(namespace, name): Promise<Uint8Array | null>` - Get cached ZIP
- `getCachedMetadata(namespace, name): Promise<CachedZipMetadata | null>` - Get metadata
- `listCached(): Promise<CachedZipMetadata[]>` - List all cached ZIPs
- `deleteCached(namespace, name): Promise<void>` - Delete cached ZIP
- `isDownloading: boolean` - Download in progress
- `progress: DownloadProgress | null` - Progress info
- `error: string | null` - Error message
- `cancel(): void` - Cancel download

### Scanner Functions

#### `scanZipFile(file: File | Blob, options?): Promise<ZipScanResult>`

Scans a single ZIP file.

**Options:**
- `onProgress?: (progress) => void` - Progress callback
- `signal?: AbortSignal` - Abort signal
- `debug?: boolean` - Enable debug logging

#### `scanZip(zipData: Uint8Array | ArrayBuffer, options?): Promise<ZipScanResult>`

Scans ZIP data from buffer.

#### `scanMultipleZipFiles(files): Promise<BatchScanResult>`

Scans multiple ZIPs in parallel.

### Scan Worker Hook

#### `useScanWorker(): UseZipScannerResult`

Returns an object with:
- `scanFile(file, options?): Promise<ZipScanResult>` - Scan in worker
- `isScanning: boolean` - Scan in progress
- `cancelScan(scanId?): void` - Cancel scan(s)

**Options:**
- `onProgress?: (progress) => void`
- `onComplete?: (result, fileName) => void`
- `onError?: (error) => void`
- `signal?: AbortSignal`

### Types

#### `ZipScanResult`

```typescript
interface ZipScanResult {
  manifestContent: string | null;
  manifest: ManifestData | null;
  iconData: Uint8Array | null;
  cosmetics: CosmeticMetadata[];
  errors: string[];
  hasFatalError: boolean;
}
```

#### `CosmeticMetadata`

```typescript
interface CosmeticMetadata {
  internalPath: string;      // Path in ZIP
  filename: string;          // Just the filename
  displayName: string;       // Human-readable name
  type: string;              // Inferred type
  hash: string;              // SHA256 hex
  size: number;              // Bytes
}
```

#### `ManifestData`

```typescript
interface ManifestData {
  name: string;
  author: string;
  version_number: string;
  description?: string;
  dependencies?: string[];
}
```

### Utility Functions

#### `generateDisplayName(filename: string): string`

Converts filename to display name.

```typescript
generateDisplayName('cool_hat.hhh')  // "Cool Hat"
```

#### `inferCosmeticType(filename: string): string`

Infers type from filename. Returns one of:
- `'head'`
- `'hat'`
- `'glasses'`
- `'mask'`
- `'accessory'`
- `'decoration'` (default)

#### `calculateFileHash(content: Uint8Array): Promise<string>`

Calculates SHA256 hash using Web Crypto API.

#### `parseManifest(content: string): ManifestData | null`

Parses manifest.json string.

#### `isValidScanResult(result: ZipScanResult): boolean`

Checks if result has valid manifest.

#### `getCosmeticPaths(result: ZipScanResult): string[]`

Gets all cosmetic file paths from result.

## Error Handling

The scanner distinguishes between fatal and non-fatal errors:

### Fatal Errors

- Corrupt or invalid ZIP files
- Sets `hasFatalError: true`
- ZIP cannot be processed

### Non-Fatal Errors

- Missing `icon.png` (optional)
- Invalid manifest format
- Error reading specific cosmetic files
- Logged in `errors` array but processing continues

**Example:**
```typescript
const result = await scanZipFile(file);

if (result.hasFatalError) {
  console.error('Cannot process ZIP:', result.errors);
  return;
}

if (result.errors.length > 0) {
  console.warn('Non-fatal errors:', result.errors);
}
```

## Performance Tips

1. **Use Web Worker for files > 10MB**
   ```typescript
   const { scanFile } = useScanWorker();
   ```

2. **Batch process multiple files**
   ```typescript
   await scanMultipleZipFiles(files);
   ```

3. **Monitor progress for large files**
   ```typescript
   await scanFile(file, {
     onProgress: ({ progress, stage }) => updateUI(progress, stage)
   });
   ```

4. **Reuse cached downloads**
   ```typescript
   const cached = await getCached(namespace, name);
   if (cached) return cached;
   ```

## Browser Compatibility

- Chrome 90+
- Firefox 88+
- Edge 90+
- Safari 14+

Requires:
- Web Crypto API
- FileReader API
- Web Workers
- ES2020+

## Testing

```bash
npm test -- src/lib/pipeline
```
