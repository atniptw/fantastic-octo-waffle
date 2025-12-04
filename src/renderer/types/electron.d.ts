// Type declarations for Electron API exposed via preload script
// Re-export shared types for renderer consumers
export type {
  ImportLogEntry,
  ImportFilesResult,
  Mod,
  Cosmetic,
  CatalogData,
  ElectronAPI,
} from '../../shared/types';

import type { ElectronAPI } from '../../shared/types';

declare global {
  interface Window {
    electronAPI?: ElectronAPI;
  }
}

export {};
