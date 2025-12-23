/**
 * Core pipeline utilities - scanner, cosmetic processing
 */

export {
  scanZip,
  scanZipFile,
  scanMultipleZipFiles,
  parseManifest,
  isValidScanResult,
  getCosmeticPaths,
  calculateFileHash,
  extractCosmeticMetadata,
} from './scanner';

export {
  generateDisplayName,
  inferCosmeticType,
  convertHhhToImage,
  createPlaceholderImage,
} from './cosmetic';

export type {
  CosmeticMetadata,
  ManifestData,
  ZipScanResult,
  ScanProgressUpdate,
  ZipScanOptions,
  BatchScanResult,
  WorkerMessage,
} from './scanner';
