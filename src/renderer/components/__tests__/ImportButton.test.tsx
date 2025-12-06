import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import ImportButton from '../ImportButton';

describe('ImportButton', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    delete window.electronAPI;
  });

  it('should render the import button', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button', { name: /import mod zip/i });
    expect(button).toBeInTheDocument();
  });

  it('should display the folder icon in idle state', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button.textContent).toContain('📁');
    expect(button.textContent).toContain('Import Mod ZIP(s)');
  });

  it('should have idle status class by default', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).toHaveClass('import-button--idle');
  });

  it('should log message when clicked outside Electron', () => {
    const consoleSpy = vi.spyOn(console, 'log');
    
    render(<ImportButton />);
    const button = screen.getByRole('button');
    
    fireEvent.click(button);
    
    expect(consoleSpy).toHaveBeenCalledWith(
      'Import button clicked - running outside Electron'
    );
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

  it('should show loading state during import', async () => {
    let resolveImport: ((value: {
      logs: never[];
      totalFiles: number;
      successCount: number;
      errorCount: number;
      warningCount: number;
    }) => void) | undefined;
    const importPromise = new Promise<{
      logs: never[];
      totalFiles: number;
      successCount: number;
      errorCount: number;
      warningCount: number;
    }>(resolve => {
      resolveImport = resolve;
    });
    
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockReturnValue(importPromise);
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    
    render(<ImportButton onImportStart={onImportStart} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for loading state
    await waitFor(() => {
      expect(button.textContent).toContain('⏳');
      expect(button.textContent).toContain('Importing...');
      expect(button).toHaveClass('import-button--loading');
      expect(button).toHaveAttribute('aria-busy', 'true');
      expect(button).toBeDisabled();
    });
    
    // Cleanup - resolve the promise
    resolveImport!({
      logs: [],
      totalFiles: 1,
      successCount: 1,
      errorCount: 0,
      warningCount: 0,
    });
  });

  it('should show success state after successful import', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockResolvedValue({
      logs: [],
      totalFiles: 2,
      successCount: 2,
      errorCount: 0,
      warningCount: 0,
    });
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportComplete = vi.fn();
    
    render(<ImportButton onImportComplete={onImportComplete} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for import to complete
    await waitFor(() => {
      expect(button.textContent).toContain('✅');
      expect(button.textContent).toContain('2 mod(s) imported');
      expect(button).toHaveClass('import-button--success');
    });
    
    expect(onImportComplete).toHaveBeenCalled();
  });

  it('should auto-dismiss success message after 3 seconds', async () => {
    vi.useFakeTimers();
    
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockResolvedValue({
      logs: [],
      totalFiles: 1,
      successCount: 1,
      errorCount: 0,
      warningCount: 0,
    });
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    
    // Start import
    await act(async () => {
      fireEvent.click(button);
      await vi.runAllTimersAsync();
    });
    
    // Verify success state
    expect(button.textContent).toContain('✅');
    
    // Fast-forward time by 3 seconds
    await act(async () => {
      vi.advanceTimersByTime(3000);
      await vi.runAllTimersAsync();
    });
    
    // Should return to idle state
    expect(button.textContent).toContain('📁');
    expect(button.textContent).toContain('Import Mod ZIP(s)');
    expect(button).toHaveClass('import-button--idle');
  });

  it('should show error state when import fails', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockRejectedValue(new Error('Import failed'));
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportError = vi.fn();
    
    render(<ImportButton onImportError={onImportError} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    // Wait for error state
    await waitFor(() => {
      expect(button.textContent).toContain('❌');
      expect(button.textContent).toContain('Import failed');
      expect(button).toHaveClass('import-button--error');
    });
    
    expect(onImportError).toHaveBeenCalledWith('Import failed');
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
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    
    render(<ImportButton onImportStart={onImportStart} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    await waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
      expect(onImportStart).toHaveBeenCalled();
    });
  });

  it('should not call callbacks when file dialog is cancelled', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue(null);
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: vi.fn(),
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    const onImportComplete = vi.fn();
    
    render(<ImportButton onImportStart={onImportStart} onImportComplete={onImportComplete} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    await waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
    });
    
    // Give it a bit more time to ensure no state changes
    await new Promise(resolve => setTimeout(resolve, 100));
    
    // Should not trigger import start or complete
    expect(onImportStart).not.toHaveBeenCalled();
    expect(onImportComplete).not.toHaveBeenCalled();
    
    // Should remain in idle state
    expect(button).toHaveClass('import-button--idle');
  });

  it('should handle empty file selection', async () => {
    const mockSelectZipFiles = vi.fn().mockResolvedValue([]);
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: vi.fn(),
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    const onImportStart = vi.fn();
    
    render(<ImportButton onImportStart={onImportStart} />);
    
    const button = screen.getByRole('button');
    fireEvent.click(button);
    
    await waitFor(() => {
      expect(mockSelectZipFiles).toHaveBeenCalled();
    });
    
    // Give it a bit more time to ensure no state changes
    await new Promise(resolve => setTimeout(resolve, 100));
    
    expect(onImportStart).not.toHaveBeenCalled();
    expect(button).toHaveClass('import-button--idle');
  });

  it('should be disabled during loading state', async () => {
    let resolveImport: ((value: {
      logs: never[];
      totalFiles: number;
      successCount: number;
      errorCount: number;
      warningCount: number;
    }) => void) | undefined;
    const importPromise = new Promise<{
      logs: never[];
      totalFiles: number;
      successCount: number;
      errorCount: number;
      warningCount: number;
    }>(resolve => {
      resolveImport = resolve;
    });
    
    const mockSelectZipFiles = vi.fn().mockResolvedValue(['/path/to/mod.zip']);
    const mockImportZipFiles = vi.fn().mockReturnValue(importPromise);
    
    window.electronAPI = {
      selectZipFiles: mockSelectZipFiles,
      importZipFiles: mockImportZipFiles,
      getCatalog: vi.fn(),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };
    
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).not.toBeDisabled();
    
    fireEvent.click(button);
    
    await waitFor(() => {
      expect(button).toHaveClass('import-button--loading');
    });
    
    // Button should be disabled during loading
    expect(button).toBeDisabled();
    
    // Cleanup - resolve the promise
    resolveImport!({
      logs: [],
      totalFiles: 1,
      successCount: 1,
      errorCount: 0,
      warningCount: 0,
    });
  });
});
