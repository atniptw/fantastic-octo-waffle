import { ImportFilesResult } from '../types/electron';

interface ImportButtonProps {
  onImportStart?: () => void;
  onImportComplete?: (result: ImportFilesResult) => void;
  onImportError?: (error: string) => void;
  disabled?: boolean;
}

function ImportButton({
  onImportStart,
  onImportComplete,
  onImportError,
  disabled = false,
}: ImportButtonProps) {
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

      // Notify start
      onImportStart?.();

      // Process imports
      const result = await window.electronAPI.importZipFiles(filePaths);
      
      // Notify completion
      onImportComplete?.(result);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      console.error('Import failed:', errorMessage);
      onImportError?.(errorMessage);
    }
  };

  return (
    <button 
      className="import-button" 
      onClick={handleImport}
      disabled={disabled}
    >
      📁 Import Mod ZIP(s)
    </button>
  );
}

export default ImportButton;
