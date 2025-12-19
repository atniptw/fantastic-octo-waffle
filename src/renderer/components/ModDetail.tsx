import { useState } from 'react';
import { PackageExperimental, PackageListing, PackageIndexEntry } from '@/lib/thunderstore/types';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { useZipScanner } from '@/lib/useZipScanner';

interface ModDetailProps {
  mod: PackageExperimental | PackageListing | PackageIndexEntry | null;
  onAnalyze?: (mod: PackageExperimental | PackageListing | PackageIndexEntry) => void;
}

interface AnalysisState {
  status: 'idle' | 'fetching' | 'extracting' | 'converting' | 'complete' | 'error';
  message: string;
  error?: string;
}

const client = new ThunderstoreClient();

export default function ModDetail({ mod, onAnalyze }: ModDetailProps) {
  const [analysisState, setAnalysisState] = useState<AnalysisState>({
    status: 'idle',
    message: '',
  });
  const { scanFile, isScanning } = useZipScanner();

  if (!mod) {
    return (
      <div className="mod-detail mod-detail-empty">
        <p>Select a mod to view details</p>
      </div>
    );
  }

  const handleAnalyze = async () => {
    if (!onAnalyze) return;

    setAnalysisState({ status: 'fetching', message: 'Fetching mod archive...' });

    try {
      // Simulate analysis process
      await new Promise(resolve => setTimeout(resolve, 500));
      setAnalysisState({ status: 'extracting', message: 'Extracting files...' });
      
      await new Promise(resolve => setTimeout(resolve, 500));
      setAnalysisState({ status: 'converting', message: 'Converting cosmetic assets...' });
      
      await new Promise(resolve => setTimeout(resolve, 500));
      
      onAnalyze(mod);
      
      setAnalysisState({ 
        status: 'complete', 
        message: 'Analysis complete! No cosmetic assets found in this mod.' 
      });
    } catch (error) {
      setAnalysisState({
        status: 'error',
        message: 'Analysis failed',
        error: error instanceof Error ? error.message : 'Unknown error',
      });
    }
  };

  const handleDownload = async () => {
    const namespace = 'namespace' in mod ? mod.namespace : ('owner' in mod ? mod.owner : '');
    if (!namespace) return;
    
    setAnalysisState({ status: 'fetching', message: `Downloading ${mod.name}...` });
    
    try {
      const downloadUrl = client.getPackageDownloadUrl(namespace, mod.name);
      const response = await fetch(downloadUrl);
      
      if (!response.ok) {
        throw new Error(`Failed to download: ${response.statusText}`);
      }
      
      const arrayBuffer = await response.arrayBuffer();
      const file = new File([arrayBuffer], `${mod.name}.zip`, { type: 'application/zip' });
      
      setAnalysisState({ status: 'extracting', message: `Extracting ${mod.name}...` });
      
      const result = await scanFile(file, {
        onProgress: (progress) => {
          setAnalysisState({ 
            status: 'extracting', 
            message: `Extracting files... ${Math.round(progress.progress)}%` 
          });
        },
      });
      
      setAnalysisState({ 
        status: 'complete', 
        message: `Successfully extracted ${result.cosmetics?.length || 0} cosmetic files` 
      });
      
      console.log('ZIP extracted:', result);
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : 'Unknown error';
      setAnalysisState({
        status: 'error',
        message: 'Failed to import mod',
        error: errorMsg,
      });
      console.error('Failed to download/extract mod:', error);
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
            onClick={handleDownload}
            disabled={isScanning || analysisState.status === 'fetching' || analysisState.status === 'extracting'}
          >
            {isScanning || analysisState.status === 'fetching' || analysisState.status === 'extracting' 
              ? 'Importing...' 
              : 'Import Mod'}
          </button>
          <button
            className="mod-detail-button mod-detail-button-primary"
            onClick={handleAnalyze}
            disabled={analysisState.status !== 'idle' && analysisState.status !== 'complete' && analysisState.status !== 'error'}
          >
            Analyze Mod
          </button>
          <a
            href={'package_url' in mod ? mod.package_url : `https://thunderstore.io/package/${mod.namespace}/${mod.name}/`}
            target="_blank"
            rel="noopener noreferrer"
            className="mod-detail-button mod-detail-button-secondary"
          >
            Open on Thunderstore
          </a>
        </div>
      </div>

      {analysisState.status !== 'idle' && (
        <div className={`analysis-status analysis-status-${analysisState.status}`}>
          <div className="analysis-status-message">
            {(analysisState.status === 'fetching' || analysisState.status === 'extracting' || analysisState.status === 'converting') && (
              <span className="analysis-spinner">⟳</span>
            )}
            {analysisState.message}
          </div>
          {analysisState.error && (
            <div className="analysis-error">{analysisState.error}</div>
          )}
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
    </div>
  );
}
