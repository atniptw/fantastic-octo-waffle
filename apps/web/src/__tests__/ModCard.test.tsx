import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { ModCard } from '../components/ModCard';
import type { ThunderstorePackageListing } from '@fantastic-octo-waffle/utils';

describe('ModCard', () => {
  const mockMod: ThunderstorePackageListing = {
    namespace: 'TestAuthor',
    name: 'TestMod',
    full_name: 'TestAuthor-TestMod',
    owner: 'TestAuthor',
    package_url: 'https://thunderstore.io/c/repo/p/TestAuthor/TestMod/',
    date_created: '2024-01-01T00:00:00Z',
    date_updated: '2024-01-01T00:00:00Z',
    uuid4: 'test-uuid',
    rating_score: 85,
    is_pinned: false,
    is_deprecated: false,
    has_nsfw_content: false,
    categories: ['cosmetics'],
    versions: [
      {
        name: 'TestAuthor-TestMod',
        full_name: 'TestAuthor-TestMod-1.0.0',
        description: 'A test mod for testing',
        icon: 'https://example.com/icon.png',
        version_number: '1.0.0',
        dependencies: [],
        download_url: 'https://example.com/mod.zip',
        downloads: 1234,
        date_created: '2024-01-01T00:00:00Z',
        website_url: 'https://example.com',
        is_active: true,
        uuid4: 'version-uuid',
        file_size: 1000000,
      },
    ],
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

    // Total downloads: 1234 should be formatted as 1.2K
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

  it('should render icon when icon is provided', () => {
    const onClick = vi.fn();
    render(<ModCard mod={mockMod} onClick={onClick} />);

    const img = screen.getByAltText('TestMod icon');
    expect(img).toBeDefined();
  });

  it('should handle mod without description', () => {
    const modWithoutDesc: ThunderstorePackageListing = {
      ...mockMod,
      versions: [
        {
          ...mockMod.versions[0]!,
          description: '',
        },
      ],
    };
    const onClick = vi.fn();
    render(<ModCard mod={modWithoutDesc} onClick={onClick} />);

    expect(screen.getByText('No description available')).toBeDefined();
  });

  it('should handle mod without versions', () => {
    const modWithoutVersions: ThunderstorePackageListing = { ...mockMod, versions: [] };
    const onClick = vi.fn();
    render(<ModCard mod={modWithoutVersions} onClick={onClick} />);

    expect(screen.getByText('TestMod')).toBeDefined();
    expect(screen.getByText('No description available')).toBeDefined();
  });
});
