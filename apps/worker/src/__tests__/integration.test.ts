import { describe, it, expect, vi, beforeEach } from 'vitest';
import worker from '../index';

// Mock the thunderstore-client module
vi.mock('@fantastic-octo-waffle/thunderstore-client', () => ({
  getPackageListing: vi.fn(),
  getPackageDetail: vi.fn(),
}));

import { getPackageListing, getPackageDetail } from '@fantastic-octo-waffle/thunderstore-client';

describe('Worker Integration', () => {
  const mockEnv = {};

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Routing', () => {
    it('should route health requests to health handler', async () => {
      const request = new Request('http://localhost:8787/health');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      const data = await response.json();
      expect(data).toEqual({ status: 'ok' });
    });

    it('should route api/mods requests to mods handler', async () => {
      (getPackageListing as any).mockResolvedValueOnce({
        count: 0,
        next: null,
        previous: null,
        results: [],
      });

      const request = new Request('http://localhost:8787/api/mods?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      expect(getPackageListing).toHaveBeenCalled();
    });

    it('should route api/mod/:namespace/:name/versions to mods handler', async () => {
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

      const request = new Request('http://localhost:8787/api/mod/Test/Mod/versions?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      expect(getPackageDetail).toHaveBeenCalled();
    });

    it('should route proxy requests to proxy handler', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({ 'Content-Type': 'application/zip' }),
      });

      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalled();
    });

    it('should return 404 for unknown routes', async () => {
      const request = new Request('http://localhost:8787/unknown');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'not_found',
        status: 404,
      });
    });
  });

  describe('CORS preflight', () => {
    it('should handle OPTIONS request', async () => {
      const request = new Request('http://localhost:8787/api/mods', {
        method: 'OPTIONS',
      });
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(204);
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
    });
  });

  describe('Error handling', () => {
    it('should catch and report handler errors', async () => {
      (getPackageListing as any).mockRejectedValueOnce(new Error('Unexpected error'));

      const request = new Request('http://localhost:8787/api/mods?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toHaveProperty('error');
      expect(data).toHaveProperty('message');
    });
  });
});
