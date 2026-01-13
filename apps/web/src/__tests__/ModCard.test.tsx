import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { ModCard } from '../components/ModCard';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';

describe('ModCard', () => {
  const mockMod: ThunderstorePackageVersion = {
    namespace: 'TestAuthor',
    name: 'TestMod',
    full_name: 'TestAuthor/TestMod',
    description: 'A test mod for testing',
    version_number: '1.0.0',
    dependencies: [],
    download_url: 'https://example.com/mod.zip',
    downloads: 1234,
    date_created: '2024-01-01T00:00:00Z',
    icon_url: 'https://example.com/icon.png',
    website_url: 'https://example.com',
    is_deprecated: false,
    is_pinned: false,
    rating_score: 85,
  };

  it('should render mod information', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    expect(screen.getByText('TestMod')).toBeDefined();
    expect(screen.getByText('by TestAuthor')).toBeDefined();
    expect(screen.getByText('A test mod for testing')).toBeDefined();
  });

  it('should call onClick when clicked', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    const card = screen.getByRole('button');
    card.click();

    expect(onClick).toHaveBeenCalledWith(mockMod);
  });

  it('should format download count correctly', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    // Downloads: 1234 should be formatted as 1.2K
    expect(screen.getByText(/1\.2K/)).toBeDefined();
  });

  it('should display rating', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    // Rating score 85 (out of 100) should be 4.3 out of 5
    expect(screen.getByText(/4\.3.*★/)).toBeDefined();
  });

  it('should display version number', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    expect(screen.getByText(/v1\.0\.0/)).toBeDefined();
  });

  it('should render icon when icon_url is provided', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    const img = screen.getByAltText('TestMod icon');
    expect(img).toBeDefined();
  });

  it('should handle mod without description', () => {
    const modWithoutDesc = { ...mockMod, description: '' };
    const onClick = vi.fn();
    render(<ModCard mod={modWithoutDesc} onClick={onClick} />);

    expect(screen.getByText('No description available')).toBeDefined();
  });
});
