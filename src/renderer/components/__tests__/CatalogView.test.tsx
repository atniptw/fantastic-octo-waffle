import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import CatalogView from '../CatalogView';

// Default props to ensure component renders without errors
const defaultProps = {
  mods: [],
  cosmetics: [],
};

describe('CatalogView', () => {
  it('should render catalog header', () => {
    render(<CatalogView {...defaultProps} />);

    expect(screen.getByText('Cosmetics Catalog')).toBeInTheDocument();
  });

  it('should display empty message when no cosmetics', () => {
    render(<CatalogView {...defaultProps} />);

    expect(screen.getByText(/import mod zip files to populate the catalog/i)).toBeInTheDocument();
  });

  it('should display cosmetics when available', () => {
    render(
      <CatalogView
        mods={[
          {
            id: '1',
            mod_name: 'Test Mod',
            author: 'Author1',
            version: '1.0.0',
            iconData: null,
            source: 'test.zip',
          },
        ]}
        cosmetics={[
          {
            id: '1',
            mod_id: '1',
            display_name: 'Cool Hat',
            filename: 'cool_hat.hhh',
            hash: 'abc123',
            type: 'decoration',
            internal_path: 'plugins/Test/Decorations/cool_hat.hhh',
          },
        ]}
      />
    );

    expect(screen.getByText('Cool Hat')).toBeInTheDocument();
    expect(screen.getByText('by Author1')).toBeInTheDocument();
    expect(screen.getByText('cool_hat.hhh')).toBeInTheDocument();
  });

  it('should have search input', () => {
    render(<CatalogView {...defaultProps} />);

    expect(screen.getByPlaceholderText('Search cosmetics...')).toBeInTheDocument();
  });

  it('should show clear button when search query exists', () => {
    render(<CatalogView {...defaultProps} />);

    const searchInput = screen.getByPlaceholderText('Search cosmetics...');

    // Initially no clear button
    expect(screen.queryByText('✕ Clear')).not.toBeInTheDocument();

    // Type in search
    fireEvent.change(searchInput, { target: { value: 'test' } });

    // Clear button should appear
    expect(screen.getByText('✕ Clear')).toBeInTheDocument();
  });

  it('should clear search when clear button clicked', () => {
    render(<CatalogView {...defaultProps} />);

    const searchInput = screen.getByPlaceholderText('Search cosmetics...') as HTMLInputElement;

    // Type in search
    fireEvent.change(searchInput, { target: { value: 'test' } });
    expect(searchInput.value).toBe('test');

    // Click clear
    const clearButton = screen.getByText('✕ Clear');
    fireEvent.click(clearButton);

    // Search should be cleared
    expect(searchInput.value).toBe('');
  });

  it('should display stats correctly', () => {
    render(
      <CatalogView
        mods={[
          {
            id: '1',
            mod_name: 'Mod1',
            author: 'A',
            version: '1.0.0',
            iconData: null,
            source: 'a.zip',
          },
          {
            id: '2',
            mod_name: 'Mod2',
            author: 'B',
            version: '1.0.0',
            iconData: null,
            source: 'b.zip',
          },
        ]}
        cosmetics={[
          {
            id: '1',
            mod_id: '1',
            display_name: 'C1',
            filename: 'c1.hhh',
            hash: 'h1',
            type: 'decoration',
            internal_path: 'p1',
          },
          {
            id: '2',
            mod_id: '1',
            display_name: 'C2',
            filename: 'c2.hhh',
            hash: 'h2',
            type: 'decoration',
            internal_path: 'p2',
          },
          {
            id: '3',
            mod_id: '2',
            display_name: 'C3',
            filename: 'c3.hhh',
            hash: 'h3',
            type: 'decoration',
            internal_path: 'p3',
          },
        ]}
      />
    );

    expect(screen.getByText('2 mod(s) • 3 cosmetic(s)')).toBeInTheDocument();
  });

  it('should filter cosmetics based on search query', () => {
    render(
      <CatalogView
        mods={[
          {
            id: '1',
            mod_name: 'Test Mod',
            author: 'Author1',
            version: '1.0.0',
            iconData: null,
            source: 'test.zip',
          },
        ]}
        cosmetics={[
          {
            id: '1',
            mod_id: '1',
            display_name: 'Cool Hat',
            filename: 'cool_hat.hhh',
            hash: 'abc123',
            type: 'decoration',
            internal_path: 'path1',
          },
          {
            id: '2',
            mod_id: '1',
            display_name: 'Nice Shoes',
            filename: 'shoes.hhh',
            hash: 'def456',
            type: 'decoration',
            internal_path: 'path2',
          },
        ]}
      />
    );

    // Both should be visible initially
    expect(screen.getByText('Cool Hat')).toBeInTheDocument();
    expect(screen.getByText('Nice Shoes')).toBeInTheDocument();

    // Search for "hat"
    const searchInput = screen.getByPlaceholderText('Search cosmetics...');
    fireEvent.change(searchInput, { target: { value: 'hat' } });

    // Only Cool Hat should be visible
    expect(screen.getByText('Cool Hat')).toBeInTheDocument();
    expect(screen.queryByText('Nice Shoes')).not.toBeInTheDocument();
  });
});
