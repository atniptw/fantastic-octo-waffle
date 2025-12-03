/**
 * Shared modules for the R.E.P.O. Cosmetic Catalog.
 * Re-exports all public APIs from the library modules.
 */

// Database wrapper (service layer for data access)
export { DatabaseWrapper } from './database';

export type { Mod, Cosmetic } from './database';

// Schema and migrations (separate from service layer)
export {
  SCHEMA,
  MIGRATIONS,
  getCurrentSchemaVersion,
  getPendingMigrations,
} from './schema';

export type { Migration } from './schema';

// ZIP importer and helpers
export {
  ModImporter,
  parseManifest,
  extractCosmeticInfo,
  calculateHash,
} from './importer';

export type { ManifestData, ImportResult } from './importer';

// ZIP scanner
export { scanZip, isValidScanResult, getCosmeticPaths } from './zipScanner';

export type { ZipScanResult } from './zipScanner';
