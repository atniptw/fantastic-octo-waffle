/**
 * Shared modules for the R.E.P.O. Cosmetic Catalog.
 * Re-exports all public APIs from the library modules.
 */

// Database wrapper with schema and migrations
export {
  DatabaseWrapper,
  SCHEMA,
  MIGRATIONS,
  getCurrentSchemaVersion,
  getPendingMigrations,
} from './database';

export type { Mod, Cosmetic, Migration } from './database';

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
