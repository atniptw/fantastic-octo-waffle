/**
 * Web Worker for processing ZIP files in the background.
 * This keeps the main thread responsive while extracting large ZIP files.
 *
 * Progress reporting streams the underlying scan progress reported by
 * sevenzip/inspection rather than fixed jumps.
 */

import { scanZip, type WorkerMessage, type ZipScanResult } from '../lib/pipeline/core';

const abortControllers = new Map<number, AbortController>();

// Handle messages from the main thread
self.onmessage = async (event: MessageEvent<WorkerMessage>) => {
  const { type, file, fileName, scanId } = event.data;

  if (type === 'cancel' && typeof scanId === 'number') {
    const controller = abortControllers.get(scanId);
    controller?.abort();
    abortControllers.delete(scanId);
    return;
  }

  if (type === 'scan' && file) {
    const abortController = new AbortController();
    if (typeof scanId === 'number') {
      abortControllers.set(scanId, abortController);
    }

    try {
      // Scan the ZIP file with progress passthrough
      const result: ZipScanResult = await scanZip(file, {
        signal: abortController.signal,
        onProgress: ({ percent, stage, detail, processed, total }) => {
          postProgress(percent, scanId, stage, detail, processed, total);
        },
      });

      // Send result back to main thread
      const response: WorkerMessage = {
        type: 'result',
        fileName,
        result,
        scanId,
      };
      const transfer: Transferable[] = [];
      if (result.iconData) {
        transfer.push(result.iconData.buffer as ArrayBuffer);
      }
      self.postMessage(response, { transfer });

      postProgress(100, scanId, 'complete', 'done');
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

    if (typeof scanId === 'number') {
      abortControllers.delete(scanId);
    }
  }
};

function postProgress(
  progress: number,
  scanId?: number,
  stage?: WorkerMessage['stage'],
  detail?: string,
  processed?: number,
  total?: number
) {
  const response: WorkerMessage = {
    type: 'progress',
    progress,
    stage,
    detail,
    processed,
    total,
    scanId,
  };
  self.postMessage(response);
}
