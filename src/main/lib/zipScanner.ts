/**
 * ZIP Scanner module for scanning Thunderstore mod ZIP files.
 * Extracts manifest.json, icon.png, and .hhh cosmetic files.
 */

import JSZip from 'jszip';
import { ManifestData, parseManifest } from './importer';

export interface ZipScanResult {
  manifestContent: string | null;
  manifest: ManifestData | null;
  iconData: Uint8Array | null;
  cosmeticFiles: Map<string, Uint8Array>;
  errors: string[];
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
    cosmeticFiles: new Map(),
    errors: [],
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
    const cosmeticPattern = /^plugins\/[^/]+\/Decorations\/[^/]+\.hhh$/i;
    
    for (const [path, file] of Object.entries(zip.files)) {
      if (!file.dir && cosmeticPattern.test(path)) {
        try {
          const content = await file.async('uint8array');
          result.cosmeticFiles.set(path, content);
        } catch (e) {
          result.errors.push(`Error reading ${path}: ${e instanceof Error ? e.message : String(e)}`);
        }
      }
    }
  } catch (e) {
    result.errors.push(`Error parsing ZIP file: ${e instanceof Error ? e.message : String(e)}`);
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
  return Array.from(scanResult.cosmeticFiles.keys());
}
