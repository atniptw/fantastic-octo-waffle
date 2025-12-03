/**
 * Database wrapper for SQLite operations.
 * Provides an interface for mod and cosmetic data storage.
 */

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
 * SQLite schema definitions for the catalog database.
 * These will be used when migrating to actual SQLite storage.
 */
export const SCHEMA = {
  version: 1,
  tables: {
    mods: `
      CREATE TABLE IF NOT EXISTS mods (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_name TEXT NOT NULL,
        author TEXT NOT NULL,
        version TEXT NOT NULL,
        icon_path TEXT,
        source_zip TEXT NOT NULL UNIQUE
      )
    `,
    cosmetics: `
      CREATE TABLE IF NOT EXISTS cosmetics (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_id INTEGER NOT NULL,
        display_name TEXT NOT NULL,
        filename TEXT NOT NULL,
        hash TEXT NOT NULL,
        type TEXT NOT NULL,
        internal_path TEXT NOT NULL,
        FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE CASCADE
      )
    `,
    schema_version: `
      CREATE TABLE IF NOT EXISTS schema_version (
        version INTEGER PRIMARY KEY
      )
    `,
  },
  indexes: {
    cosmetics_mod_id: 'CREATE INDEX IF NOT EXISTS idx_cosmetics_mod_id ON cosmetics(mod_id)',
    cosmetics_display_name: 'CREATE INDEX IF NOT EXISTS idx_cosmetics_display_name ON cosmetics(display_name)',
    mods_source_zip: 'CREATE INDEX IF NOT EXISTS idx_mods_source_zip ON mods(source_zip)',
  },
};

/**
 * Migration definitions for schema updates.
 * Each migration has a version number and SQL statements to execute.
 */
export interface Migration {
  version: number;
  description: string;
  up: string[];
  down: string[];
}

export const MIGRATIONS: Migration[] = [
  {
    version: 1,
    description: 'Initial schema creation',
    up: [
      SCHEMA.tables.schema_version,
      SCHEMA.tables.mods,
      SCHEMA.tables.cosmetics,
      SCHEMA.indexes.cosmetics_mod_id,
      SCHEMA.indexes.cosmetics_display_name,
      SCHEMA.indexes.mods_source_zip,
      'INSERT OR IGNORE INTO schema_version (version) VALUES (1)',
    ],
    down: [
      'DROP INDEX IF EXISTS idx_mods_source_zip',
      'DROP INDEX IF EXISTS idx_cosmetics_display_name',
      'DROP INDEX IF EXISTS idx_cosmetics_mod_id',
      'DROP TABLE IF EXISTS cosmetics',
      'DROP TABLE IF EXISTS mods',
      'DROP TABLE IF EXISTS schema_version',
    ],
  },
];

/**
 * Get the current schema version from migrations.
 */
export function getCurrentSchemaVersion(): number {
  return MIGRATIONS.length > 0 ? MIGRATIONS[MIGRATIONS.length - 1].version : 0;
}

/**
 * Get migrations that need to be applied to reach target version.
 */
export function getPendingMigrations(currentVersion: number, targetVersion: number = getCurrentSchemaVersion()): Migration[] {
  return MIGRATIONS.filter(m => m.version > currentVersion && m.version <= targetVersion);
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
