/**
 * Browser-compatible ZIP Scanner module for scanning Thunderstore mod ZIP files.
 * Extracts manifest.json, icon.png, and .hhh cosmetic files with comprehensive metadata.
 * Uses Web Crypto API instead of Node.js crypto for browser compatibility.
 * Uses JSZip for cross-platform ZIP extraction.
 */

import { debugLog, isDebugEnabled } from '../logger';
import JSZip from 'jszip';
import { generateDisplayName, inferCosmeticType } from './core/cosmetic';

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
  /** File size in bytes */
  size: number;
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
  errors: string[];
  /** True if there was a fatal error parsing the ZIP file */
  hasFatalError: boolean;
}

export interface ScanProgressUpdate {
  /** Percentage 0-100 */
  percent: number;
  /** Stage label */
  stage: 'prepare' | 'extract' | 'inspect' | 'complete';
  /** Optional human-readable detail */
  detail?: string;
  /** Processed files count (for inspect stage) */
  processed?: number;
  /** Total files (for inspect stage) */
  total?: number;
}

export interface ZipScanOptions {
  onProgress?: (progress: ScanProgressUpdate) => void;
  signal?: AbortSignal;
  debug?: boolean;
}

const DEBUG = isDebugEnabled();

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw new DOMException('Scan aborted', 'AbortError');
  }
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

// Re-export cosmetic utilities from core/cosmetic
export { generateDisplayName, inferCosmeticType } from './core/cosmetic';

/**
 * Calculates SHA256 hash for file content using Web Crypto API.
 * This is browser-compatible, unlike Node.js's crypto module.
 *
 * @param content - File content as Uint8Array
 * @returns Hex-encoded SHA256 hash
 */
export async function calculateFileHash(content: Uint8Array): Promise<string> {
  // Use Web Crypto API (available in browsers)
  // TypeScript's strict type checking requires assertion to handle ArrayBufferLike vs ArrayBuffer
  const hashBuffer = await crypto.subtle.digest('SHA-256', content as BufferSource);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  const hashHex = hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
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
    size: content.byteLength,
  };
}

/**
 * Scans a ZIP file and extracts relevant mod data.
 * Looks for:
 * - manifest.json at root
 * - icon.png at root
 * - *.hhh files anywhere in the ZIP (searches entire archive)
 *
 * This is the browser-compatible version that works with File objects.
 *
 * @param file - The ZIP file as a File or Blob object
 * @returns ZipScanResult containing extracted data and any errors
 */
export async function scanZipFile(file: File | Blob, options?: ZipScanOptions): Promise<ZipScanResult> {
  // Read the file as ArrayBuffer using FileReader for better compatibility
  const arrayBuffer = await new Promise<ArrayBuffer>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(file);
  });

  const zipData = new Uint8Array(arrayBuffer);

  return scanZip(zipData, options);
}

/**
 * Scans a ZIP file buffer and extracts relevant mod data.
 * Looks for:
 * - manifest.json at root
 * - icon.png at root
 * - *.hhh files anywhere in the ZIP (searches entire archive)
 *
 * @param zipData - The ZIP file as a Uint8Array or ArrayBuffer
 * @returns ZipScanResult containing extracted data and any errors
 */
export async function scanZip(
  zipData: Uint8Array | ArrayBuffer,
  options?: ZipScanOptions
): Promise<ZipScanResult> {
  const result: ZipScanResult = {
    manifestContent: null,
    manifest: null,
    iconData: null,
    cosmetics: [],
    errors: [],
    hasFatalError: false,
  };

  const reportProgress = (percent: number, stage: ScanProgressUpdate['stage'], detail?: string, processed?: number, total?: number) => {
    options?.onProgress?.({ percent, stage, detail, processed, total });
  };

  const debugEnabled = options?.debug ?? DEBUG;

  try {
    throwIfAborted(options?.signal);

    // Convert to Uint8Array if needed
    const data = zipData instanceof ArrayBuffer ? new Uint8Array(zipData) : zipData;

    reportProgress(2, 'prepare', 'Loading ZIP file');

    // Load and parse ZIP file
    const zip = new JSZip();
    await zip.loadAsync(data);

    reportProgress(25, 'extract', 'Archive loaded');

    // Get list of all files
    const files: { name: string; file: JSZip.JSZipObject }[] = [];
    zip.forEach((relativePath, file) => {
      if (!file.dir) {
        files.push({ name: relativePath, file });
      }
    });

    if (debugEnabled) {
      debugLog(`[ZIP Scanner] Loaded ZIP with ${files.length} files`);
    }

    if (files.length === 0) {
      result.errors.push('No files found in archive');
      result.hasFatalError = true;
      return result;
    }

    reportProgress(30, 'inspect', 'Scanning files', 0, files.length);

    // Extract manifest.json
    const manifestFile = files.find(
      (f) => f.name === 'manifest.json' || f.name.endsWith('/manifest.json')
    );
    if (manifestFile) {
      try {
        const manifestData = await manifestFile.file.async('string');
        result.manifestContent = manifestData;
        result.manifest = parseManifest(manifestData);
        if (!result.manifest) {
          result.errors.push('Invalid manifest.json format');
        }
      } catch (e) {
        result.errors.push(
          `Error reading manifest.json: ${e instanceof Error ? e.message : String(e)}`
        );
      }
    } else {
      result.errors.push('manifest.json not found');
    }

    // Extract icon.png
    const iconFile = files.find(
      (f) => f.name === 'icon.png' || f.name.endsWith('/icon.png')
    );
    if (iconFile) {
      try {
        result.iconData = await iconFile.file.async('uint8array');
      } catch (e) {
        result.errors.push(`Error reading icon.png: ${e instanceof Error ? e.message : String(e)}`);
      }
    }

    // Extract ALL .hhh files from anywhere in the ZIP
    // Note: Not all mods follow the plugins/<plugin>/Decorations/ convention
    const cosmeticPattern = /\.hhh$/i;

    if (debugEnabled) {
      debugLog(`[ZIP Scanner] Scanning for .hhh files in ${files.length} files`);
    }

    for (let idx = 0; idx < files.length; idx++) {
      const { name: filePath, file } = files[idx];
      throwIfAborted(options?.signal);
      if (cosmeticPattern.test(filePath)) {
        if (debugEnabled) {
          debugLog('[ZIP Scanner] Found .hhh file:', filePath);
        }
        try {
          const content = await file.async('uint8array');
          const metadata = await extractCosmeticMetadata(filePath, content);
          result.cosmetics.push(metadata);
        } catch (e) {
          result.errors.push(
            `Error reading ${filePath}: ${e instanceof Error ? e.message : String(e)}`
          );
        }
      }

      const percent = 30 + Math.min(60, Math.floor(((idx + 1) / files.length) * 60));
      reportProgress(percent, 'inspect', filePath, idx + 1, files.length);
    }

    if (debugEnabled) {
      debugLog(`[ZIP Scanner] Found ${result.cosmetics.length} cosmetic files`);
    }
    reportProgress(95, 'inspect', 'Metadata collected', files.length, files.length);
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      throw e;
    }
    result.errors.push(`Error parsing ZIP file: ${e instanceof Error ? e.message : String(e)}`);
    result.hasFatalError = true;
  }

  reportProgress(100, 'complete', 'Scan complete');
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
  return scanResult.cosmetics.map((c) => c.internalPath);
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
  type: 'scan' | 'result' | 'error' | 'progress' | 'cancel';
  file?: ArrayBuffer;
  fileName?: string;
  result?: ZipScanResult;
  error?: string;
  progress?: number;
  stage?: ScanProgressUpdate['stage'];
  detail?: string;
  processed?: number;
  total?: number;
  scanId?: number;
}
