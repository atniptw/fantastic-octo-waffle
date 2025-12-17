import { useState } from 'react';
import FileUpload from '@/renderer/components/FileUpload';
import { useZipScanner, type ScanProgress } from '@/lib/useZipScanner';
import type { ZipScanResult } from '@/lib/zipScanner';

interface ScannedFile {
  file: File;
  result?: ZipScanResult;
  error?: string;
  progress: number;
  isScanning: boolean;
}

function FileUploadDemo() {
  const [scannedFiles, setScannedFiles] = useState<ScannedFile[]>([]);
  const { scanFile } = useZipScanner();

  const handleFilesSelected = async (files: File[]) => {
    console.log('Files selected:', files);
    
    // Add files to state with initial scanning status
    setScannedFiles(prev => {
      const newFiles: ScannedFile[] = files.map(file => ({
        file,
        progress: 0,
        isScanning: true,
      }));
      
      // Return the updated array and capture the starting index for scanning
      const updatedFiles = [...prev, ...newFiles];
      const startIndex = prev.length;
      
      // Scan each file asynchronously
      files.forEach((file, relativeIndex) => {
        const absoluteIndex = startIndex + relativeIndex;
        
        scanFile(file, {
          onProgress: (progress: ScanProgress) => {
            setScannedFiles(current => {
              const updated = [...current];
              if (updated[absoluteIndex]) {
                updated[absoluteIndex] = {
                  ...updated[absoluteIndex],
                  progress: progress.progress,
                };
              }
              return updated;
            });
          },
          onComplete: (result: ZipScanResult) => {
            setScannedFiles(current => {
              const updated = [...current];
              if (updated[absoluteIndex]) {
                updated[absoluteIndex] = {
                  ...updated[absoluteIndex],
                  result,
                  isScanning: false,
                  progress: 100,
                };
              }
              return updated;
            });
          },
          onError: (error) => {
            setScannedFiles(current => {
              const updated = [...current];
              if (updated[absoluteIndex]) {
                updated[absoluteIndex] = {
                  ...updated[absoluteIndex],
                  error: error.error,
                  isScanning: false,
                  progress: 0,
                };
              }
              return updated;
            });
          },
        }).catch(error => {
          console.error('Error scanning file:', error);
        });
      });
      
      return updatedFiles;
    });
  };

  return (
    <div style={{ padding: '2rem', maxWidth: '1000px', margin: '0 auto' }}>
      <h1 style={{ color: '#e94560', marginBottom: '1rem' }}>
        Browser-Based ZIP Scanner Demo
      </h1>
      <p style={{ color: '#a0a0a0', marginBottom: '2rem' }}>
        Upload Thunderstore mod ZIP files to see the browser-based scanner in action. 
        Files are processed using Web Workers to keep the UI responsive.
      </p>
      
      <FileUpload onFilesSelected={handleFilesSelected} />
      
      {scannedFiles.length > 0 && (
        <div style={{ marginTop: '2rem' }}>
          <h3 style={{ color: '#e94560', marginBottom: '1rem' }}>
            Scanned Files ({scannedFiles.length})
          </h3>
          
          {scannedFiles.map((scanned) => (
            <div
              key={`${scanned.file.name}-${scanned.file.size}-${scanned.file.lastModified}`}
              style={{
                marginBottom: '1rem',
                padding: '1rem',
                backgroundColor: '#16213e',
                borderRadius: '8px',
                borderLeft: `4px solid ${scanned.error ? '#e94560' : scanned.isScanning ? '#ffa500' : '#4ecca3'}`,
              }}
            >
              <div style={{ marginBottom: '0.5rem' }}>
                <strong style={{ color: '#eaeaea' }}>
                  {scanned.file.name}
                </strong>
                <span style={{ color: '#a0a0a0', marginLeft: '0.5rem' }}>
                  ({Math.round(scanned.file.size / 1024)} KB)
                </span>
              </div>

              {scanned.isScanning && (
                <div style={{ marginBottom: '0.5rem' }}>
                  <div style={{ 
                    width: '100%', 
                    height: '4px', 
                    backgroundColor: '#0f3460',
                    borderRadius: '2px',
                    overflow: 'hidden',
                  }}>
                    <div style={{
                      width: `${scanned.progress}%`,
                      height: '100%',
                      backgroundColor: '#ffa500',
                      transition: 'width 0.3s ease',
                    }} />
                  </div>
                  <span style={{ color: '#ffa500', fontSize: '0.875rem' }}>
                    Scanning... {scanned.progress}%
                  </span>
                </div>
              )}

              {scanned.error && (
                <div style={{ color: '#e94560', marginTop: '0.5rem' }}>
                  ❌ Error: {scanned.error}
                </div>
              )}

              {scanned.result && !scanned.error && (
                <div style={{ marginTop: '0.5rem' }}>
                  {scanned.result.manifest ? (
                    <div style={{ color: '#4ecca3' }}>
                      ✅ <strong>Mod:</strong> {scanned.result.manifest.name} v{scanned.result.manifest.version_number}
                      <br />
                      <span style={{ color: '#a0a0a0' }}>
                        by {scanned.result.manifest.author}
                      </span>
                      <br />
                      <span style={{ color: '#a0a0a0' }}>
                        Cosmetics found: {scanned.result.cosmetics.length}
                      </span>
                      {scanned.result.cosmetics.length > 0 && (
                        <div style={{ marginTop: '0.5rem', paddingLeft: '1rem' }}>
                          {scanned.result.cosmetics.map((cosmetic, idx) => (
                            <div key={idx} style={{ color: '#eaeaea', fontSize: '0.875rem' }}>
                              • {cosmetic.displayName} ({cosmetic.type})
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  ) : (
                    <div style={{ color: '#ffa500' }}>
                      ⚠️ No valid manifest found
                    </div>
                  )}
                  
                  {scanned.result.errors.length > 0 && (
                    <div style={{ marginTop: '0.5rem', color: '#ffa500', fontSize: '0.875rem' }}>
                      Warnings:
                      {scanned.result.errors.map((err, idx) => (
                        <div key={idx}>• {err}</div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default FileUploadDemo;
