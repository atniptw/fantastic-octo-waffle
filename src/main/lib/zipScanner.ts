/**
 * ZIP Scanner module for scanning Thunderstore mod ZIP files.
 * Extracts manifest.json, icon.png, and .hhh cosmetic files with comprehensive metadata.
 */

import JSZip from 'jszip';
import { createHash } from 'crypto';
import { ManifestData, parseManifest } from './importer';

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
 * Generates a display name from a filename by removing extension and formatting.
 * Example: "my_cool_hat.hhh" -> "My Cool Hat"
 *
 * @param filename - The filename to convert
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
 *
 * @param filename - The filename to analyze
 * @returns Inferred type string
 */
export function inferCosmeticType(filename: string): string {
  const lowerFilename = filename.toLowerCase();
  
  // Check for common type keywords
  if (lowerFilename.includes('head')) {
    return 'head';
  } else if (lowerFilename.includes('hat') || lowerFilename.includes('helmet')) {
    return 'hat';
  } else if (lowerFilename.includes('glasses') || lowerFilename.includes('goggles')) {
    return 'glasses';
  } else if (lowerFilename.includes('mask')) {
    return 'mask';
  } else if (lowerFilename.includes('accessory') || lowerFilename.includes('acc_')) {
    return 'accessory';
  }
  
  // Default to decoration
  return 'decoration';
}

/**
 * Calculates SHA256 hash for file content.
 *
 * @param content - File content as Uint8Array
 * @returns Hex-encoded SHA256 hash
 */
export function calculateFileHash(content: Uint8Array): string {
  return createHash('sha256').update(content).digest('hex');
}

/**
 * Extracts cosmetic metadata from a file path and content.
 *
 * @param internalPath - The path within the ZIP file
 * @param content - The file content
 * @returns CosmeticMetadata object
 */
export function extractCosmeticMetadata(
  internalPath: string,
  content: Uint8Array
): CosmeticMetadata {
  // Extract filename from path (handle both forward and backward slashes)
  const normalizedPath = internalPath.replace(/\\/g, '/');
  const parts = normalizedPath.split('/');
  const filename = parts[parts.length - 1] || 'unknown.hhh';
  
  return {
    internalPath: normalizedPath,
    filename,
    displayName: generateDisplayName(filename),
    type: inferCosmeticType(filename),
    hash: calculateFileHash(content),
    content,
  };
}

/**
 * Scans a ZIP file buffer and extracts relevant mod data.
 * Looks for:
 * - manifest.json at root
 * - icon.png at root
 * - *.hhh files in plugins/<plugin>/Decorations/ directories
 *
 * @param zipData - The ZIP file as a Buffer or Uint8Array
 * @returns ZipScanResult containing extracted data and any errors
 */
export async function scanZip(zipData: Buffer | Uint8Array): Promise<ZipScanResult> {
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
    // Handle both forward slashes and backslashes in paths
    const cosmeticPattern = /^plugins\/[^/]+\/Decorations\/[^/]+\.hhh$/i;
    
    for (const [path, file] of Object.entries(zip.files)) {
      if (!file.dir && cosmeticPattern.test(path)) {
        try {
          const content = await file.async('uint8array');
          const metadata = extractCosmeticMetadata(path, content);
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
 * @param zipFiles - Array of objects containing path and data for each ZIP
 * @returns BatchScanResult with successful and failed scans
 */
export async function scanMultipleZips(
  zipFiles: Array<{ path: string; data: Buffer | Uint8Array }>
): Promise<BatchScanResult> {
  const result: BatchScanResult = {
    successful: [],
    failed: [],
    total: zipFiles.length,
    totalCosmetics: 0,
  };

  const scanPromises = zipFiles.map(async (zipFile) => {
    try {
      const scanResult = await scanZip(zipFile.data);
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
      const { zipPath, error } = settled.reason || {};
      result.failed.push({
        zipPath: zipPath || 'unknown',
        error: error || String(settled.reason),
      });
    }
  }

  return result;
}
