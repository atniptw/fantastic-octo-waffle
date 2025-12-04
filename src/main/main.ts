import { app, BrowserWindow, ipcMain, dialog } from 'electron';
import path from 'path';
import fs from 'fs/promises';
import { scanZip, isValidScanResult } from './lib/zipScanner';
import { DatabaseWrapper, ModImporter } from './lib';
import type { ImportLogEntry, ImportFilesResult } from '../shared/types';

// Initialize database
const db = new DatabaseWrapper();
let initPromise: Promise<void> | null = null;

async function ensureDbInitialized(): Promise<void> {
  if (!initPromise) {
    initPromise = db.initialize();
  }
  return initPromise;
}

// IPC handler to open file dialog and select ZIP files
ipcMain.handle('select-zip-files', async (): Promise<string[] | null> => {
  const result = await dialog.showOpenDialog({
    title: 'Select Mod ZIP Files',
    filters: [{ name: 'ZIP Archives', extensions: ['zip'] }],
    properties: ['openFile', 'multiSelections'],
  });

  if (result.canceled || result.filePaths.length === 0) {
    return null;
  }

  return result.filePaths;
});

// IPC handler to import selected ZIP files
ipcMain.handle('import-zip-files', async (_event, filePaths: string[]): Promise<ImportFilesResult> => {
  await ensureDbInitialized();
  
  const importer = new ModImporter(db);
  const logs: ImportLogEntry[] = [];
  let successCount = 0;
  let errorCount = 0;
  let warningCount = 0;

  for (const filePath of filePaths) {
    const filename = path.basename(filePath);
    const timestamp = new Date().toISOString();

    try {
      // Read ZIP file
      const zipData = await fs.readFile(filePath);
      
      // Scan the ZIP
      const scanResult = await scanZip(zipData);
      
      // Check for scan errors (corrupt files, etc.)
      if (scanResult.errors.length > 0) {
        for (const error of scanResult.errors) {
          logs.push({
            timestamp,
            filename,
            status: 'warning',
            message: error,
          });
          warningCount++;
        }
      }

      // Validate scan result
      if (!isValidScanResult(scanResult)) {
        logs.push({
          timestamp,
          filename,
          status: 'error',
          message: 'Invalid mod structure - manifest.json not found or invalid',
        });
        errorCount++;
        continue;
      }

      // Import the mod
      const importResult = await importer.importMod(
        filePath,
        scanResult.manifestContent!,
        scanResult.cosmeticFiles,
        scanResult.iconData ? `icon_${Date.now()}.png` : null
      );

      if (importResult.success) {
        logs.push({
          timestamp,
          filename,
          status: 'success',
          message: `Imported successfully: ${importResult.cosmeticsCount} cosmetic(s) found`,
        });
        successCount++;
      } else {
        logs.push({
          timestamp,
          filename,
          status: importResult.error === 'Mod already imported' ? 'warning' : 'error',
          message: importResult.error || 'Unknown error',
        });
        if (importResult.error === 'Mod already imported') {
          warningCount++;
        } else {
          errorCount++;
        }
      }
    } catch (error) {
      logs.push({
        timestamp,
        filename,
        status: 'error',
        message: `Failed to process: ${error instanceof Error ? error.message : String(error)}`,
      });
      errorCount++;
    }
  }

  return {
    logs,
    totalFiles: filePaths.length,
    successCount,
    errorCount,
    warningCount,
  };
});

// IPC handler to get catalog data
ipcMain.handle('get-catalog', async () => {
  await ensureDbInitialized();
  const mods = await db.getAllMods();
  const cosmetics = await db.getAllCosmetics();
  return { mods, cosmetics };
});

// IPC handler to search cosmetics
ipcMain.handle('search-cosmetics', async (_event, query: string) => {
  await ensureDbInitialized();
  return await db.searchCosmetics(query);
});

// Legacy handler - kept for backward compatibility
ipcMain.handle('import-mods', async () => {
  console.log('[IPC] import-mods invoked - use select-zip-files and import-zip-files instead');
  return { success: false, message: 'Use select-zip-files and import-zip-files handlers instead' };
});

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    title: 'R.E.P.O. Cosmetic Catalog',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  // In development, load from Vite dev server
  // In production, load from dist/renderer
  const isDev = !app.isPackaged;
  if (isDev) {
    const devServerPort = process.env.VITE_DEV_SERVER_PORT || process.env.VITE_PORT || '5173';
    mainWindow.loadURL(`http://localhost:${devServerPort}`);
    mainWindow.webContents.openDevTools();
  } else {
    mainWindow.loadFile(path.join(__dirname, '../renderer/index.html'));
  }
}

app.whenReady().then(() => {
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
