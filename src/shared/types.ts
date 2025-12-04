/**
 * Shared type definitions for IPC communication between main and renderer processes.
 * This file is the single source of truth for all shared types.
 */

export interface ImportLogEntry {
  timestamp: string;
  filename: string;
  status: 'success' | 'warning' | 'error' | 'info';
  message: string;
}

export interface ImportFilesResult {
  logs: ImportLogEntry[];
  totalFiles: number;
  successCount: number;
  errorCount: number;
  warningCount: number;
}

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

export interface CatalogData {
  mods: Mod[];
  cosmetics: Cosmetic[];
}

export interface ElectronAPI {
  selectZipFiles: () => Promise<string[] | null>;
  importZipFiles: (filePaths: string[]) => Promise<ImportFilesResult>;
  getCatalog: () => Promise<CatalogData>;
  searchCosmetics: (query: string) => Promise<Cosmetic[]>;
  importMods: () => Promise<{ success: boolean; message: string }>;
}
