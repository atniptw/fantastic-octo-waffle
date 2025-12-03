/**
 * Mod importer for scanning and processing Thunderstore mod ZIP files.
 */

import { DatabaseWrapper, Mod, Cosmetic } from './database';
import { createHash } from 'crypto';

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

  constructor(db: DatabaseWrapper) {
    this.db = db;
  }

  /**
   * Import a mod from ZIP file data.
   * Returns import result with success status and counts.
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
   * Get import statistics.
   */
  async getStats(): Promise<{ modCount: number; cosmeticCount: number }> {
    return {
      modCount: await this.db.getModCount(),
      cosmeticCount: await this.db.getCosmeticCount(),
    };
  }
}
