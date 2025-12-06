/**
 * Database wrapper for SQLite operations.
 * Provides an interface for mod and cosmetic data storage.
 *
 * Note: Schema definitions and migrations are in a separate module (schema.ts)
 * to maintain separation of concerns between schema management and data access.
 */

import Database, { type Database as DatabaseType } from 'better-sqlite3';
import { app } from 'electron';
import path from 'path';
import { getPendingMigrations } from './schema';

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
 * Filter options for querying cosmetics.
 */
export interface CosmeticFilters {
  /** Search by display name (partial match) */
  display_name?: string;
  /** Filter by cosmetic type */
  type?: string;
  /** Filter by mod ID */
  mod_id?: number;
  /** Free text search across display_name, filename, and type */
  text?: string;
}

/**
 * Escape special SQL LIKE wildcard characters (%, _, \) in user input.
 * This ensures that these characters are treated as literals rather than wildcards.
 * The backslash must be escaped first to avoid double-escaping.
 */
function escapeLikePattern(str: string): string {
  return str.replace(/\\/g, '\\\\').replace(/[%_]/g, '\\$&');
}

/**
 * SQLite database wrapper providing typed methods for mod and cosmetic operations.
 * Supports migrations, transactions, and error handling.
 */
export class DatabaseWrapper {
  private db: DatabaseType | null = null;
  private dbPath: string;
  private initialized = false;

  /**
   * Create a new database wrapper.
   * @param dbPath Optional path to database file. Defaults to app data directory.
   *               Use ':memory:' for in-memory database (testing).
   */
  constructor(dbPath?: string) {
    if (dbPath) {
      this.dbPath = dbPath;
    } else {
      // Use electron app data path for persistent storage
      try {
        const userDataPath = app.getPath('userData');
        this.dbPath = path.join(userDataPath, 'catalog.db');
      } catch {
        // Fallback for testing environment without electron
        this.dbPath = ':memory:';
      }
    }
  }

  /**
   * Initialize the database (create tables if they don't exist).
   * Applies all pending migrations idempotently.
   */
  async initialize(): Promise<void> {
    if (this.initialized && this.db) {
      return;
    }

    this.db = new Database(this.dbPath);
    this.db.pragma('journal_mode = WAL');
    this.db.pragma('foreign_keys = ON');

    // Run migrations
    await this.runMigrations();
    this.initialized = true;
  }

  /**
   * Run all pending migrations within a transaction.
   */
  private async runMigrations(): Promise<void> {
    if (!this.db) throw new Error('Database not opened');

    // Check if schema_version table exists
    const tableExists = this.db
      .prepare(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_version'"
      )
      .get();

    let currentVersion = 0;
    if (tableExists) {
      const row = this.db
        .prepare('SELECT version FROM schema_version LIMIT 1')
        .get() as { version: number } | undefined;
      currentVersion = row?.version ?? 0;
    }

    const pendingMigrations = getPendingMigrations(currentVersion);

    if (pendingMigrations.length === 0) {
      return;
    }

    // Run migrations in a transaction
    const runMigration = this.db.transaction(() => {
      for (const migration of pendingMigrations) {
        for (const sql of migration.up) {
          this.db!.exec(sql);
        }
      }
    });

    runMigration();
  }

  /**
   * Check if the database has been initialized.
   */
  isInitialized(): boolean {
    return this.initialized;
  }

  /**
   * Get the current schema version from the database.
   */
  getSchemaVersion(): number {
    if (!this.db) return 0;
    try {
      const row = this.db
        .prepare('SELECT version FROM schema_version LIMIT 1')
        .get() as { version: number } | undefined;
      return row?.version ?? 0;
    } catch {
      return 0;
    }
  }

  /**
   * Insert a new mod or return the existing mod's ID if it already exists.
   * Uniqueness is determined by (mod_name, author, version) combination.
   *
   * @param mod Mod metadata to insert
   * @returns The mod ID (either new or existing)
   */
  async insertOrGetMod(mod: Omit<Mod, 'id'>): Promise<number> {
    if (!this.db) throw new Error('Database not initialized');

    // First, check if mod already exists by (mod_name, author, version)
    const existing = this.db
      .prepare(
        'SELECT id FROM mods WHERE mod_name = ? AND author = ? AND version = ?'
      )
      .get(mod.mod_name, mod.author, mod.version) as { id: number } | undefined;

    if (existing) {
      return existing.id;
    }

    // Insert new mod
    const result = this.db
      .prepare(
        `INSERT INTO mods (mod_name, author, version, icon_path, source_zip)
         VALUES (?, ?, ?, ?, ?)`
      )
      .run(
        mod.mod_name,
        mod.author,
        mod.version,
        mod.icon_path,
        mod.source_zip
      );

    return Number(result.lastInsertRowid);
  }

  /**
   * Insert a new mod into the database.
   * @deprecated Use insertOrGetMod for duplicate detection
   */
  async insertMod(mod: Omit<Mod, 'id'>): Promise<number> {
    return this.insertOrGetMod(mod);
  }

  /**
   * Insert a new cosmetic or return the existing cosmetic's ID if it already exists.
   * Uniqueness is determined by SHA256 hash.
   *
   * @param cosmetic Cosmetic metadata to insert
   * @returns The cosmetic ID (either new or existing)
   */
  async insertCosmetic(cosmetic: Omit<Cosmetic, 'id'>): Promise<number> {
    if (!this.db) throw new Error('Database not initialized');

    // Check if cosmetic already exists by hash
    const existing = this.db
      .prepare('SELECT id FROM cosmetics WHERE hash = ?')
      .get(cosmetic.hash) as { id: number } | undefined;

    if (existing) {
      return existing.id;
    }

    // Insert new cosmetic
    const result = this.db
      .prepare(
        `INSERT INTO cosmetics (mod_id, display_name, filename, hash, type, internal_path)
         VALUES (?, ?, ?, ?, ?, ?)`
      )
      .run(
        cosmetic.mod_id,
        cosmetic.display_name,
        cosmetic.filename,
        cosmetic.hash,
        cosmetic.type,
        cosmetic.internal_path
      );

    return Number(result.lastInsertRowid);
  }

  /**
   * Get a mod by its source ZIP path.
   */
  async getModBySourceZip(sourceZip: string): Promise<Mod | null> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare('SELECT * FROM mods WHERE source_zip = ?')
      .get(sourceZip) as Mod | undefined;

    return row ?? null;
  }

  /**
   * Get a mod by (mod_name, author, version) combination.
   */
  async getModByIdentity(
    modName: string,
    author: string,
    version: string
  ): Promise<Mod | null> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare(
        'SELECT * FROM mods WHERE mod_name = ? AND author = ? AND version = ?'
      )
      .get(modName, author, version) as Mod | undefined;

    return row ?? null;
  }

  /**
   * Get all mods.
   */
  async getAllMods(): Promise<Mod[]> {
    if (!this.db) throw new Error('Database not initialized');

    return this.db.prepare('SELECT * FROM mods').all() as Mod[];
  }

  /**
   * Get all cosmetics for a specific mod.
   */
  async getCosmeticsByModId(modId: number): Promise<Cosmetic[]> {
    if (!this.db) throw new Error('Database not initialized');

    return this.db
      .prepare('SELECT * FROM cosmetics WHERE mod_id = ?')
      .all(modId) as Cosmetic[];
  }

  /**
   * Get a cosmetic by its SHA256 hash.
   */
  async getCosmeticByHash(hash: string): Promise<Cosmetic | null> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare('SELECT * FROM cosmetics WHERE hash = ?')
      .get(hash) as Cosmetic | undefined;

    return row ?? null;
  }

  /**
   * Get all cosmetics in the database.
   */
  async getAllCosmetics(): Promise<Cosmetic[]> {
    if (!this.db) throw new Error('Database not initialized');

    return this.db.prepare('SELECT * FROM cosmetics').all() as Cosmetic[];
  }

  /**
   * Query cosmetics with flexible filters.
   * Supports filtering by display_name, type, mod_id, and free text search.
   *
   * @param filters Filter options for the query
   * @returns Array of matching cosmetics
   */
  async queryCosmetics(filters: CosmeticFilters): Promise<Cosmetic[]> {
    if (!this.db) throw new Error('Database not initialized');

    const conditions: string[] = [];
    const params: (string | number)[] = [];

    if (filters.display_name) {
      conditions.push("display_name LIKE ? ESCAPE '\\'");
      params.push(`%${escapeLikePattern(filters.display_name)}%`);
    }

    if (filters.type) {
      conditions.push('type = ?');
      params.push(filters.type);
    }

    if (filters.mod_id !== undefined) {
      conditions.push('mod_id = ?');
      params.push(filters.mod_id);
    }

    if (filters.text) {
      conditions.push(
        "(display_name LIKE ? ESCAPE '\\' OR filename LIKE ? ESCAPE '\\' OR type LIKE ? ESCAPE '\\')"
      );
      const textPattern = `%${escapeLikePattern(filters.text)}%`;
      params.push(textPattern, textPattern, textPattern);
    }

    let sql = 'SELECT * FROM cosmetics';
    if (conditions.length > 0) {
      sql += ' WHERE ' + conditions.join(' AND ');
    }

    return this.db.prepare(sql).all(...params) as Cosmetic[];
  }

  /**
   * Search cosmetics by display name (legacy method for backward compatibility).
   * @deprecated Use queryCosmetics for more flexible searching
   */
  async searchCosmetics(query: string): Promise<Cosmetic[]> {
    return this.queryCosmetics({ display_name: query });
  }

  /**
   * Get total count of mods.
   */
  async getModCount(): Promise<number> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db.prepare('SELECT COUNT(*) as count FROM mods').get() as {
      count: number;
    };
    return row.count;
  }

  /**
   * Get total count of cosmetics.
   */
  async getCosmeticCount(): Promise<number> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare('SELECT COUNT(*) as count FROM cosmetics')
      .get() as { count: number };
    return row.count;
  }

  /**
   * Check if a mod with the given source ZIP already exists.
   */
  async modExists(sourceZip: string): Promise<boolean> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare('SELECT 1 FROM mods WHERE source_zip = ? LIMIT 1')
      .get(sourceZip);
    return row !== undefined;
  }

  /**
   * Check if a mod with the given identity (name, author, version) exists.
   */
  async modExistsByIdentity(
    modName: string,
    author: string,
    version: string
  ): Promise<boolean> {
    if (!this.db) throw new Error('Database not initialized');

    const row = this.db
      .prepare(
        'SELECT 1 FROM mods WHERE mod_name = ? AND author = ? AND version = ? LIMIT 1'
      )
      .get(modName, author, version);
    return row !== undefined;
  }

  /**
   * Execute multiple operations within a transaction.
   * If any operation fails, all changes are rolled back.
   *
   * @param operations Function containing database operations to execute
   * @returns Result of the operations function
   */
  async transaction<T>(operations: () => T): Promise<T> {
    if (!this.db) throw new Error('Database not initialized');

    const txn = this.db.transaction(operations);
    return txn();
  }

  /**
   * Close the database connection.
   */
  async close(): Promise<void> {
    if (this.db) {
      this.db.close();
      this.db = null;
      this.initialized = false;
    }
  }
}
