/**
 * Database wrapper for SQLite operations.
 * Provides an interface for mod and cosmetic data storage.
 * 
 * Note: Schema definitions and migrations are in a separate module (schema.ts)
 * to maintain separation of concerns between schema management and data access.
 */

import { getCurrentSchemaVersion } from './schema';

export interface Mod {
  id?: number;
  mod_name: string;
  author: string;
  version: string;
  icon_path: string | null;
  source_zip: string;
}

export interface Cosmetic {
  id?: number;
  mod_id: number;
  display_name: string;
  filename: string;
  hash: string;
  type: string;
  internal_path: string;
}

/**
 * In-memory database implementation.
 * This is a temporary implementation that will be replaced with SQLite
 * for persistent storage. Currently used for development and testing.
 *
 * Limitations:
 * - Data is not persisted between sessions
 * - Memory usage grows linearly with records
 */
export class DatabaseWrapper {
  private mods: Mod[] = [];
  private cosmetics: Cosmetic[] = [];
  private modIdCounter = 1;
  private cosmeticIdCounter = 1;
  private schemaVersion = 0;
  private initialized = false;

  /**
   * Initialize the database (create tables if they don't exist).
   * Applies all pending migrations.
   */
  async initialize(): Promise<void> {
    // In production, this would create SQLite tables and run migrations
    this.mods = [];
    this.cosmetics = [];
    this.modIdCounter = 1;
    this.cosmeticIdCounter = 1;
    this.schemaVersion = getCurrentSchemaVersion();
    this.initialized = true;
  }

  /**
   * Check if the database has been initialized.
   */
  isInitialized(): boolean {
    return this.initialized;
  }

  /**
   * Get the current schema version.
   */
  getSchemaVersion(): number {
    return this.schemaVersion;
  }

  /**
   * Insert a new mod into the database.
   */
  async insertMod(mod: Omit<Mod, 'id'>): Promise<number> {
    const id = this.modIdCounter++;
    const newMod: Mod = {
      ...mod,
      id,
    };
    this.mods.push(newMod);
    return id;
  }

  /**
   * Insert a new cosmetic into the database.
   */
  async insertCosmetic(cosmetic: Omit<Cosmetic, 'id'>): Promise<number> {
    const id = this.cosmeticIdCounter++;
    const newCosmetic: Cosmetic = {
      ...cosmetic,
      id,
    };
    this.cosmetics.push(newCosmetic);
    return id;
  }

  /**
   * Get a mod by its source ZIP path.
   */
  async getModBySourceZip(sourceZip: string): Promise<Mod | null> {
    return this.mods.find((mod) => mod.source_zip === sourceZip) ?? null;
  }

  /**
   * Get all mods.
   */
  async getAllMods(): Promise<Mod[]> {
    return [...this.mods];
  }

  /**
   * Get all cosmetics for a specific mod.
   */
  async getCosmeticsByModId(modId: number): Promise<Cosmetic[]> {
    return this.cosmetics.filter((cosmetic) => cosmetic.mod_id === modId);
  }

  /**
   * Get all cosmetics in the database.
   */
  async getAllCosmetics(): Promise<Cosmetic[]> {
    return [...this.cosmetics];
  }

  /**
   * Search cosmetics by display name.
   */
  async searchCosmetics(query: string): Promise<Cosmetic[]> {
    const lowerQuery = query.toLowerCase();
    return this.cosmetics.filter((cosmetic) =>
      cosmetic.display_name.toLowerCase().includes(lowerQuery)
    );
  }

  /**
   * Get total count of mods.
   */
  async getModCount(): Promise<number> {
    return this.mods.length;
  }

  /**
   * Get total count of cosmetics.
   */
  async getCosmeticCount(): Promise<number> {
    return this.cosmetics.length;
  }

  /**
   * Check if a mod with the given source ZIP already exists.
   */
  async modExists(sourceZip: string): Promise<boolean> {
    return this.mods.some((mod) => mod.source_zip === sourceZip);
  }

  /**
   * Close the database connection.
   */
  async close(): Promise<void> {
    // In production, this would close the SQLite connection
  }
}
