import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { App } from '../App';

// Mock fetch globally
beforeEach(() => {
  globalThis.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({
      count: 0,
      next: null,
      previous: null,
      results: [],
    }),
  });
});

describe('Web App', () => {
  it('App component should be defined', () => {
    expect(App).toBeDefined();
    expect(typeof App).toBe('function');
  });

  it('App component should render header', () => {
    render(<App />);
    expect(screen.getByText('REPO Cosmetic Viewer')).toBeDefined();
    expect(screen.getByText('Browse and preview cosmetic mods')).toBeDefined();
  });
});
