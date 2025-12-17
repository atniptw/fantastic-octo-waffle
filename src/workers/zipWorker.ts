/**
 * Web Worker for processing ZIP files in the background.
 * This keeps the main thread responsive while extracting large ZIP files.
 */

import { scanZip, type WorkerMessage, type ZipScanResult } from '../lib/zipScanner';

// Handle messages from the main thread
self.onmessage = async (event: MessageEvent<WorkerMessage>) => {
  const { type, file, fileName } = event.data;

  if (type === 'scan' && file) {
    try {
      // Report progress
      postProgress(10);

      // Scan the ZIP file
      const result: ZipScanResult = await scanZip(file);

      // Report progress
      postProgress(90);

      // Send result back to main thread
      const response: WorkerMessage = {
        type: 'result',
        fileName,
        result,
      };
      self.postMessage(response);

      postProgress(100);
    } catch (error) {
      // Send error back to main thread
      const response: WorkerMessage = {
        type: 'error',
        fileName,
        error: error instanceof Error ? error.message : String(error),
      };
      self.postMessage(response);
    }
  }
};

function postProgress(progress: number) {
  const response: WorkerMessage = {
    type: 'progress',
    progress,
  };
  self.postMessage(response);
}
