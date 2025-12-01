import { app, BrowserWindow, ipcMain } from 'electron';
import path from 'path';

// Stub IPC handler for import-mods - logs a message until full implementation
ipcMain.handle('import-mods', async () => {
  console.log('[IPC] import-mods invoked - functionality not yet implemented');
  return { success: false, message: 'Import functionality not yet implemented' };
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
