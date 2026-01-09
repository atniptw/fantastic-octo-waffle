import { useState, useEffect } from 'react';
import { ModPackage } from '@/lib/thunderstore/normalize';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { useScanWorker, useZipDownloader, type ZipScanResult } from '@/lib/pipeline';
import { CachedZipMetadata } from '@/lib/storage/zipCache';
import { config } from '@/config';
import { debugLog } from '@/lib/logger';

if (config.debugLogging) {
  debugLog('[ModDetail] Config:', {
    baseUrl: config.thunderstoreBaseUrl,
    proxyUrl: config.thunderstoreProxyUrl,
    community: config.thunderstoreCommunity,
  });
}

interface ModDetailProps {
  mod: ModPackage | null;
  onAnalyze?: (mod: ModPackage) => void;
}

interface AnalysisState {
  status: 'idle' | 'fetching' | 'extracting' | 'converting' | 'complete' | 'error';
  message: string;
  error?: string;
}

const client = new ThunderstoreClient({
  baseUrl: config.thunderstoreBaseUrl,
  proxyUrl: config.thunderstoreProxyUrl,
  community: config.thunderstoreCommunity,
});

export default function ModDetail({ mod, onAnalyze }: ModDetailProps) {
  const [analysisState, setAnalysisState] = useState<AnalysisState>({
    status: 'idle',
    message: '',
  });
  const { scanFile, isScanning } = useScanWorker();
  const { download, listCached, deleteCached, isDownloading, progress } = useZipDownloader();
  const [cachedZips, setCachedZips] = useState<CachedZipMetadata[]>([]);
  const [lastResult, setLastResult] = useState<ZipScanResult | null>(null);

  useEffect(() => {
    loadCachedZips();
  }, []);

  const loadCachedZips = async () => {
    const cached = await listCached();
    setCachedZips(cached);
  };

  const matchedCache =
    mod && cachedZips.find((zip) => zip.namespace === mod.namespace && zip.name === mod.name);

  if (!mod) {
    return (
      <div className="mod-detail mod-detail-empty">
        <p>Select a mod to view details</p>
      </div>
    );
  }

  const handleAnalyze = async () => {
    // Notify parent that analysis was triggered, if provided
    if (onAnalyze && mod) {
      try {
        onAnalyze(mod);
      } catch {
        // no-op: parent callback errors shouldn't block analysis
      }
    }
    const namespace = mod.namespace;
    if (!namespace) return;

    setLastResult(null);
    setAnalysisState({ status: 'fetching', message: `Downloading ${mod.name}...` });

    try {
      // Get the download URL from the mod
      let downloadUrl: string | undefined;
      if (mod.downloadUrl) {
        // Use the canonical download URL from Thunderstore
        const canonicalUrl = mod.downloadUrl;
        debugLog('[ModDetail] Using download_url from package:', canonicalUrl);
        
        // Route through proxy if configured
        if (config.thunderstoreProxyUrl) {
          const urlObj = new URL(canonicalUrl);
          downloadUrl = `${config.thunderstoreProxyUrl}${urlObj.pathname}${urlObj.search}`;
          debugLog('[ModDetail] Routed through proxy:', downloadUrl);
        } else {
          downloadUrl = canonicalUrl;
        }
      } else {
        // Fallback to constructing the URL manually
        const version = mod.version;
        
        if (!version) {
          debugLog('[ModDetail] No version found in package');
        }
        
        downloadUrl = version 
          ? client.getPackageDownloadUrl(namespace, mod.name, version)
          : client.getPackageDownloadUrl(namespace, mod.name);
      }

      debugLog('[ModDetail] Final Download URL:', downloadUrl);

      // Use the new downloader with caching
      const arrayBuffer = await download(downloadUrl, namespace, mod.name);

      setAnalysisState({ status: 'extracting', message: `Extracting ${mod.name}...` });

      const result = await scanFile(
        { buffer: arrayBuffer, fileName: `${mod.name}.zip` },
        {
          onProgress: (progress) => {
            const pct = Number.isFinite(progress.progress) ? Math.round(progress.progress) : 0;
            setAnalysisState({
              status: 'extracting',
              message:
                progress.detail || progress.stage
                  ? `${progress.detail || progress.stage} (${pct}%)`
                  : `Extracting files... ${pct}%`,
            });
          },
        }
      );

      setAnalysisState({
        status: 'complete',
        message: `Successfully extracted ${result.cosmetics?.length || 0} cosmetic files`,
      });
      setLastResult(result);

      // Refresh cached ZIPs list
      await loadCachedZips();

      debugLog('ZIP extracted:', result);
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : 'Unknown error';
      setAnalysisState({
        status: 'error',
        message: 'Failed to analyze mod',
        error: errorMsg,
      });
      console.error('Failed to download/extract mod:', error);
    }
  };

  const handleDeleteCached = async (namespace: string, name: string) => {
    try {
      await deleteCached(namespace, name);
      await loadCachedZips();
    } catch (error) {
      console.error('Failed to delete cached ZIP:', error);
    }
  };

  const downloadCount = mod.downloadCount || 0;
  const formattedDownloads = downloadCount.toLocaleString();

  return (
    <div className="mod-detail">
      <div className="mod-detail-header">
        <h2 className="mod-detail-title">{mod.name}</h2>
        <div className="mod-detail-metadata">
          <span className="mod-detail-meta">by {mod.owner}</span>
          {mod.version && (
            <span className="mod-detail-meta">v{mod.version}</span>
          )}
          {downloadCount > 0 && (
            <span className="mod-detail-meta">↓ {formattedDownloads} downloads</span>
          )}
        </div>
        <p className="mod-detail-description">
          {mod.description || 'No description available.'}
        </p>

        <div className="mod-detail-actions">
          <button
            className="mod-detail-button mod-detail-button-primary"
            onClick={handleAnalyze}
            disabled={
              isScanning ||
              isDownloading ||
              analysisState.status === 'fetching' ||
              analysisState.status === 'extracting'
            }
          >
            {isScanning ||
            isDownloading ||
            analysisState.status === 'fetching' ||
            analysisState.status === 'extracting'
              ? 'Analyzing...'
              : 'Analyze Mod'}
          </button>
          <a
            href={
              'package_url' in mod
                ? mod.package_url
                : `https://thunderstore.io/package/${mod.namespace}/${mod.name}/`
            }
            target="_blank"
            rel="noopener noreferrer"
            className="mod-detail-button mod-detail-button-secondary"
          >
            Open on Thunderstore
          </a>
        </div>
      </div>

      {progress && (
        <div className="download-progress">
          {(() => {
            const totalBytes = progress.total > 0 ? progress.total : matchedCache?.size ?? 0;
            const hasTotal = totalBytes > 0;
            const percent = hasTotal ? Math.min(100, Math.round((progress.loaded / totalBytes) * 100)) : null;
            const loadedMb = (progress.loaded / 1024 / 1024).toFixed(1);
            const totalMb = hasTotal ? (totalBytes / 1024 / 1024).toFixed(1) : '?';

            return (
              <>
                <div className={`progress-bar ${hasTotal ? '' : 'progress-indeterminate'}`}>
                  <div
                    className="progress-fill"
                    style={hasTotal ? { width: `${percent ?? 0}%` } : undefined}
                  />
                </div>
                <div className="progress-info">
                  <span className="progress-percentage">{hasTotal ? `${percent}%` : '...'}
                  </span>
                  <span className="progress-size">
                    {loadedMb} MB / {totalMb} MB
                  </span>
                </div>
              </>
            );
          })()}
        </div>
      )}

      {analysisState.status !== 'idle' && (
        <div className={`analysis-status analysis-status-${analysisState.status}`}>
          <div className="analysis-status-message">
            {(analysisState.status === 'fetching' ||
              analysisState.status === 'extracting' ||
              analysisState.status === 'converting') && <span className="analysis-spinner">⟳</span>}
            {analysisState.message}
          </div>
          {analysisState.error && <div className="analysis-error">{analysisState.error}</div>}
        </div>
      )}

      {lastResult && (
        <div className="asset-gallery">
          <div className="asset-gallery-header">
            <h3>Cosmetics ({lastResult.cosmetics.length})</h3>
            {lastResult.manifest && (
              <span className="asset-gallery-meta">{lastResult.manifest.name} v{lastResult.manifest.version_number}</span>
            )}
          </div>

          {lastResult.cosmetics.length > 0 ? (
            <div className="asset-gallery-list">
              {lastResult.cosmetics.map((cosmetic) => (
                <div key={cosmetic.internalPath} className="asset-gallery-item">
                  <div className="asset-name">{cosmetic.displayName}</div>
                  <div className="asset-meta">
                    <span className="asset-type">{cosmetic.type}</span>
                    <span className="asset-size">{(cosmetic.size / 1024).toFixed(1)} KB</span>
                    <span className="asset-path">{cosmetic.internalPath}</span>
                  </div>
                  <div className="asset-hash" title={cosmetic.hash}>
                    {cosmetic.hash.slice(0, 12)}…
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="asset-gallery-empty">
              <p>No cosmetic assets found in this mod.</p>
              <p className="asset-gallery-hint">
                Cosmetic assets are typically located in <code>plugins/*/Decorations/*.hhh</code>
              </p>
            </div>
          )}

          {lastResult.errors.length > 0 && (
            <div className="analysis-errors">
              <h4>Warnings</h4>
              <ul>
                {lastResult.errors.map((err, idx) => (
                  <li key={`${err}-${idx}`}>{err}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {cachedZips.length > 0 && (
        <div className="cached-downloads">
          <h3 className="cached-downloads-title">Cached Downloads ({cachedZips.length})</h3>
          <div className="cached-downloads-list">
            {cachedZips.map((cached) => (
              <div key={cached.id} className="cached-download-item">
                <div className="cached-item-info">
                  <div className="cached-item-name">{cached.name}</div>
                  <div className="cached-item-meta">
                    <span className="cached-item-size">{(cached.size / 1024 / 1024).toFixed(1)} MB</span>
                    <span className="cached-item-date">
                      {new Date(cached.downloadedAt).toLocaleDateString()}
                    </span>
                  </div>
                </div>
                <button
                  className="cached-item-delete"
                  onClick={() => handleDeleteCached(cached.namespace, cached.name)}
                  title="Delete from cache"
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
