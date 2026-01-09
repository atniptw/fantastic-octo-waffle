/**
 * Pipeline package for mod analysis
 *
 * PRIMARY API (what you should use):
 * import { useModAnalyzer } from '@/lib/pipeline';
 *
 * ADVANCED APIs (for special cases):
 * import { useScanWorker, useZipDownloader } from '@/lib/pipeline';
 * import { scanZip, generateDisplayName } from '@/lib/pipeline';
 */

// Primary high-level API
export { useModAnalyzer } from './hooks/useModAnalyzer';
export type {
  AnalyzeOptions,
  AnalyzeProgress,
  AnalyzeResult,
  CosmeticPreview,
  UseModAnalyzerResult,
} from './types';

// Advanced APIs - hooks
export {
  useScanWorker,
  useZipScanner,
  useZipDownloader,
} from './hooks';

export type {
  ScanProgress,
  ScanError,
  UseZipScannerResult,
  DownloadProgress,
  UseZipDownloaderResult,
} from './hooks';

// Advanced APIs - core functions
export {
  scanZip,
  scanZipFile,
  scanMultipleZipFiles,
  generateDisplayName,
  inferCosmeticType,
  convertHhhToImage,
  parseManifest,
  isValidScanResult,
  getCosmeticPaths,
  calculateFileHash,
  extractCosmeticMetadata,
} from './core';

export type {
  CosmeticMetadata,
  ManifestData,
  ZipScanResult,
  ScanProgressUpdate,
  ZipScanOptions,
  BatchScanResult,
  WorkerMessage,
} from './core';

// All types from types.ts are already exported above
export * from './types';
