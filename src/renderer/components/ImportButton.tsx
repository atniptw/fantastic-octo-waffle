import { useState, useEffect, useRef } from 'react';
import { scanZipFile } from '@/lib/zipScanner';
import type { Cosmetic, ImportFilesResult, ImportLogEntry, Mod } from '@/shared/types';

interface ImportButtonProps {
  onImportStart?: () => void;
  onImportComplete?: (result: ImportFilesResult) => void;
  onImportError?: (error: string) => void;
  disabled?: boolean;
}

type ImportStatus = 'idle' | 'loading' | 'success' | 'error';

interface StatusState {
  status: ImportStatus;
  message?: string;
}

async function iconToDataUrl(iconData: Uint8Array | null): Promise<string | null> {
  if (!iconData) return null;
  // Create a new Uint8Array to ensure proper ArrayBuffer type for Blob constructor
  const properlyTypedArray = new Uint8Array(iconData);
  const blob = new Blob([properlyTypedArray], { type: 'image/png' });
  return new Promise(resolve => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => resolve(null);
    reader.readAsDataURL(blob);
  });
}

function ImportButton({
  onImportStart,
  onImportComplete,
  onImportError,
  disabled = false,
}: ImportButtonProps) {
  const [statusState, setStatusState] = useState<StatusState>({ status: 'idle' });
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Auto-clear success and error messages after 3 seconds
  useEffect(() => {
    if (statusState.status === 'success' || statusState.status === 'error') {
      const timer = setTimeout(() => {
        setStatusState({ status: 'idle' });
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [statusState.status]);

  const processFiles = async (files: FileList | File[]): Promise<void> => {
    const fileArray = Array.from(files);
    if (fileArray.length === 0) return;

    setStatusState({ status: 'loading', message: 'Importing...' });
    onImportStart?.();

    const logs: ImportLogEntry[] = [];
    const mods: Mod[] = [];
    const cosmetics: Cosmetic[] = [];
    let successCount = 0;
    let warningCount = 0;
    let errorCount = 0;

    for (const file of fileArray) {
      const timestamp = new Date().toISOString();
      try {
        const result = await scanZipFile(file);

        if (result.errors.length > 0) {
          result.errors.forEach(msg => {
            logs.push({
              timestamp,
              filename: file.name,
              status: 'warning',
              message: msg,
            });
          });
          warningCount += 1;
        }

        if (!result.manifest) {
          logs.push({
            timestamp,
            filename: file.name,
            status: 'error',
            message: 'Invalid mod structure - manifest.json not found or invalid',
          });
          errorCount += 1;
          continue;
        }

        const modId = crypto.randomUUID();
        const iconData = await iconToDataUrl(result.iconData);
        const mod: Mod = {
          id: modId,
          mod_name: result.manifest.name,
          author: result.manifest.author,
          version: result.manifest.version_number,
          iconData,
          source: file.name,
        };
        mods.push(mod);

        result.cosmetics.forEach(cosmeticMeta => {
          const cosmetic: Cosmetic = {
            id: crypto.randomUUID(),
            mod_id: modId,
            display_name: cosmeticMeta.displayName,
            filename: cosmeticMeta.filename,
            hash: cosmeticMeta.hash,
            type: cosmeticMeta.type,
            internal_path: cosmeticMeta.internalPath,
          };
          cosmetics.push(cosmetic);
        });

        logs.push({
          timestamp,
          filename: file.name,
          status: 'success',
          message: `${result.cosmetics.length} cosmetic(s) found`,
        });
        successCount += 1;
      } catch (error) {
        logs.push({
          timestamp,
          filename: file.name,
          status: 'error',
          message: `Failed to process: ${error instanceof Error ? error.message : String(error)}`,
        });
        errorCount += 1;
      }
    }

    const result: ImportFilesResult = {
      logs,
      totalFiles: fileArray.length,
      successCount,
      errorCount,
      warningCount,
      mods,
      cosmetics,
    };

    const successMsg = result.successCount === result.totalFiles
      ? `${result.successCount} mod(s) imported successfully`
      : `${result.successCount} mod(s) imported, ${result.totalFiles - result.successCount} skipped/failed`;

    setStatusState({ status: result.errorCount > 0 ? 'error' : 'success', message: successMsg });
    onImportComplete?.(result);
  };

  const handleButtonClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      processFiles(event.target.files).catch(err => {
        const message = err instanceof Error ? err.message : String(err);
        setStatusState({ status: 'error', message });
        onImportError?.(message);
      });
      // Reset the input so the same file can be selected again
      event.target.value = '';
    }
  };

  const isDisabled = disabled || statusState.status === 'loading';

  const renderButtonContent = () => {
    switch (statusState.status) {
      case 'loading':
        return (
          <>
            <span className="spinner">⏳</span>
            <span>{statusState.message}</span>
          </>
        );
      case 'success':
        return (
          <>
            <span className="success-icon">✅</span>
            <span>{statusState.message}</span>
          </>
        );
      case 'error':
        return (
          <>
            <span className="error-icon">❌</span>
            <span>{statusState.message || 'Import failed'}</span>
          </>
        );
      default:
        return (
          <>
            <span>📁</span>
            <span>Import Mod ZIP(s)</span>
          </>
        );
    }
  };

  return (
    <>
      <input
        type="file"
        accept=".zip"
        multiple
        ref={fileInputRef}
        data-testid="import-input"
        style={{ display: 'none' }}
        onChange={handleFileInputChange}
      />
      <button
        className={`import-button import-button--${statusState.status}`}
        onClick={handleButtonClick}
        disabled={isDisabled}
        aria-busy={statusState.status === 'loading'}
        type="button"
      >
        {renderButtonContent()}
      </button>
    </>
  );
}

export default ImportButton;
