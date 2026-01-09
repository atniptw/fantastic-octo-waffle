/**
 * HIGH-LEVEL API: Analyze a mod by URL
 * Handles: download → scan → convert → cache
 * Returns: cosmetics ready for display
 */

import { useCallback, useRef, useState, useEffect } from 'react';
import { useZipDownloader } from './useZipDownloader';
import { useScanWorker } from './useScanWorker';
import { convertHhhToImage } from '../core/cosmetic';
import type { CosmeticPreview, AnalyzeOptions, AnalyzeResult, UseModAnalyzerResult } from '../types';

export { type UseModAnalyzerResult } from '../types';

/**
 * Hook for analyzing mods by downloading, scanning, and converting cosmetics
 * PRIMARY API for the pipeline module
 *
 * @example
 * ```tsx
 * const { analyze, isAnalyzing, error } = useModAnalyzer();
 *
 * const handleAnalyze = async () => {
 *   try {
 *     const result = await analyze(modUrl, 'AuthorName', 'ModName', {
 *       onProgress: (p) => console.log(`${p.stage}: ${p.percent}%`),
 *     });
 *     setCosmetics(result.cosmetics);
 *   } catch (err) {
 *     console.error('Analysis failed:', err);
 *   }
 * };
 * ```
 */
export function useModAnalyzer(): UseModAnalyzerResult {
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const unmountedRef = useRef(false);

  const { download } = useZipDownloader();
  const { scanFile } = useScanWorker();

  useEffect(() => {
    return () => {
      unmountedRef.current = true;
      abortControllerRef.current?.abort();
    };
  }, []);

  const analyze = useCallback(
    async (
      modUrl: string,
      namespace: string,
      name: string,
      options?: AnalyzeOptions
    ): Promise<AnalyzeResult> => {
      abortControllerRef.current?.abort();
      const controller = new AbortController();
      abortControllerRef.current = controller;

      if (!unmountedRef.current) {
        setIsAnalyzing(true);
        setError(null);
      }

      try {
        if (options?.signal?.aborted) {
          throw new DOMException('Analysis cancelled', 'AbortError');
        }

        // Stage 1: Download
        options?.onProgress?.({
          stage: 'download',
          percent: 10,
          detail: 'Downloading mod...',
        });

        const zipBuffer = await download(modUrl, namespace, name, {
          signal: controller.signal,
        });

        if (controller.signal.aborted) throw new DOMException('Cancelled', 'AbortError');

        // Stage 2: Extract & Scan
        options?.onProgress?.({
          stage: 'extract',
          percent: 40,
          detail: 'Scanning ZIP archive...',
        });

        const scanResult = await scanFile(
          { buffer: zipBuffer, fileName: `${name}.zip` },
          {
            signal: controller.signal,
            onProgress: ({ progress }) => {
              // Map scan progress (0-100) to extract stage (40-70)
              options?.onProgress?.({
                stage: 'extract',
                percent: 40 + Math.floor(progress * 0.3),
              });
            },
          }
        );

        if (scanResult.hasFatalError) {
          throw new Error(scanResult.errors.join('; '));
        }

        // Stage 3: Convert to Images
        options?.onProgress?.({
          stage: 'convert',
          percent: 70,
          detail: 'Converting cosmetics...',
        });

        const cosmetics: CosmeticPreview[] = [];

        if (!options?.metadataOnly) {
          // Convert each cosmetic to image
          for (let i = 0; i < scanResult.cosmetics.length; i++) {
            const metadata = scanResult.cosmetics[i];

            if (controller.signal.aborted) {
              throw new DOMException('Cancelled', 'AbortError');
            }

            // TODO: In the future, fetch actual .hhh content from ZIP
            // For now, create placeholder
            const image = await convertHhhToImage(new Uint8Array(0));

            cosmetics.push({
              image,
              displayName: metadata.displayName,
              type: metadata.type,
              hash: metadata.hash,
              filename: metadata.filename,
              internalPath: metadata.internalPath,
            });

            const percent = 70 + Math.floor((i / scanResult.cosmetics.length) * 25);
            options?.onProgress?.({
              stage: 'convert',
              percent,
              detail: `Converting ${i + 1} of ${scanResult.cosmetics.length}...`,
            });
          }
        } else {
          // Metadata only - no conversion
          cosmetics.push(
            ...scanResult.cosmetics.map((metadata) => ({
              image: new Blob(), // empty
              displayName: metadata.displayName,
              type: metadata.type,
              hash: metadata.hash,
              filename: metadata.filename,
              internalPath: metadata.internalPath,
            }))
          );
        }

        // Complete
        options?.onProgress?.({
          stage: 'complete',
          percent: 100,
          detail: 'Analysis complete',
        });

        if (!unmountedRef.current) {
          setIsAnalyzing(false);
        }

        return {
          cosmetics,
          mod: {
            name: scanResult.manifest?.name ?? name,
            author: scanResult.manifest?.author ?? 'Unknown',
            version: scanResult.manifest?.version_number ?? 'Unknown',
            icon: scanResult.iconData ? new Blob([scanResult.iconData.buffer as ArrayBuffer], { type: 'image/png' }) : undefined,
          },
          warnings: scanResult.errors,
        };
      } catch (err) {
        if (controller.signal.aborted && err instanceof DOMException) {
          if (!unmountedRef.current) {
            setIsAnalyzing(false);
          }
          throw err;
        }

        const errorMsg = err instanceof Error ? err.message : 'Unknown error during analysis';
        if (!unmountedRef.current) {
          setError(errorMsg);
          setIsAnalyzing(false);
        }
        throw err;
      }
    },
    [download, scanFile]
  );

  return {
    analyze,
    isAnalyzing,
    error,
    cancel: () => abortControllerRef.current?.abort(),
  };
}
