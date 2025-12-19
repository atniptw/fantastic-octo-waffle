/**
 * React hook for using the ZIP scanner Web Worker.
 * Provides a clean API for scanning ZIP files without blocking the UI.
 */

import { useRef, useCallback, useState, useEffect } from 'react';
import type { ZipScanResult, WorkerMessage } from './zipScanner';

export interface ScanProgress {
  fileName: string;
  progress: number;
  stage?: WorkerMessage['stage'];
  detail?: string;
  processed?: number;
  total?: number;
}

export interface ScanError {
  fileName: string;
  error: string;
}

export interface UseZipScannerResult {
  scanFile: (
    file: File | { buffer: ArrayBuffer; fileName: string },
    options?: {
      onProgress?: (progress: ScanProgress) => void;
      onComplete?: (result: ZipScanResult, fileName: string) => void;
      onError?: (error: ScanError) => void;
      signal?: AbortSignal;
    }
  ) => Promise<ZipScanResult>;
  isScanning: boolean;
  cancelScan: (scanId?: number) => void;
}

/**
 * Hook for scanning ZIP files using a Web Worker.
 * Handles worker lifecycle and provides a promise-based API.
 * Tracks concurrent scans and properly cleans up the worker on unmount.
 *
 * @example
 * ```tsx
 * const { scanFile, isScanning } = useZipScanner();
 *
 * const handleFileSelect = async (file: File) => {
 *   try {
 *     const result = await scanFile(file, {
 *       onProgress: ({ progress }) => console.log(`Progress: ${progress}%`),
 *     });
 *     console.log('Scan complete:', result);
 *   } catch (error) {
 *     console.error('Scan failed:', error);
 *   }
 * };
 * ```
 */
export function useZipScanner(): UseZipScannerResult {
  const workerRef = useRef<Worker | null>(null);
  const activeScanCountRef = useRef(0);
  const [isScanning, setIsScanning] = useState(false);
  const nextScanIdRef = useRef(0);
  const abortControllersRef = useRef(new Map<number, AbortController>());
  const unmountedRef = useRef(false);

  // Cleanup worker on unmount
  useEffect(() => {
    return () => {
      unmountedRef.current = true;
      if (workerRef.current) {
        workerRef.current.terminate();
        workerRef.current = null;
      }
    };
  }, []);

  const scanFile = useCallback(
    async (
      file: File | { buffer: ArrayBuffer; fileName: string },
      options?: {
        onProgress?: (progress: ScanProgress) => void;
        onComplete?: (result: ZipScanResult, fileName: string) => void;
        onError?: (error: ScanError) => void;
        signal?: AbortSignal;
      }
    ): Promise<ZipScanResult> => {
      return new Promise((resolve, reject) => {
        // Create worker if it doesn't exist
        if (!workerRef.current) {
          // Vite handles worker imports with ?worker suffix
          workerRef.current = new Worker(new URL('../workers/zipWorker.ts', import.meta.url), {
            type: 'module',
          });
        }

        const worker = workerRef.current;
        const scanId = nextScanIdRef.current++;
        const abortController = new AbortController();

        const cleanup = () => {
          worker.removeEventListener('message', handleMessage);
          abortControllersRef.current.delete(scanId);
        };

        const cancelWorkerScan = () => {
          worker.postMessage({ type: 'cancel', scanId } satisfies WorkerMessage);
          abortController.abort();
        };

        const handleAbort = () => {
          cancelWorkerScan();
          activeScanCountRef.current = Math.max(0, activeScanCountRef.current - 1);
          if (activeScanCountRef.current === 0) {
            setIsScanning(false);
          }
          cleanup();
          reject(new DOMException('Scan aborted', 'AbortError'));
        };

        if (options?.signal) {
          if (options.signal.aborted) {
            handleAbort();
            return;
          }
          options.signal.addEventListener('abort', handleAbort, { once: true });
        }

        abortControllersRef.current.set(scanId, abortController);

        // Increment active scan count
        activeScanCountRef.current++;
        setIsScanning(true);

        const handleMessage = (event: MessageEvent<WorkerMessage>) => {
          const { type, result, error, progress, fileName, scanId: responseScanId } = event.data;

          // Only process messages for this specific scan
          if (responseScanId !== scanId) {
            return;
          }

          switch (type) {
            case 'progress': {
              if (progress !== undefined && options?.onProgress) {
                options.onProgress({
                  fileName: file instanceof File ? file.name : file.fileName,
                  progress,
                  stage: event.data.stage,
                  detail: event.data.detail,
                  processed: event.data.processed,
                  total: event.data.total,
                });
              }
              break;
            }

            case 'result': {
              if (result) {
                // Decrement active scan count
                activeScanCountRef.current--;
                if (activeScanCountRef.current === 0) {
                  setIsScanning(false);
                }

                if (options?.onComplete) {
                  options.onComplete(result, file instanceof File ? file.name : file.fileName);
                }
                cleanup();
                resolve(result);
              }
              break;
            }

            case 'error': {
              // Decrement active scan count
              activeScanCountRef.current--;
              if (activeScanCountRef.current === 0) {
                setIsScanning(false);
              }

                const errorObj = {
                  fileName: fileName || (file instanceof File ? file.name : file.fileName),
                  error: error || 'Unknown error',
                };
              if (options?.onError) {
                options.onError(errorObj);
              }
                cleanup();
              reject(new Error(errorObj.error));
              break;
            }
          }
        };

        worker.addEventListener('message', handleMessage);

        const sendScan = (arrayBuffer: ArrayBuffer, fileName: string) => {
          const message: WorkerMessage = {
            type: 'scan',
            file: arrayBuffer,
            fileName,
            scanId,
          };
          worker.postMessage(message, [arrayBuffer]);
        };

        if (file instanceof File) {
          file
            .arrayBuffer()
            .then((arrayBuffer) => {
              if (abortController.signal.aborted || unmountedRef.current) {
                handleAbort();
                return;
              }
              sendScan(arrayBuffer, file.name);
            })
            .catch((err) => {
              activeScanCountRef.current--;
              if (activeScanCountRef.current === 0) {
                setIsScanning(false);
              }

              const errorMsg = `Failed to read file: ${err instanceof Error ? err.message : String(err)}`;
              if (options?.onError) {
                options.onError({ fileName: file.name, error: errorMsg });
              }
              cleanup();
              reject(new Error(errorMsg));
            });
        } else {
          if (abortController.signal.aborted || unmountedRef.current) {
            handleAbort();
          } else {
            sendScan(file.buffer, file.fileName);
          }
        }
      });
    },
    []
  );

  return {
    scanFile,
    isScanning,
    cancelScan: (scanId?: number) => {
      if (!workerRef.current) return;
      if (typeof scanId === 'number') {
        workerRef.current.postMessage({ type: 'cancel', scanId } satisfies WorkerMessage);
      } else {
        // Cancel all active scans
        abortControllersRef.current.forEach((_controller, id) => {
          workerRef.current?.postMessage({ type: 'cancel', scanId: id } satisfies WorkerMessage);
        });
        abortControllersRef.current.clear();
      }
      setIsScanning(false);
    },
  };
}
