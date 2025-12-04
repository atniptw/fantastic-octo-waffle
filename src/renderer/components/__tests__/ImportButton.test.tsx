import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import ImportButton from '../ImportButton';

describe('ImportButton', () => {
  it('should render the import button', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button', { name: /import mod zip/i });
    expect(button).toBeInTheDocument();
  });

  it('should display the folder icon', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button.textContent).toContain('📁');
  });

  it('should log message when clicked outside Electron', () => {
    const consoleSpy = vi.spyOn(console, 'log');
    
    render(<ImportButton />);
    const button = screen.getByRole('button');
    
    fireEvent.click(button);
    
    expect(consoleSpy).toHaveBeenCalledWith(
      'Import button clicked - running outside Electron'
    );
    
    consoleSpy.mockRestore();
  });

  it('should have the correct CSS class', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).toHaveClass('import-button');
  });

  it('should be disabled when disabled prop is true', () => {
    render(<ImportButton disabled={true} />);
    
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should call onImportStart when import begins', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockResolvedValue({
      logs: [],
      totalFiles: 1,
      successCount: 1,
      errorCount: 0,
      warningCount: 0,
    });
    
    // Mock electronAPI
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    const onImportComplete = vi.fn();
    
    render(
      <ImportButton 
        onImportStart={onImportStart}
        onImportComplete={onImportComplete}
      />
    );
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for async operations
    await vi.waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
    });
    
    await vi.waitFor(() => {
      expect(onImportStart).toHaveBeenCalled();
    });
    
    await vi.waitFor(() => {
      expect(onImportComplete).toHaveBeenCalled();
    });
    
    // Cleanup
    delete window.electronAPI;
  });

  it('should not call callbacks when file dialog is cancelled', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(null);
    
    // Mock electronAPI
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: vi.fn(),
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    const onImportComplete = vi.fn();
    
    render(
      <ImportButton 
        onImportStart={onImportStart}
        onImportComplete={onImportComplete}
      />
    );
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for async operations
    await vi.waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
    });
    
    // Should not trigger import start or complete
    expect(onImportStart).not.toHaveBeenCalled();
    expect(onImportComplete).not.toHaveBeenCalled();
    
    // Cleanup
    delete window.electronAPI;
  });

  it('should call onImportError when import fails', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockRejectedValue(new Error('Import failed'));
    
    // Mock electronAPI
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    const onImportError = vi.fn();
    
    render(
      <ImportButton 
        onImportStart={onImportStart}
        onImportError={onImportError}
      />
    );
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for async operations
    await vi.waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
    });
    
    await vi.waitFor(() => {
      expect(onImportStart).toHaveBeenCalled();
    });
    
    await vi.waitFor(() => {
      expect(onImportError).toHaveBeenCalledWith('Import failed');
    });
    
    // Cleanup
    delete window.electronAPI;
  });
});
