/**
 * Browser-compatible ZIP Scanner module for scanning Thunderstore mod ZIP files.
 * Extracts manifest.json, icon.png, and .hhh cosmetic files with comprehensive metadata.
 * Uses Web Crypto API instead of Node.js crypto for browser compatibility.
 */

import JSZip from 'jszip';

/**
 * Metadata for a single cosmetic file extracted from a ZIP.
 */
export interface CosmeticMetadata {
  /** The internal path within the ZIP file */
  internalPath: string;
  /** The filename (with extension) */
  filename: string;
  /** Human-readable display name */
  displayName: string;
  /** Inferred cosmetic type (e.g., 'head', 'decoration', 'accessory') */
  type: string;
  /** SHA256 hash of the file content */
  hash: string;
  /** Raw file content */
  content: Uint8Array;
}

export interface ManifestData {
  name: string;
  author: string;
  version_number: string;
  description?: string;
  dependencies?: string[];
}

export interface ZipScanResult {
  manifestContent: string | null;
  manifest: ManifestData | null;
  iconData: Uint8Array | null;
  /** Array of cosmetic metadata */
  cosmetics: CosmeticMetadata[];
  /** Deprecated: Use cosmetics array instead */
  cosmeticFiles: Map<string, Uint8Array>;
  errors: string[];
  /** True if there was a fatal error parsing the ZIP file */
  hasFatalError: boolean;
}

/**
 * Parse manifest.json content.
 */
export function parseManifest(content: string): ManifestData | null {
  try {
    const data = JSON.parse(content);
    if (
      typeof data.name !== 'string' ||
      typeof data.author !== 'string' ||
      typeof data.version_number !== 'string'
    ) {
      return null;
    }
    return {
      name: data.name,
      author: data.author,
      version_number: data.version_number,
      description: data.description,
      dependencies: data.dependencies,
    };
  } catch {
    return null;
  }
}

/**
 * Generates a display name from a filename by removing extension and formatting.
 * Example: "my_cool_hat.hhh" -> "My Cool Hat"
 * Note: This function expects .hhh files. If a filename doesn't end with .hhh,
 * the extension will remain in the display name.
 *
 * @param filename - The filename to convert (expected to end with .hhh)
 * @returns Human-readable display name
 */
export function generateDisplayName(filename: string): string {
  // Trim whitespace first
  const trimmed = filename.trim();
  
  // Remove extension
  const nameWithoutExt = trimmed.replace(/\.hhh$/i, '');
  
  // Replace underscores and hyphens with spaces
  // Capitalize first letter of each word
  return nameWithoutExt
    .replace(/[_-]/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase())
    .trim();
}

/**
 * Infers cosmetic type from filename.
 * Looks for common patterns like "head", "hat", "accessory", etc.
 * Uses word boundary matching to avoid false positives from substrings.
 *
 * @param filename - The filename to analyze
 * @returns Inferred type string
 */
export function inferCosmeticType(filename: string): string {
  const lowerFilename = filename.toLowerCase();
  
  // Check for common type keywords using lookahead/lookbehind for word boundaries
  // Match whole words that are separated by underscores, hyphens, dots, or string boundaries
  if (/(?:^|[_\-. ])head(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'head';
  } else if (/(?:^|[_\-. ])hat(?:[_\-. ]|$)/.test(lowerFilename) || /(?:^|[_\-. ])helmet(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'hat';
  } else if (/(?:^|[_\-. ])glasses(?:[_\-. ]|$)/.test(lowerFilename) || /(?:^|[_\-. ])goggles(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'glasses';
  } else if (/(?:^|[_\-. ])mask(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'mask';
  } else if (/(?:^|[_\-. ])accessory(?:[_\-. ]|$)/.test(lowerFilename) || /acc_/.test(lowerFilename)) {
    return 'accessory';
  }
  
  // Default to decoration
  return 'decoration';
}

/**
 * Calculates SHA256 hash for file content using Web Crypto API.
 * This is browser-compatible, unlike Node.js's crypto module.
 *
 * @param content - File content as Uint8Array
 * @returns Hex-encoded SHA256 hash
 */
export async function calculateFileHash(content: Uint8Array): Promise<string> {
  // Use Web Crypto API (available in browsers)
  // Pass the Uint8Array directly - it's a BufferSource
  const hashBuffer = await crypto.subtle.digest('SHA-256', content);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
  return hashHex;
}

/**
 * Extracts cosmetic metadata from a file path and content.
 *
 * @param internalPath - The path within the ZIP file
 * @param content - The file content
 * @returns CosmeticMetadata object
 */
export async function extractCosmeticMetadata(
  internalPath: string,
  content: Uint8Array
): Promise<CosmeticMetadata> {
  // Extract filename from path (handle both forward and backward slashes)
  const normalizedPath = internalPath.replace(/\\/g, '/');
  const parts = normalizedPath.split('/');
  const filename = parts[parts.length - 1] || 'unknown.hhh';
  
  return {
    internalPath: normalizedPath,
    filename,
    displayName: generateDisplayName(filename),
    type: inferCosmeticType(filename),
    hash: await calculateFileHash(content),
    content,
  };
}

/**
 * Scans a ZIP file and extracts relevant mod data.
 * Looks for:
 * - manifest.json at root
 * - icon.png at root
 * - *.hhh files in plugins/<plugin>/Decorations/ directories
 *
 * This is the browser-compatible version that works with File objects.
 *
 * @param file - The ZIP file as a File or Blob object
 * @returns ZipScanResult containing extracted data and any errors
 */
export async function scanZipFile(file: File | Blob): Promise<ZipScanResult> {
  // Read the file as ArrayBuffer using FileReader for better compatibility
  const arrayBuffer = await new Promise<ArrayBuffer>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(file);
  });
  
  const zipData = new Uint8Array(arrayBuffer);
  
  return scanZip(zipData);
}

/**
 * Scans a ZIP file buffer and extracts relevant mod data.
 * Looks for:
 * - manifest.json at root
 * - icon.png at root
 * - *.hhh files in plugins/<plugin>/Decorations/ directories
 *
 * @param zipData - The ZIP file as a Uint8Array or ArrayBuffer
 * @returns ZipScanResult containing extracted data and any errors
 */
export async function scanZip(zipData: Uint8Array | ArrayBuffer): Promise<ZipScanResult> {
  const result: ZipScanResult = {
    manifestContent: null,
    manifest: null,
    iconData: null,
    cosmetics: [],
    cosmeticFiles: new Map(),
    errors: [],
    hasFatalError: false,
  };

  try {
    const zip = await JSZip.loadAsync(zipData);

    // Extract manifest.json
    const manifestFile = zip.file('manifest.json');
    if (manifestFile) {
      try {
        result.manifestContent = await manifestFile.async('string');
        result.manifest = parseManifest(result.manifestContent);
        if (!result.manifest) {
          result.errors.push('Invalid manifest.json format');
        }
      } catch (e) {
        result.errors.push(`Error reading manifest.json: ${e instanceof Error ? e.message : String(e)}`);
      }
    } else {
      result.errors.push('manifest.json not found');
    }

    // Extract icon.png
    const iconFile = zip.file('icon.png');
    if (iconFile) {
      try {
        result.iconData = await iconFile.async('uint8array');
      } catch (e) {
        result.errors.push(`Error reading icon.png: ${e instanceof Error ? e.message : String(e)}`);
      }
    }

    // Extract .hhh files from plugins/<plugin>/Decorations/ directories
    // Note: JSZip normalizes all paths to use forward slashes internally,
    // even if the ZIP was created on Windows with backslashes
    const cosmeticPattern = /^plugins\/[^/]+\/Decorations\/[^/]+\.hhh$/i;
    
    for (const [path, file] of Object.entries(zip.files)) {
      if (!file.dir && cosmeticPattern.test(path)) {
        try {
          const content = await file.async('uint8array');
          const metadata = await extractCosmeticMetadata(path, content);
          result.cosmetics.push(metadata);
          
          // Maintain backward compatibility with old API
          result.cosmeticFiles.set(metadata.internalPath, content);
        } catch (e) {
          result.errors.push(`Error reading ${path}: ${e instanceof Error ? e.message : String(e)}`);
        }
      }
    }
  } catch (e) {
    result.errors.push(`Error parsing ZIP file: ${e instanceof Error ? e.message : String(e)}`);
    result.hasFatalError = true;
  }

  return result;
}

/**
 * Validates that a ZIP scan result contains the minimum required data for import.
 *
 * @param scanResult - The result from scanZip
 * @returns true if the scan result has valid manifest data
 */
export function isValidScanResult(scanResult: ZipScanResult): boolean {
  return scanResult.manifest !== null;
}

/**
 * Gets the list of cosmetic file paths from a scan result.
 *
 * @param scanResult - The result from scanZip
 * @returns Array of cosmetic file paths
 */
export function getCosmeticPaths(scanResult: ZipScanResult): string[] {
  return scanResult.cosmetics.map(c => c.internalPath);
}

/**
 * Result of scanning multiple ZIP files.
 */
export interface BatchScanResult {
  /** Successfully scanned ZIPs with their results */
  successful: Array<{ zipPath: string; result: ZipScanResult }>;
  /** Failed ZIPs with error information */
  failed: Array<{ zipPath: string; error: string }>;
  /** Total number of ZIPs processed */
  total: number;
  /** Total number of cosmetics found across all ZIPs */
  totalCosmetics: number;
}

/**
 * Scans multiple ZIP files and returns aggregate results.
 * Processes each ZIP independently, collecting errors per file.
 *
 * @param zipFiles - Array of File or Blob objects to scan
 * @returns BatchScanResult with successful and failed scans
 */
export async function scanMultipleZipFiles(
  zipFiles: Array<{ path: string; file: File | Blob }>
): Promise<BatchScanResult> {
  const result: BatchScanResult = {
    successful: [],
    failed: [],
    total: zipFiles.length,
    totalCosmetics: 0,
  };

  const scanPromises = zipFiles.map(async (zipFile) => {
    try {
      const scanResult = await scanZipFile(zipFile.file);
      return {
        zipPath: zipFile.path,
        scanResult,
      };
    } catch (e) {
      return {
        zipPath: zipFile.path,
        error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
      };
    }
  });

  const settledResults = await Promise.allSettled(scanPromises);

  for (const settled of settledResults) {
    if (settled.status === 'fulfilled') {
      const { zipPath, scanResult, error } = settled.value;
      if (scanResult) {
        // Use the explicit hasFatalError flag to determine success/failure
        if (scanResult.hasFatalError) {
          // Fatal ZIP parsing error
          result.failed.push({
            zipPath,
            error: scanResult.errors.join('; '),
          });
        } else {
          // Non-fatal errors (like missing icon) are considered successful
          result.successful.push({
            zipPath,
            result: scanResult,
          });
          result.totalCosmetics += scanResult.cosmetics.length;
        }
      } else if (error) {
        result.failed.push({
          zipPath,
          error,
        });
      }
    } else {
      // Promise rejected (should be rare, but handle just in case)
      const reason = settled.reason as { zipPath?: string; error?: string };
      result.failed.push({
        zipPath: reason?.zipPath || 'unknown',
        error: reason?.error || String(settled.reason),
      });
    }
  }

  return result;
}

/**
 * Data structure for communicating with Web Worker
 */
export interface WorkerMessage {
  type: 'scan' | 'result' | 'error' | 'progress';
  file?: ArrayBuffer;
  fileName?: string;
  result?: ZipScanResult;
  error?: string;
  progress?: number;
}
