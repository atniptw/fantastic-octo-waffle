import { useState, useEffect } from 'react';
import { ImportFilesResult } from '../types/electron';

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

function ImportButton({
  onImportStart,
  onImportComplete,
  onImportError,
  disabled = false,
}: ImportButtonProps) {
  const [statusState, setStatusState] = useState<StatusState>({ status: 'idle' });

  // Auto-clear success message after 3 seconds
  useEffect(() => {
    if (statusState.status === 'success') {
      const timer = setTimeout(() => {
        setStatusState({ status: 'idle' });
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [statusState.status]);

  const handleImport = async () => {
    // Check if running in Electron
    if (!window.electronAPI) {
      console.log('Import button clicked - running outside Electron');
      return;
    }

    try {
      // Open file dialog
      const filePaths = await window.electronAPI.selectZipFiles();
      
      if (!filePaths || filePaths.length === 0) {
        // User cancelled the dialog
        return;
      }

      // Set loading state
      setStatusState({ status: 'loading', message: 'Importing...' });

      // Notify start
      onImportStart?.();

      // Process imports
      const result = await window.electronAPI.importZipFiles(filePaths);
      
      // Set success state with summary
      const successMsg = `${result.successCount} mod(s) imported, ${result.totalFiles - result.successCount} skipped/failed`;
      setStatusState({ status: 'success', message: successMsg });

      // Notify completion
      onImportComplete?.(result);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      console.error('Import failed:', errorMessage);
      
      // Set error state
      setStatusState({ status: 'error', message: errorMessage });
      
      onImportError?.(errorMessage);
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
    <button 
      className={`import-button import-button--${statusState.status}`}
      onClick={handleImport}
      disabled={isDisabled}
      aria-busy={statusState.status === 'loading'}
    >
      {renderButtonContent()}
    </button>
  );
}

export default ImportButton;
