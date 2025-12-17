/**
 * React hook for using the ZIP scanner Web Worker.
 * Provides a clean API for scanning ZIP files without blocking the UI.
 */

import { useRef, useCallback, useState } from 'react';
import type { ZipScanResult, WorkerMessage } from './zipScanner';

export interface ScanProgress {
  fileName: string;
  progress: number;
}

export interface ScanError {
  fileName: string;
  error: string;
}

export interface UseZipScannerResult {
  scanFile: (
    file: File,
    options?: {
      onProgress?: (progress: ScanProgress) => void;
      onComplete?: (result: ZipScanResult, fileName: string) => void;
      onError?: (error: ScanError) => void;
    }
  ) => Promise<ZipScanResult>;
  isScanning: boolean;
}

/**
 * Hook for scanning ZIP files using a Web Worker.
 * Handles worker lifecycle and provides a promise-based API.
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
  const [isScanning, setIsScanning] = useState(false);

  const scanFile = useCallback(
    async (
      file: File,
      options?: {
        onProgress?: (progress: ScanProgress) => void;
        onComplete?: (result: ZipScanResult, fileName: string) => void;
        onError?: (error: ScanError) => void;
      }
    ): Promise<ZipScanResult> => {
      return new Promise((resolve, reject) => {
        // Create worker if it doesn't exist
        if (!workerRef.current) {
          // Vite handles worker imports with ?worker suffix
          workerRef.current = new Worker(
            new URL('../workers/zipWorker.ts', import.meta.url),
            { type: 'module' }
          );
        }

        const worker = workerRef.current;
        setIsScanning(true);

        const handleMessage = (event: MessageEvent<WorkerMessage>) => {
          const { type, result, error, progress, fileName } = event.data;

          switch (type) {
            case 'progress': {
              if (progress !== undefined && options?.onProgress) {
                options.onProgress({ fileName: file.name, progress });
              }
              break;
            }

            case 'result': {
              if (result) {
                setIsScanning(false);
                if (options?.onComplete) {
                  options.onComplete(result, file.name);
                }
                worker.removeEventListener('message', handleMessage);
                resolve(result);
              }
              break;
            }

            case 'error': {
              setIsScanning(false);
              const errorObj = { fileName: fileName || file.name, error: error || 'Unknown error' };
              if (options?.onError) {
                options.onError(errorObj);
              }
              worker.removeEventListener('message', handleMessage);
              reject(new Error(errorObj.error));
              break;
            }
          }
        };

        worker.addEventListener('message', handleMessage);

        // Read file and send to worker
        file.arrayBuffer().then(arrayBuffer => {
          const message: WorkerMessage = {
            type: 'scan',
            file: arrayBuffer,
            fileName: file.name,
          };
          worker.postMessage(message);
        }).catch(err => {
          setIsScanning(false);
          const errorMsg = `Failed to read file: ${err instanceof Error ? err.message : String(err)}`;
          if (options?.onError) {
            options.onError({ fileName: file.name, error: errorMsg });
          }
          reject(new Error(errorMsg));
        });
      });
    },
    []
  );

  return {
    scanFile,
    isScanning,
  };
}
