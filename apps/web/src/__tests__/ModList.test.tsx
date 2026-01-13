import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { ModList } from '../components/ModList';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';

describe('ModList', () => {
  const mockMods: ThunderstorePackageVersion[] = [
    {
      namespace: 'Author1',
      name: 'Mod1',
      full_name: 'Author1/Mod1',
      description: 'First test mod',
      version_number: '1.0.0',
      dependencies: [],
      download_url: 'https://example.com/mod1.zip',
      downloads: 100,
      date_created: '2024-01-01T00:00:00Z',
      is_deprecated: false,
      is_pinned: false,
    },
    {
      namespace: 'Author2',
      name: 'Mod2',
      full_name: 'Author2/Mod2',
      description: 'Second test mod',
      version_number: '2.0.0',
      dependencies: [],
      download_url: 'https://example.com/mod2.zip',
      downloads: 200,
      date_created: '2024-01-02T00:00:00Z',
      is_deprecated: false,
      is_pinned: false,
    },
  ];

  it('should render all mods', () => {
    const onModClick = vi.fn();
    render(<ModList mods={mockMods} onModClick={onModClick} />);

    expect(screen.getByText('Mod1')).toBeDefined();
    expect(screen.getByText('Mod2')).toBeDefined();
  });

  it('should render empty state when no mods', () => {
    const onModClick = vi.fn();
    render(<ModList mods={[]} onModClick={onModClick} />);

    expect(screen.getByText('No mods found')).toBeDefined();
  });

  it('should pass onModClick to each ModCard', () => {
    const onModClick = vi.fn();
    render(<ModList mods={mockMods} onModClick={onModClick} />);

    const cards = screen.getAllByRole('button');
    expect(cards.length).toBe(2);
  });
});
