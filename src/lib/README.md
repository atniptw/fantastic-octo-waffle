# Library Modules

This directory contains reusable library modules for the R.E.P.O. Cosmetic Catalog application.

## Available Modules

- **[ZIP Scanner](./zipScanner.ts)** - Browser-based ZIP file scanning with Web Worker support
- **[Thunderstore API Client](./thunderstore/)** - TypeScript client for the Thunderstore API

---

## ZIP Scanner

Browser-compatible ZIP file scanning with Web Worker support for handling large files without blocking the UI.

## Features

- ✅ **Browser-compatible**: Uses Web Crypto API instead of Node.js crypto
- ✅ **Web Worker support**: Non-blocking ZIP extraction for large files (100MB+)
- ✅ **Error handling**: Gracefully handles corrupt ZIPs and missing files
- ✅ **Comprehensive metadata**: Extracts manifest, icons, and cosmetic files with SHA256 hashing
- ✅ **TypeScript**: Full type safety with detailed interfaces
- ✅ **Powered by 7-Zip**: Uses sevenzip-wasm for robust ZIP extraction

## Quick Start

### Basic Usage (Main Thread)

```typescript
import { scanZipFile } from '@/lib/zipScanner';

const handleFileUpload = async (file: File) => {
  try {
    const result = await scanZipFile(file);
    
    if (result.hasFatalError) {
      console.error('Failed to scan ZIP:', result.errors);
      return;
    }
    
    console.log('Manifest:', result.manifest);
    console.log('Cosmetics found:', result.cosmetics.length);
  } catch (error) {
    console.error('Error scanning file:', error);
  }
};
```

### Using Web Worker (Recommended for Large Files)

```typescript
import { useZipScanner } from '@/lib/useZipScanner';

function MyComponent() {
  const { scanFile } = useZipScanner();
  
  const handleFileSelect = async (file: File) => {
    try {
      const result = await scanFile(file, {
        onProgress: ({ progress }) => {
          console.log(`Scanning: ${progress}%`);
        },
      });
      
      console.log('Scan complete!', result);
    } catch (error) {
      console.error('Scan failed:', error);
    }
  };
  
  return (
    <input 
      type="file" 
      accept=".zip" 
      onChange={(e) => e.target.files && handleFileSelect(e.target.files[0])}
    />
  );
}
```

### Batch Scanning Multiple Files

```typescript
import { scanMultipleZipFiles } from '@/lib/zipScanner';

const files: File[] = [...]; // Array of File objects

const result = await scanMultipleZipFiles(
  files.map(file => ({ path: file.name, file }))
);

console.log(`Scanned ${result.total} files`);
console.log(`Successful: ${result.successful.length}`);
console.log(`Failed: ${result.failed.length}`);
console.log(`Total cosmetics: ${result.totalCosmetics}`);
```

## API Reference

### Types

#### `ZipScanResult`

```typescript
interface ZipScanResult {
  manifestContent: string | null;
  manifest: ManifestData | null;
  iconData: Uint8Array | null;
  cosmetics: CosmeticMetadata[];
  cosmeticFiles: Map<string, Uint8Array>; // Deprecated
  errors: string[];
  hasFatalError: boolean;
}
```

#### `CosmeticMetadata`

```typescript
interface CosmeticMetadata {
  internalPath: string;      // e.g., "plugins/MyMod/Decorations/hat.hhh"
  filename: string;           // e.g., "hat.hhh"
  displayName: string;        // e.g., "Hat"
  type: string;               // e.g., "hat", "head", "glasses"
  hash: string;               // SHA256 hash (64 hex chars)
  content: Uint8Array;        // Raw file content
}
```

### Functions

#### `scanZipFile(file: File | Blob): Promise<ZipScanResult>`

Scans a single ZIP file (File or Blob object).

**Parameters:**
- `file` - The ZIP file to scan

**Returns:**
- Promise resolving to `ZipScanResult`

**Example:**
```typescript
const result = await scanZipFile(myFile);
```

#### `scanZip(zipData: Uint8Array | ArrayBuffer): Promise<ZipScanResult>`

Scans ZIP data from a buffer.

**Parameters:**
- `zipData` - ZIP file as Uint8Array or ArrayBuffer

**Returns:**
- Promise resolving to `ZipScanResult`

#### `scanMultipleZipFiles(files: Array<{path: string, file: File | Blob}>): Promise<BatchScanResult>`

Scans multiple ZIP files in parallel.

**Parameters:**
- `files` - Array of objects with `path` and `file` properties

**Returns:**
- Promise resolving to `BatchScanResult`

### Utility Functions

#### `generateDisplayName(filename: string): string`

Converts filenames to human-readable display names.

**Example:**
```typescript
generateDisplayName('cool_hat.hhh')  // Returns: "Cool Hat"
```

#### `inferCosmeticType(filename: string): string`

Infers cosmetic type from filename.

**Returns:** One of: `'head'`, `'hat'`, `'glasses'`, `'mask'`, `'accessory'`, `'decoration'`

**Example:**
```typescript
inferCosmeticType('robot_head.hhh')  // Returns: "head"
```

#### `calculateFileHash(content: Uint8Array): Promise<string>`

Calculates SHA256 hash using Web Crypto API.

**Returns:** 64-character hex string

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
- Errors are logged in the `errors` array but processing continues

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

// Continue processing
if (result.manifest) {
  console.log('Mod:', result.manifest.name);
}
```

## Performance Tips

1. **Use Web Workers for files > 10MB**
   ```typescript
   const { scanFile } = useZipScanner();
   ```

2. **Batch process multiple files**
   ```typescript
   await scanMultipleZipFiles(files);
   ```

3. **Monitor progress for large files**
   ```typescript
   await scanFile(file, {
     onProgress: ({ progress }) => updateUI(progress)
   });
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

## Migration from Node.js Version

If you're migrating from the Node.js version (`src/main/lib/zipScanner.ts`), note these differences:

1. **Hash function is async**:
   ```typescript
   // Old (Node.js)
   const hash = calculateFileHash(content);
   
   // New (Browser)
   const hash = await calculateFileHash(content);
   ```

2. **Uses FileReader instead of Buffer**:
   ```typescript
   // Old (Node.js)
   await scanZip(buffer);
   
   // New (Browser)
   await scanZipFile(file);
   ```

3. **Web Worker support**:
   ```typescript
   // New feature
   const { scanFile } = useZipScanner();
   await scanFile(file);
   ```

## Testing

Run tests with:
```bash
npm test -- src/lib/__tests__/zipScanner.test.ts
```

All tests use browser-compatible APIs and mocked File objects.
