import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import CatalogView from '../CatalogView';

describe('CatalogView', () => {
  beforeEach(() => {
    // Clear any previous mocks
    delete window.electronAPI;
  });

  afterEach(() => {
    delete window.electronAPI;
  });

  it('should show placeholder when running outside Electron', () => {
    render(<CatalogView />);
    
    expect(screen.getByText(/catalog view is available in the desktop application/i)).toBeInTheDocument();
  });

  it('should render catalog header when in Electron', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({ mods: [], cosmetics: [] }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    expect(screen.getByText('Cosmetics Catalog')).toBeInTheDocument();
  });

  it('should load catalog on mount', async () => {
    const mockGetCatalog = vi.fn().mockResolvedValue({ mods: [], cosmetics: [] });
    
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: mockGetCatalog,
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    await vi.waitFor(() => {
      expect(mockGetCatalog).toHaveBeenCalled();
    });
  });

  it('should display empty message when no cosmetics', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({ mods: [], cosmetics: [] }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    await vi.waitFor(() => {
      expect(screen.getByText(/import mod zip files to populate the catalog/i)).toBeInTheDocument();
    });
  });

  it('should display cosmetics when available', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({
        mods: [{ id: 1, mod_name: 'Test Mod', author: 'Author1', version: '1.0.0', icon_path: null, source_zip: 'test.zip' }],
        cosmetics: [
          { id: 1, mod_id: 1, display_name: 'Cool Hat', filename: 'cool_hat.hhh', hash: 'abc123', type: 'decoration', internal_path: 'plugins/Test/Decorations/cool_hat.hhh' }
        ],
      }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    await vi.waitFor(() => {
      expect(screen.getByText('Cool Hat')).toBeInTheDocument();
    });
    
    expect(screen.getByText('by Author1')).toBeInTheDocument();
    expect(screen.getByText('cool_hat.hhh')).toBeInTheDocument();
  });

  it('should have search input', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({ mods: [], cosmetics: [] }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    expect(screen.getByPlaceholderText('Search cosmetics...')).toBeInTheDocument();
  });

  it('should call searchCosmetics when search button clicked', async () => {
    const mockSearchCosmetics = vi.fn().mockResolvedValue([]);
    
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({ mods: [], cosmetics: [] }),
      searchCosmetics: mockSearchCosmetics,
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    const searchInput = screen.getByPlaceholderText('Search cosmetics...');
    const searchButton = screen.getByText('🔍 Search');
    
    fireEvent.change(searchInput, { target: { value: 'hat' } });
    fireEvent.click(searchButton);
    
    await vi.waitFor(() => {
      expect(mockSearchCosmetics).toHaveBeenCalledWith('hat');
    });
  });

  it('should show clear button when search query exists', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({ mods: [], cosmetics: [] }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    const searchInput = screen.getByPlaceholderText('Search cosmetics...');
    
    // Initially no clear button
    expect(screen.queryByText('✕ Clear')).not.toBeInTheDocument();
    
    // Type in search
    fireEvent.change(searchInput, { target: { value: 'test' } });
    
    // Clear button should appear
    expect(screen.getByText('✕ Clear')).toBeInTheDocument();
  });

  it('should reload catalog when clear button clicked', async () => {
    const mockGetCatalog = vi.fn().mockResolvedValue({ mods: [], cosmetics: [] });
    
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: mockGetCatalog,
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    const searchInput = screen.getByPlaceholderText('Search cosmetics...');
    
    // Type in search
    fireEvent.change(searchInput, { target: { value: 'test' } });
    
    // Click clear
    const clearButton = screen.getByText('✕ Clear');
    fireEvent.click(clearButton);
    
    await vi.waitFor(() => {
      // getCatalog should be called again (initial + after clear)
      expect(mockGetCatalog).toHaveBeenCalledTimes(2);
    });
  });

  it('should display stats correctly', async () => {
    window.electronAPI = {
      selectZipFiles: vi.fn(),
      importZipFiles: vi.fn(),
      getCatalog: vi.fn().mockResolvedValue({
        mods: [
          { id: 1, mod_name: 'Mod1', author: 'A', version: '1.0.0', icon_path: null, source_zip: 'a.zip' },
          { id: 2, mod_name: 'Mod2', author: 'B', version: '1.0.0', icon_path: null, source_zip: 'b.zip' },
        ],
        cosmetics: [
          { id: 1, mod_id: 1, display_name: 'C1', filename: 'c1.hhh', hash: 'h1', type: 'decoration', internal_path: 'p1' },
          { id: 2, mod_id: 1, display_name: 'C2', filename: 'c2.hhh', hash: 'h2', type: 'decoration', internal_path: 'p2' },
          { id: 3, mod_id: 2, display_name: 'C3', filename: 'c3.hhh', hash: 'h3', type: 'decoration', internal_path: 'p3' },
        ],
      }),
      searchCosmetics: vi.fn(),
      importMods: vi.fn(),
    };

    render(<CatalogView />);
    
    await vi.waitFor(() => {
      expect(screen.getByText('2 mod(s) • 3 cosmetic(s)')).toBeInTheDocument();
    });
  });
});
