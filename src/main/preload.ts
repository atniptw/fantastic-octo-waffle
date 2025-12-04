import { contextBridge, ipcRenderer } from 'electron';
import type {
  ImportLogEntry,
  ImportFilesResult,
  Mod,
  Cosmetic,
  CatalogData,
} from '../shared/types';

// Re-export types for consumers of this module
export type { ImportLogEntry, ImportFilesResult, Mod, Cosmetic, CatalogData };

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
