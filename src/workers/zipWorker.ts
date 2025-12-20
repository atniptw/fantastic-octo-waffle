/**
 * Web Worker for processing ZIP files in the background.
 * This keeps the main thread responsive while extracting large ZIP files.
 *
 * Note: Progress reporting currently uses approximated values rather than
 * actual file processing counts. The progress jumps from 10% to 90% when
 * scanning completes, then to 100% when results are sent. For smoother
 * progress tracking, consider implementing granular progress based on the
 * number of files processed within the ZIP.
 */

import { PROGRESS } from '@/lib/constants';
import { scanZip, type WorkerMessage, type ZipScanResult } from '../lib/zipScanner';

// Handle messages from the main thread
self.onmessage = async (event: MessageEvent<WorkerMessage>) => {
  const { type, file, fileName, scanId } = event.data;

  if (type === 'scan' && file) {
    try {
      // Report initial progress (approximation)
      postProgress(PROGRESS.SCAN_START, scanId);

      // Scan the ZIP file
      const result: ZipScanResult = await scanZip(file);

      // Report near-complete progress (approximation)
      postProgress(PROGRESS.SCAN_COMPLETE, scanId);

      // Send result back to main thread
      const response: WorkerMessage = {
        type: 'result',
        fileName,
        result,
        scanId,
      };
      self.postMessage(response);

      // Report completion
      postProgress(PROGRESS.RESULT_SENT, scanId);
    } catch (error) {
      // Send error back to main thread
      const response: WorkerMessage = {
        type: 'error',
        fileName,
        error: error instanceof Error ? error.message : String(error),
        scanId,
      };
      self.postMessage(response);
    }
  }
};

function postProgress(progress: number, scanId?: number) {
  const response: WorkerMessage = {
    type: 'progress',
    progress,
    scanId,
  };
  self.postMessage(response);
}
