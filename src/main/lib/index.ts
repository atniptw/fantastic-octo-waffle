/**
 * Shared modules for the R.E.P.O. Cosmetic Catalog.
 * Re-exports all public APIs from the library modules.
 */

// Database wrapper (service layer for data access)
export { DatabaseWrapper } from './database';

export type { Mod, Cosmetic, CosmeticFilters } from './database';

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

export type {
  ManifestData,
  ImportResult,
  ModImportResult,
  BatchImportResult,
  ImportActivity,
} from './importer';

// ZIP scanner
export { scanZip, isValidScanResult, getCosmeticPaths } from './zipScanner';

export type { ZipScanResult } from './zipScanner';
