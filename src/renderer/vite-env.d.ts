/// <reference types="vite/client" />

interface ElectronAPI {
  importMods: () => Promise<void>;
}

declare global {
  interface Window {
    electronAPI: ElectronAPI;
  }
}
