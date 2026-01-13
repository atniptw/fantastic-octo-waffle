import { describe, it, expect, vi, beforeEach } from 'vitest';
import worker from '../index';

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
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [] }),
      });

      const request = new Request('http://localhost:8787/api/mods?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalled();
    });

    it('should route api/mod/:namespace/:name/versions to mods handler', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ namespace: 'Test', name: 'Mod', versions: [] }),
      });

      const request = new Request('http://localhost:8787/api/mod/Test/Mod/versions?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalled();
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
      globalThis.fetch = vi.fn().mockRejectedValue(new Error('Unexpected error'));

      const request = new Request('http://localhost:8787/api/mods?community=repo');
      const response = await worker.fetch(request, mockEnv);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toHaveProperty('error');
      expect(data).toHaveProperty('message');
    });
  });
});
