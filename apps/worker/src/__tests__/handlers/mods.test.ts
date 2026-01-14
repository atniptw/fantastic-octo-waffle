import { describe, it, expect, vi, beforeEach } from 'vitest';
import { handleModsList, handleModVersions } from '../../handlers/mods';

// Mock the thunderstore-client module
vi.mock('@fantastic-octo-waffle/thunderstore-client', () => ({
  getPackageListing: vi.fn(),
  getPackageDetail: vi.fn(),
}));

import { getPackageListing, getPackageDetail } from '@fantastic-octo-waffle/thunderstore-client';

// Helper to create mock requests
function createMockRequest(url: string): Request {
  return new Request(url, {
    headers: {
      'CF-Connecting-IP': '127.0.0.1',
    },
  });
}

describe('Mods handlers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('handleModsList', () => {
    it('should proxy mod list request to new Thunderstore listing API', async () => {
      const mockResponse = {
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
            versions: [],
          },
        ],
      };

      (getPackageListing as any).mockResolvedValueOnce(mockResponse);

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const request = createMockRequest(url.toString());
      const response = await handleModsList(url, request);

      expect(response.status).toBe(200);
      expect(getPackageListing).toHaveBeenCalledWith(
        expect.objectContaining({
          community: 'repo',
          page: 1,
        }),
        expect.objectContaining({
          baseUrl: 'https://thunderstore.io',
          userAgent: expect.any(String),
        })
      );

      const data = await response.json();
      expect(data).toEqual(mockResponse);
    });

    it('should handle query parameter', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&query=head');
      const request = createMockRequest(url.toString());
      await handleModsList(url, request);

      expect(getPackageListing).toHaveBeenCalledWith(
        expect.objectContaining({
          q: 'head',
        }),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - downloads', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=downloads');
      const request = createMockRequest(url.toString());
      await handleModsList(url, request);

      expect(getPackageListing).toHaveBeenCalledWith(
        expect.objectContaining({
          ordering: '-downloads',
        }),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - newest', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=newest');
      const request = createMockRequest(url.toString());
      await handleModsList(url, request);

      expect(getPackageListing).toHaveBeenCalledWith(
        expect.objectContaining({
          ordering: '-date_created',
        }),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - rating', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=rating');
      const request = createMockRequest(url.toString());
      await handleModsList(url, request);

      expect(getPackageListing).toHaveBeenCalledWith(
        expect.objectContaining({
          ordering: '-rating_score',
        }),
        expect.any(Object)
      );
    });

    it('should return 404 for invalid community', async () => {
      (getPackageListing as any).mockRejectedValueOnce(
        new Error('Failed to fetch package listing: 404')
      );

      const url = new URL('http://localhost:8787/api/mods?community=invalid');
      const request = createMockRequest(url.toString());
      const response = await handleModsList(url, request);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_community',
        status: 404,
      });
    });

    it('should return 502 on upstream error', async () => {
      (getPackageListing as any).mockRejectedValueOnce(new Error('Network error'));

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const request = createMockRequest(url.toString());
      const response = await handleModsList(url, request);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'upstream_error',
        status: 502,
      });
    });

    it('should include cache headers', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const request = createMockRequest(url.toString());
      const response = await handleModsList(url, request);

      expect(response.headers.get('Cache-Control')).toContain('max-age=300');
      expect(response.headers.get('Cache-Control')).toContain('stale-while-revalidate');
    });
  });

  describe('handleModVersions', () => {
    it('should proxy mod version request to new Thunderstore package detail API', async () => {
      const mockResponse = {
        namespace: 'TestAuthor',
        name: 'TestMod',
        full_name: 'TestAuthor-TestMod',
        owner: 'TestAuthor',
        package_url: 'https://thunderstore.io/c/repo/p/TestAuthor/TestMod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-01T00:00:00Z',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        categories: ['cosmetics'],
        versions: [
          {
            name: 'TestAuthor-TestMod',
            full_name: 'TestAuthor-TestMod-1.0.0',
            description: 'Test mod',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '1.0.0',
            dependencies: [],
            download_url: 'https://cdn.thunderstore.io/file.zip',
            downloads: 100,
            date_created: '2024-01-01T00:00:00Z',
            website_url: '',
            is_active: true,
            uuid4: 'version-uuid',
            file_size: 1000000,
          },
        ],
      };

      (getPackageDetail as any).mockResolvedValueOnce(mockResponse);

      const url = new URL(
        'http://localhost:8787/api/mod/TestAuthor/TestMod/versions?community=repo'
      );
      const request = createMockRequest(url.toString());
      const response = await handleModVersions(url, request);

      expect(response.status).toBe(200);
      expect(getPackageDetail).toHaveBeenCalledWith(
        'TestAuthor',
        'TestMod',
        'repo',
        expect.objectContaining({
          baseUrl: 'https://thunderstore.io',
          userAgent: expect.any(String),
        })
      );

      const data = await response.json();
      expect(data).toEqual(mockResponse);
    });

    it('should return 400 for invalid path - missing name', async () => {
      const url = new URL('http://localhost:8787/api/mod/TestAuthor/versions');
      const request = createMockRequest(url.toString());
      const response = await handleModVersions(url, request);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_path',
        status: 400,
      });
    });

    it('should return 400 for invalid path - missing versions suffix', async () => {
      const url = new URL('http://localhost:8787/api/mod/TestAuthor/TestMod');
      const request = createMockRequest(url.toString());
      const response = await handleModVersions(url, request);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_path',
        status: 400,
      });
    });

    it('should return 404 for non-existent mod', async () => {
      (getPackageDetail as any).mockRejectedValueOnce(
        new Error('Failed to fetch package detail: 404')
      );

      const url = new URL('http://localhost:8787/api/mod/Unknown/Mod/versions?community=repo');
      const request = createMockRequest(url.toString());
      const response = await handleModVersions(url, request);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'mod_not_found',
        status: 404,
      });
    });

    it('should include cache headers', async () => {
      (getPackageDetail as any).mockResolvedValueOnce({
        namespace: 'Test',
        name: 'Mod',
        full_name: 'Test-Mod',
        owner: 'Test',
        package_url: '',
        date_created: '',
        date_updated: '',
        rating_score: 0,
        is_pinned: false,
        is_deprecated: false,
        categories: [],
        versions: [],
      });

      const url = new URL('http://localhost:8787/api/mod/Test/Mod/versions?community=repo');
      const request = createMockRequest(url.toString());
      const response = await handleModVersions(url, request);

      expect(response.headers.get('Cache-Control')).toContain('max-age=300');
    });
  });
});
