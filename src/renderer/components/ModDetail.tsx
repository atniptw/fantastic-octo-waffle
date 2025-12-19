import { useState, useEffect } from 'react';
import { PackageExperimental, PackageListing, PackageIndexEntry } from '@/lib/thunderstore/types';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { useZipScanner } from '@/lib/useZipScanner';
import { useZipDownloader } from '@/lib/useZipDownloader';
import { CachedZipMetadata } from '@/lib/storage/zipCache';
import { config } from '@/config';

interface ModDetailProps {
  mod: PackageExperimental | PackageListing | PackageIndexEntry | null;
  onAnalyze?: (mod: PackageExperimental | PackageListing | PackageIndexEntry) => void;
}

interface AnalysisState {
  status: 'idle' | 'fetching' | 'extracting' | 'converting' | 'complete' | 'error';
  message: string;
  error?: string;
}

const client = new ThunderstoreClient({
  baseUrl: config.thunderstoreBaseUrl,
  community: config.thunderstoreCommunity,
});

export default function ModDetail({ mod, onAnalyze }: ModDetailProps) {
  const [analysisState, setAnalysisState] = useState<AnalysisState>({
    status: 'idle',
    message: '',
  });
  const { scanFile, isScanning } = useZipScanner();
  const { download, listCached, deleteCached, isDownloading, progress } = useZipDownloader();
  const [cachedZips, setCachedZips] = useState<CachedZipMetadata[]>([]);

  useEffect(() => {
    loadCachedZips();
  }, []);

  const loadCachedZips = async () => {
    const cached = await listCached();
    setCachedZips(cached);
  };

  const matchedCache =
    mod && cachedZips.find((zip) => zip.namespace === ('namespace' in mod ? mod.namespace : '') && zip.name === mod.name);

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
    const namespace = 'namespace' in mod ? mod.namespace : 'owner' in mod ? mod.owner : '';
    if (!namespace) return;

    setAnalysisState({ status: 'fetching', message: `Downloading ${mod.name}...` });

    try {
      const downloadUrl = client.getPackageDownloadUrl(namespace, mod.name);
      // Use proxy URL (Cloudflare Worker or fallback) by forwarding path only
      const url = new URL(downloadUrl);
      const proxyUrl = `${config.thunderstoreBaseUrl}${url.pathname}`;

      // Use the new downloader with caching
      const arrayBuffer = await download(proxyUrl, namespace, mod.name);

      // Create a File from the ArrayBuffer for scanning
      const blob = new Blob([arrayBuffer], { type: 'application/zip' });
      const file = new File([blob], `${mod.name}.zip`, { type: 'application/zip' });

      setAnalysisState({ status: 'extracting', message: `Extracting ${mod.name}...` });

      const result = await scanFile(file, {
        onProgress: (progress) => {
          setAnalysisState({
            status: 'extracting',
            message: `Extracting files... ${Math.round(progress.progress)}%`,
          });
        },
      });

      setAnalysisState({
        status: 'complete',
        message: `Successfully extracted ${result.cosmetics?.length || 0} cosmetic files`,
      });

      // Refresh cached ZIPs list
      await loadCachedZips();

      console.log('ZIP extracted:', result);
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

  const downloadCount = 'total_downloads' in mod ? parseInt(mod.total_downloads, 10) || 0 : 0;
  const formattedDownloads = downloadCount.toLocaleString();

  return (
    <div className="mod-detail">
      <div className="mod-detail-header">
        <h2 className="mod-detail-title">{mod.name}</h2>
        <div className="mod-detail-metadata">
          <span className="mod-detail-meta">by {'owner' in mod ? mod.owner : mod.namespace}</span>
          {'latest' in mod && mod.latest?.version_number && (
            <span className="mod-detail-meta">v{mod.latest.version_number}</span>
          )}
          {'version_number' in mod && mod.version_number && !('latest' in mod) && (
            <span className="mod-detail-meta">v{mod.version_number}</span>
          )}
          {downloadCount > 0 && (
            <span className="mod-detail-meta">↓ {formattedDownloads} downloads</span>
          )}
        </div>
        {'latest' in mod ? (
          <p className="mod-detail-description">
            {mod.latest?.description || 'No description available.'}
          </p>
        ) : (
          <p className="mod-detail-description">No description available.</p>
        )}

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

      {analysisState.status === 'complete' && (
        <div className="asset-gallery">
          <div className="asset-gallery-empty">
            <p>No cosmetic assets found in this mod.</p>
            <p className="asset-gallery-hint">
              Cosmetic assets are typically located in <code>plugins/*/Decorations/*.hhh</code>
            </p>
          </div>
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
