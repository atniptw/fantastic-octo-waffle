import { contextBridge, ipcRenderer } from 'electron';

// Type definitions for IPC results
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

// Expose protected methods that allow the renderer process to use
// the ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('electronAPI', {
  // Open file dialog to select ZIP files
  selectZipFiles: (): Promise<string[] | null> => 
    ipcRenderer.invoke('select-zip-files'),
  
  // Import selected ZIP files
  importZipFiles: (filePaths: string[]): Promise<ImportFilesResult> => 
    ipcRenderer.invoke('import-zip-files', filePaths),
  
  // Get catalog data (mods and cosmetics)
  getCatalog: (): Promise<CatalogData> => 
    ipcRenderer.invoke('get-catalog'),
  
  // Search cosmetics by query
  searchCosmetics: (query: string): Promise<Cosmetic[]> => 
    ipcRenderer.invoke('search-cosmetics', query),
  
  // Legacy - kept for backward compatibility
  importMods: () => ipcRenderer.invoke('import-mods'),
});
