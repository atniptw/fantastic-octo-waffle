/**
 * Shared, browser-friendly types used across the app.
 */

export interface ImportLogEntry {
  timestamp: string;
  filename: string;
  status: 'success' | 'warning' | 'error' | 'info';
  message: string;
}

export interface Mod {
  id: string;
  mod_name: string;
  author: string;
  version: string;
  iconData: string | null;
  source: string;
}

export interface Cosmetic {
  id: string;
  mod_id: string;
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

export interface ImportFilesResult {
  logs: ImportLogEntry[];
  totalFiles: number;
  successCount: number;
  errorCount: number;
  warningCount: number;
  mods: Mod[];
  cosmetics: Cosmetic[];
}
