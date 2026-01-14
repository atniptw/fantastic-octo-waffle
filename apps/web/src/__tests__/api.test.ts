import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { fetchMods } from '../lib/api';
import type { ThunderstoreApiResponse } from '@fantastic-octo-waffle/utils';

describe('API client', () => {
  const mockResponse: ThunderstoreApiResponse = {
    count: 2,
    next: null,
    previous: null,
    results: [
      {
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
            description: 'Test description',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '1.0.0',
            dependencies: [],
            download_url: 'https://example.com/mod.zip',
            downloads: 100,
            date_created: '2024-01-01T00:00:00Z',
            is_active: true,
            uuid4: 'version-uuid',
            file_size: 1000000,
          },
        ],
      },
    ],
  };

  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('should fetch mods successfully', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockResponse,
    });

    const result = await fetchMods(1, '', 'downloads');

    expect(result).toEqual(mockResponse);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/mods'),
      expect.any(Object)
    );
  });

  it('should include query parameter in request', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockResponse,
    });

    await fetchMods(1, 'test', 'downloads');

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('query=test'),
      expect.any(Object)
    );
  });

  it('should include sort parameter in request', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockResponse,
    });

    await fetchMods(1, '', 'newest');

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining('sort=newest'),
      expect.any(Object)
    );
  });

  it('should throw error on failed request', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({
        error: 'server_error',
        message: 'Internal server error',
        status: 500,
      }),
    });

    await expect(fetchMods(1, '', 'downloads')).rejects.toThrow('Internal server error');
  });

  it('should handle non-JSON error response', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      json: async () => {
        throw new Error('Not JSON');
      },
    });

    await expect(fetchMods(1, '', 'downloads')).rejects.toThrow('Request failed with status 503');
  });
});
