/**
 * Mod importer for scanning and processing Thunderstore mod ZIP files.
 */

import { DatabaseWrapper, Mod, Cosmetic } from './database';
import { createHash } from 'crypto';
import { scanZip, ZipScanResult } from './zipScanner';
import { readFile } from 'fs/promises';

export interface ManifestData {
  name: string;
  author: string;
  version_number: string;
  description?: string;
  dependencies?: string[];
}

export interface ImportResult {
  success: boolean;
  modId?: number;
  cosmeticsCount: number;
  error?: string;
}

/**
 * Result of importing a single mod ZIP file.
 */
export interface ModImportResult {
  /** Path to the ZIP file */
  zipPath: string;
  /** Whether the import was successful */
  success: boolean;
  /** ID of the mod (if imported or already exists) */
  modId?: number;
  /** Number of cosmetics imported */
  cosmeticsImported: number;
  /** Number of cosmetics that were duplicates (skipped) */
  cosmeticsDuplicate: number;
  /** Whether this mod was already imported (duplicate) */
  isDuplicate: boolean;
  /** Error message if import failed */
  error?: string;
  /** Warning messages (non-fatal issues) */
  warnings: string[];
}

/**
 * Aggregate result of importing multiple mod ZIP files.
 */
export interface BatchImportResult {
  /** Successfully imported mods */
  imported: ModImportResult[];
  /** Duplicate mods that were skipped */
  duplicates: ModImportResult[];
  /** Failed imports with errors */
  failed: ModImportResult[];
  /** Total number of ZIPs processed */
  totalZips: number;
  /** Total number of new mods imported */
  totalModsImported: number;
  /** Total number of new cosmetics imported */
  totalCosmeticsImported: number;
  /** Total number of duplicate cosmetics skipped */
  totalCosmeticsDuplicate: number;
}

/**
 * Activity log entry for tracking import actions.
 */
export interface ImportActivity {
  /** Timestamp of the activity */
  timestamp: Date;
  /** ZIP filename (not full path) */
  zipFilename: string;
  /** Number of cosmetics found in the ZIP */
  cosmeticsFound: number;
  /** Number of cosmetics actually imported */
  cosmeticsImported: number;
  /** Whether the import was successful */
  success: boolean;
  /** Error message if failed */
  error?: string;
  /** Warning messages */
  warnings: string[];
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
 * Extract cosmetic file info from a path.
 * Expected format: plugins/<plugin>/Decorations/*.hhh
 */
export function extractCosmeticInfo(path: string): {
  displayName: string;
  filename: string;
  type: string;
} | null {
  // Match paths like plugins/PluginName/Decorations/CosmeticName.hhh
  const match = path.match(
    /^plugins\/[^/]+\/Decorations\/([^/]+)\.hhh$/i
  );
  if (!match) {
    return null;
  }

  const filename = match[1] + '.hhh';
  // Convert filename to display name (e.g., "my_cosmetic_head" -> "My Cosmetic Head")
  const displayName = match[1]
    .replace(/[_-]/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());

  return {
    displayName,
    filename,
    type: 'decoration',
  };
}

/**
 * Calculate SHA-256 hash for content.
 * Used for reliable file identification and duplicate detection.
 */
export function calculateHash(content: Uint8Array): string {
  return createHash('sha256').update(content).digest('hex');
}

/**
 * ModImporter class for processing mod ZIP files.
 */
export class ModImporter {
  private db: DatabaseWrapper;
  private activityLog: ImportActivity[] = [];

  constructor(db: DatabaseWrapper) {
    this.db = db;
  }

  /**
   * Import multiple mod ZIP files in a single operation.
   * Processes all ZIPs with duplicate detection and comprehensive error handling.
   * Each ZIP is processed independently - failures don't affect other ZIPs.
   *
   * @param zipPaths Array of paths to ZIP files to import
   * @returns BatchImportResult with detailed results for each ZIP
   */
  async importModZips(zipPaths: string[]): Promise<BatchImportResult> {
    const result: BatchImportResult = {
      imported: [],
      duplicates: [],
      failed: [],
      totalZips: zipPaths.length,
      totalModsImported: 0,
      totalCosmeticsImported: 0,
      totalCosmeticsDuplicate: 0,
    };

    // Process each ZIP independently
    for (const zipPath of zipPaths) {
      try {
        const importResult = await this.importSingleZip(zipPath);
        
        // Categorize result
        if (importResult.success) {
          if (importResult.isDuplicate) {
            result.duplicates.push(importResult);
          } else {
            result.imported.push(importResult);
            result.totalModsImported++;
          }
          result.totalCosmeticsImported += importResult.cosmeticsImported;
          result.totalCosmeticsDuplicate += importResult.cosmeticsDuplicate;
        } else {
          result.failed.push(importResult);
        }

        // Log activity
        this.logActivity({
          timestamp: new Date(),
          zipFilename: this.getFilename(zipPath),
          cosmeticsFound: importResult.cosmeticsImported + importResult.cosmeticsDuplicate,
          cosmeticsImported: importResult.cosmeticsImported,
          success: importResult.success,
          error: importResult.error,
          warnings: importResult.warnings,
        });
      } catch (error) {
        // Unexpected error - add to failed results
        const errorResult: ModImportResult = {
          zipPath,
          success: false,
          cosmeticsImported: 0,
          cosmeticsDuplicate: 0,
          isDuplicate: false,
          error: `Unexpected error: ${error instanceof Error ? error.message : String(error)}`,
          warnings: [],
        };
        result.failed.push(errorResult);

        this.logActivity({
          timestamp: new Date(),
          zipFilename: this.getFilename(zipPath),
          cosmeticsFound: 0,
          cosmeticsImported: 0,
          success: false,
          error: errorResult.error,
          warnings: [],
        });
      }
    }

    return result;
  }

  /**
   * Import a single mod ZIP file.
   * Internal method used by importModZips().
   *
   * @param zipPath Path to the ZIP file
   * @returns ModImportResult with details of the import
   */
  private async importSingleZip(zipPath: string): Promise<ModImportResult> {
    const result: ModImportResult = {
      zipPath,
      success: false,
      cosmeticsImported: 0,
      cosmeticsDuplicate: 0,
      isDuplicate: false,
      warnings: [],
    };

    // Read ZIP file
    let zipData: Buffer;
    try {
      zipData = await readFile(zipPath);
    } catch (error) {
      result.error = `Failed to read ZIP file: ${error instanceof Error ? error.message : String(error)}`;
      return result;
    }

    // Scan ZIP
    let scanResult: ZipScanResult;
    try {
      scanResult = await scanZip(zipData);
    } catch (error) {
      result.error = `Failed to scan ZIP file: ${error instanceof Error ? error.message : String(error)}`;
      return result;
    }

    // Check for fatal scanning errors
    if (scanResult.hasFatalError) {
      result.error = scanResult.errors.join('; ') || 'Failed to parse ZIP file';
      return result;
    }

    // Validate manifest
    if (!scanResult.manifest) {
      result.error = 'Invalid or missing manifest.json';
      return result;
    }

    // Add non-fatal errors as warnings
    if (scanResult.errors.length > 0) {
      result.warnings.push(...scanResult.errors);
    }

    // Check for duplicate mod by source_zip
    const existingByZip = await this.db.getModBySourceZip(zipPath);
    if (existingByZip) {
      result.isDuplicate = true;
      result.success = true;
      result.modId = existingByZip.id;
      // Count cosmetics from the existing mod
      const existingCosmetics = await this.db.getCosmeticsByModId(existingByZip.id!);
      result.cosmeticsDuplicate = existingCosmetics.length;
      result.warnings.push('Mod with this ZIP path already imported');
      return result;
    }

    // Check for duplicate mod by identity (name, author, version)
    const existingByIdentity = await this.db.getModByIdentity(
      scanResult.manifest.name,
      scanResult.manifest.author,
      scanResult.manifest.version_number
    );
    if (existingByIdentity) {
      result.isDuplicate = true;
      result.success = true;
      result.modId = existingByIdentity.id;
      const existingCosmetics = await this.db.getCosmeticsByModId(existingByIdentity.id!);
      result.cosmeticsDuplicate = existingCosmetics.length;
      result.warnings.push('Mod with this name, author, and version already imported from a different ZIP');
      return result;
    }

    // Import mod and cosmetics
    // Note: better-sqlite3 operations are actually synchronous despite async signatures,
    // and since we're doing sequential operations, we don't need explicit transaction wrapping
    try {
      // Insert mod
      const mod: Omit<Mod, 'id'> = {
        mod_name: scanResult.manifest!.name,
        author: scanResult.manifest!.author,
        version: scanResult.manifest!.version_number,
        icon_path: null, // Icon handling can be added later
        source_zip: zipPath,
      };
      const modId = await this.db.insertOrGetMod(mod);
      result.modId = modId;

      // Process cosmetics
      for (const cosmetic of scanResult.cosmetics) {
        // Check if cosmetic already exists by hash
        const existingCosmetic = await this.db.getCosmeticByHash(cosmetic.hash);
        if (existingCosmetic) {
          result.cosmeticsDuplicate++;
          continue;
        }

        // Insert new cosmetic
        const cosmeticData: Omit<Cosmetic, 'id'> = {
          mod_id: modId,
          display_name: cosmetic.displayName,
          filename: cosmetic.filename,
          hash: cosmetic.hash,
          type: cosmetic.type,
          internal_path: cosmetic.internalPath,
        };
        await this.db.insertCosmetic(cosmeticData);
        result.cosmeticsImported++;
      }

      result.success = true;
      
      if (result.cosmeticsDuplicate > 0) {
        result.warnings.push(`${result.cosmeticsDuplicate} duplicate cosmetic(s) skipped`);
      }
    } catch (error) {
      result.error = `Failed to save to database: ${error instanceof Error ? error.message : String(error)}`;
      return result;
    }

    return result;
  }

  /**
   * Import a mod from ZIP file data.
   * Returns import result with success status and counts.
   * 
   * @deprecated Use importModZips() for better duplicate detection and error handling
   */
  async importMod(
    zipPath: string,
    manifestContent: string,
    cosmeticFiles: Map<string, Uint8Array>,
    iconPath: string | null = null
  ): Promise<ImportResult> {
    // Check if mod already exists
    if (await this.db.modExists(zipPath)) {
      return {
        success: false,
        cosmeticsCount: 0,
        error: 'Mod already imported',
      };
    }

    // Parse manifest
    const manifest = parseManifest(manifestContent);
    if (!manifest) {
      return {
        success: false,
        cosmeticsCount: 0,
        error: 'Invalid manifest.json',
      };
    }

    // Insert mod
    const mod: Omit<Mod, 'id'> = {
      mod_name: manifest.name,
      author: manifest.author,
      version: manifest.version_number,
      icon_path: iconPath,
      source_zip: zipPath,
    };
    const modId = await this.db.insertMod(mod);

    // Process cosmetic files
    let cosmeticsCount = 0;
    for (const [filePath, content] of cosmeticFiles) {
      const info = extractCosmeticInfo(filePath);
      if (info) {
        const cosmetic: Omit<Cosmetic, 'id'> = {
          mod_id: modId,
          display_name: info.displayName,
          filename: info.filename,
          hash: calculateHash(content),
          type: info.type,
          internal_path: filePath,
        };
        await this.db.insertCosmetic(cosmetic);
        cosmeticsCount++;
      }
    }

    return {
      success: true,
      modId,
      cosmeticsCount,
    };
  }

  /**
   * Get the activity log for all imports performed by this importer instance.
   */
  getActivityLog(): ImportActivity[] {
    return [...this.activityLog];
  }

  /**
   * Clear the activity log.
   */
  clearActivityLog(): void {
    this.activityLog = [];
  }

  /**
   * Get import statistics.
   */
  async getStats(): Promise<{ modCount: number; cosmeticCount: number }> {
    return {
      modCount: await this.db.getModCount(),
      cosmeticCount: await this.db.getCosmeticCount(),
    };
  }

  /**
   * Extract filename from a full path.
   */
  private getFilename(path: string): string {
    return path.replace(/\\/g, '/').split('/').pop() || path;
  }

  /**
   * Log an import activity.
   */
  private logActivity(activity: ImportActivity): void {
    this.activityLog.push(activity);
  }
}
