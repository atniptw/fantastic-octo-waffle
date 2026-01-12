import { describe, it, expect, vi, beforeEach } from 'vitest';
import { handleModsList, handleModVersions } from '../../handlers/mods';

describe('Mods handlers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('handleModsList', () => {
    it('should proxy mod list request to Thunderstore', async () => {
      const mockResponse = {
        count: 2,
        results: [
          {
            namespace: 'TestAuthor',
            name: 'TestMod',
            full_name: 'TestAuthor/TestMod',
          },
        ],
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockResponse,
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const response = await handleModsList(url);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('thunderstore.io/api/experimental/frontend/c/repo'),
        expect.any(Object)
      );

      const data = await response.json();
      expect(data).toEqual(mockResponse);
    });

    it('should handle query parameter', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&query=head');
      await handleModsList(url);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('q=head'),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - downloads', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=downloads');
      await handleModsList(url);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('ordering=-downloads'),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - newest', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=newest');
      await handleModsList(url);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('ordering=-date_created'),
        expect.any(Object)
      );
    });

    it('should handle sort parameter - rating', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo&sort=rating');
      await handleModsList(url);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('ordering=-rating'),
        expect.any(Object)
      );
    });

    it('should return 404 for invalid community', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
      });

      const url = new URL('http://localhost:8787/api/mods?community=invalid');
      const response = await handleModsList(url);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_community',
        status: 404,
      });
    });

    it('should return 502 on upstream error', async () => {
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('Network error'));

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const response = await handleModsList(url);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'upstream_error',
        status: 502,
      });
    });

    it('should include cache headers', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const response = await handleModsList(url);

      expect(response.headers.get('Cache-Control')).toContain('max-age=300');
      expect(response.headers.get('Cache-Control')).toContain('stale-while-revalidate');
    });
  });

  describe('handleModVersions', () => {
    it('should proxy mod version request to Thunderstore', async () => {
      const mockResponse = {
        namespace: 'TestAuthor',
        name: 'TestMod',
        versions: [{ number: '1.0.0', downloads: 100 }],
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockResponse,
      });

      const url = new URL(
        'http://localhost:8787/api/mod/TestAuthor/TestMod/versions?community=repo'
      );
      const response = await handleModVersions(url);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('TestAuthor/TestMod'),
        expect.any(Object)
      );

      const data = await response.json();
      expect(data).toEqual(mockResponse);
    });

    it('should return 400 for invalid path - missing name', async () => {
      const url = new URL('http://localhost:8787/api/mod/TestAuthor/versions');
      const response = await handleModVersions(url);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_path',
        status: 400,
      });
    });

    it('should return 400 for invalid path - missing versions suffix', async () => {
      const url = new URL('http://localhost:8787/api/mod/TestAuthor/TestMod');
      const response = await handleModVersions(url);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_path',
        status: 400,
      });
    });

    it('should return 404 for non-existent mod', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
      });

      const url = new URL(
        'http://localhost:8787/api/mod/Unknown/Mod/versions?community=repo'
      );
      const response = await handleModVersions(url);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'mod_not_found',
        status: 404,
      });
    });

    it('should include cache headers', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ namespace: 'Test', name: 'Mod', versions: [] }),
      });

      const url = new URL(
        'http://localhost:8787/api/mod/Test/Mod/versions?community=repo'
      );
      const response = await handleModVersions(url);

      expect(response.headers.get('Cache-Control')).toContain('max-age=300');
    });
  });
});
