/**
 * SQLite schema definitions and migrations for the catalog database.
 * This module is separate from the database service layer to maintain
 * separation of concerns between schema management and data access.
 */

/**
 * SQLite schema definitions for the catalog database.
 * These will be used when migrating to actual SQLite storage.
 */
export const SCHEMA = {
  version: 2,
  tables: {
    mods: `
      CREATE TABLE IF NOT EXISTS mods (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_name TEXT NOT NULL,
        author TEXT NOT NULL,
        version TEXT NOT NULL,
        icon_path TEXT,
        source_zip TEXT NOT NULL UNIQUE,
        UNIQUE(mod_name, author, version)
      )
    `,
    cosmetics: `
      CREATE TABLE IF NOT EXISTS cosmetics (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_id INTEGER NOT NULL,
        display_name TEXT NOT NULL,
        filename TEXT NOT NULL,
        hash TEXT NOT NULL UNIQUE,
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
    cosmetics_mod_type_name: 'CREATE INDEX IF NOT EXISTS idx_cosmetics_mod_type_name ON cosmetics(mod_id, type, display_name)',
    cosmetics_hash: 'CREATE UNIQUE INDEX IF NOT EXISTS idx_cosmetics_hash ON cosmetics(hash)',
    mods_source_zip: 'CREATE INDEX IF NOT EXISTS idx_mods_source_zip ON mods(source_zip)',
    mods_name_author_version: 'CREATE UNIQUE INDEX IF NOT EXISTS idx_mods_name_author_version ON mods(mod_name, author, version)',
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
      // Create mods table without the unique constraint for version 1
      `CREATE TABLE IF NOT EXISTS mods (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_name TEXT NOT NULL,
        author TEXT NOT NULL,
        version TEXT NOT NULL,
        icon_path TEXT,
        source_zip TEXT NOT NULL UNIQUE
      )`,
      // Create cosmetics table without hash unique constraint for version 1
      `CREATE TABLE IF NOT EXISTS cosmetics (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mod_id INTEGER NOT NULL,
        display_name TEXT NOT NULL,
        filename TEXT NOT NULL,
        hash TEXT NOT NULL,
        type TEXT NOT NULL,
        internal_path TEXT NOT NULL,
        FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE CASCADE
      )`,
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
  {
    version: 2,
    description: 'Add unique constraints for duplicate detection',
    up: [
      // Add unique index on mods(mod_name, author, version) for duplicate detection
      SCHEMA.indexes.mods_name_author_version,
      // Add unique index on cosmetics(hash) for duplicate detection by SHA256
      SCHEMA.indexes.cosmetics_hash,
      // Add composite index on cosmetics for fast search/filter
      SCHEMA.indexes.cosmetics_mod_type_name,
      // Update schema version
      'UPDATE schema_version SET version = 2',
    ],
    down: [
      'DROP INDEX IF EXISTS idx_cosmetics_mod_type_name',
      'DROP INDEX IF EXISTS idx_cosmetics_hash',
      'DROP INDEX IF EXISTS idx_mods_name_author_version',
      'UPDATE schema_version SET version = 1',
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
