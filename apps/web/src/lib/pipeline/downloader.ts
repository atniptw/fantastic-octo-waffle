/**
 * React hook for downloading and caching mod ZIP files
 * Provides progress tracking and automatic browser storage via IndexedDB
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { zipCache, CachedZipMetadata } from '@/lib/storage/zipCache';

export interface DownloadProgress {
  loaded: number;
  total: number;
  percentage: number;
}

export interface UseZipDownloaderResult {
  download: (
    url: string,
    namespace: string,
    name: string,
    options?: { signal?: AbortSignal }
  ) => Promise<ArrayBuffer>;
  getCached: (namespace: string, name: string) => Promise<Uint8Array | null>;
  getCachedMetadata: (namespace: string, name: string) => Promise<CachedZipMetadata | null>;
  listCached: () => Promise<CachedZipMetadata[]>;
  deleteCached: (namespace: string, name: string) => Promise<void>;
  isDownloading: boolean;
  progress: DownloadProgress | null;
  error: string | null;
  cancel: () => void;
}

export function useZipDownloader(): UseZipDownloaderResult {
  const [isDownloading, setIsDownloading] = useState(false);
  const [progress, setProgress] = useState<DownloadProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const controllerRef = useRef<AbortController | null>(null);
  const unmountedRef = useRef(false);

  useEffect(() => {
    return () => {
      unmountedRef.current = true;
      controllerRef.current?.abort();
    };
  }, []);

  const download = useCallback(
    async (url: string, namespace: string, name: string, options?: { signal?: AbortSignal }): Promise<ArrayBuffer> => {
      controllerRef.current?.abort();
      const controller = new AbortController();
      controllerRef.current = controller;

      if (options?.signal) {
        if (options.signal.aborted) {
          controller.abort();
        } else {
          options.signal.addEventListener('abort', () => controller.abort(), { once: true });
        }
      }

      if (!unmountedRef.current) {
        setIsDownloading(true);
        setError(null);
        setProgress(null);
      }

      try {
        const cached = await zipCache.getZipData(namespace, name);
        if (cached) {
          if (!unmountedRef.current) {
            setIsDownloading(false);
          }
          return cached.buffer as ArrayBuffer;
        }

        const response = await fetch(url, { signal: controller.signal });
        if (!response.ok) {
          throw new Error(`Download failed: ${response.statusText}`);
        }

        const contentLength = response.headers.get('content-length');
        const total = contentLength ? parseInt(contentLength, 10) : 0;
        let loaded = 0;

        if (!response.body) {
          throw new Error('Response body is empty');
        }

        const reader = response.body.getReader();
        const chunks: Uint8Array[] = [];

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          if (controller.signal.aborted) {
            throw new DOMException('Download aborted', 'AbortError');
          }

          chunks.push(value);
          loaded += value.length;

          if (!unmountedRef.current) {
            setProgress({
              loaded,
              total,
              percentage: total > 0 ? (loaded / total) * 100 : 0,
            });
          }
        }

        const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
        const buffer = new Uint8Array(totalLength);
        let offset = 0;
        for (const chunk of chunks) {
          buffer.set(chunk, offset);
          offset += chunk.length;
        }

        const fileName = url.split('/').pop() || `${name}.zip`;
        await zipCache.saveZip(namespace, name, fileName, buffer.buffer, 'application/zip');

        if (!unmountedRef.current) {
          setIsDownloading(false);
          setProgress(null);
        }
        return buffer.buffer;
      } catch (err) {
        if (controller.signal.aborted) {
          if (!unmountedRef.current) {
            setIsDownloading(false);
            setProgress(null);
          }
          throw err;
        }

        const errorMsg = err instanceof Error ? err.message : 'Unknown error';
        if (!unmountedRef.current) {
          setError(errorMsg);
          setIsDownloading(false);
        }
        throw err;
      }
    },
    []
  );

  const getCached = useCallback(async (namespace: string, name: string): Promise<Uint8Array | null> => {
    try {
      return await zipCache.getZipData(namespace, name);
    } catch (err) {
      console.error('Failed to get cached ZIP:', err);
      return null;
    }
  }, []);

  const getCachedMetadata = useCallback(
    async (namespace: string, name: string): Promise<CachedZipMetadata | null> => {
      try {
        return await zipCache.getZipMetadata(namespace, name);
      } catch (err) {
        console.error('Failed to get cached ZIP metadata:', err);
        return null;
      }
    },
    []
  );

  const listCached = useCallback(async (): Promise<CachedZipMetadata[]> => {
    try {
      return await zipCache.listZips();
    } catch (err) {
      console.error('Failed to list cached ZIPs:', err);
      return [];
    }
  }, []);

  const deleteCached = useCallback(async (namespace: string, name: string): Promise<void> => {
    try {
      await zipCache.deleteZip(namespace, name);
    } catch (err) {
      console.error('Failed to delete cached ZIP:', err);
      throw err;
    }
  }, []);

  return {
    download,
    getCached,
    getCachedMetadata,
    listCached,
    deleteCached,
    isDownloading,
    progress,
    error,
    cancel: () => controllerRef.current?.abort(),
  };
}
