/**
 * Unit tests for Cloudflare Worker endpoints
 * Tests GET /api/packages endpoint with mocked fetch
 */
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import worker from '../worker.js';

describe('GET /api/packages', () => {
  let mockFetch;
  let originalFetch;
  
  beforeEach(() => {
    // Save original fetch and create mock
    originalFetch = global.fetch;
    mockFetch = vi.fn();
    global.fetch = mockFetch;
  });
  
  afterEach(() => {
    // Restore original fetch
    global.fetch = originalFetch;
  });
  
  // Helper to create request object
  function createRequest(path, method = 'GET') {
    return new Request(`http://localhost:8787${path}`, { method });
  }
  
  describe('Happy Path', () => {
    it('returns 200 with JSON array when upstream succeeds', async () => {
      const mockData = [{ name: 'test-mod', owner: 'test-author' }];
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify(mockData)
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body).toEqual(mockData);
    });
    
    it('response has Content-Type: application/json header', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify([])
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });
    
    it('CORS headers present in successful response', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify([])
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('response body is valid JSON array', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify([{ test: 'data' }])
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      const body = await response.json();
      expect(Array.isArray(body)).toBe(true);
    });
    
    it('preserves upstream response body when successful', async () => {
      const upstreamBody = JSON.stringify([
        { name: 'mod1', owner: 'author1' },
        { name: 'mod2', owner: 'author2' }
      ]);
      
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => upstreamBody
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      const body = await response.text();
      expect(body).toBe(upstreamBody);
    });
    
    it('calls upstream with correct URL', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        'https://thunderstore.io/c/repo/api/v1/package/',
        expect.objectContaining({
          headers: expect.objectContaining({
            'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)',
            'Accept': 'application/json'
          })
        })
      );
    });
    
    it('includes AbortController signal in fetch', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          signal: expect.any(AbortSignal)
        })
      );
    });
  });
  
  describe('Preflight Handling', () => {
    it('OPTIONS request returns 204 with CORS headers', async () => {
      const request = createRequest('/api/packages', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(204);
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('preflight includes all required CORS headers', async () => {
      const request = createRequest('/api/packages', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.has('Access-Control-Allow-Origin')).toBe(true);
      expect(response.headers.has('Access-Control-Allow-Methods')).toBe(true);
      expect(response.headers.has('Access-Control-Allow-Headers')).toBe(true);
    });
    
    it('preflight response has no body', async () => {
      const request = createRequest('/api/packages', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      const text = await response.text();
      expect(text).toBe('');
    });
    
    it('preflight works for other routes too', async () => {
      const request = createRequest('/api/unknown', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(204);
    });
  });
  
  describe('Error Scenarios', () => {
    it('returns 502 when upstream returns 500', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 500,
        text: async () => 'Internal Server Error'
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Upstream service unavailable');
    });
    
    it('returns 502 when upstream returns 503', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 503,
        text: async () => 'Service Unavailable'
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Upstream service unavailable');
    });
    
    it('returns 502 when upstream returns 404', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 404,
        text: async () => 'Not Found'
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
    });
    
    it('returns 502 when network error occurs', async () => {
      mockFetch.mockRejectedValue(new Error('Network error'));
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Upstream service unavailable');
    });
    
    it('returns 504 when fetch times out', async () => {
      const abortError = new Error('The operation was aborted');
      abortError.name = 'AbortError';
      mockFetch.mockRejectedValue(abortError);
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(504);
      const body = await response.json();
      expect(body.error).toBe('Upstream service timeout');
    });
    
    it('error responses include CORS headers', async () => {
      mockFetch.mockRejectedValue(new Error('Test error'));
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('error responses match { error: "message" } format', async () => {
      mockFetch.mockRejectedValue(new Error('Test error'));
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      const body = await response.json();
      expect(body).toHaveProperty('error');
      expect(typeof body.error).toBe('string');
      expect(body).not.toHaveProperty('status');
      expect(body).not.toHaveProperty('message');
    });
    
    it('error responses have Content-Type: application/json', async () => {
      mockFetch.mockRejectedValue(new Error('Test error'));
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });
  });
  
  describe('Edge Cases', () => {
    it('handles upstream returning non-JSON (proxies as-is)', async () => {
      const plainText = 'This is not JSON';
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => plainText
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      const body = await response.text();
      expect(body).toBe(plainText);
    });
    
    it('handles empty upstream response', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body).toEqual([]);
    });
    
    it('handles large upstream response', async () => {
      const largeArray = Array(1000).fill({ name: 'mod', owner: 'author' });
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify(largeArray)
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body.length).toBe(1000);
    });
  });
  
  describe('Environment Configuration', () => {
    it('respects env.SITE_ORIGIN in CORS headers', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages');
      const env = { SITE_ORIGIN: 'https://atniptw.github.io' };
      const response = await worker.fetch(request, env, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('https://atniptw.github.io');
    });
    
    it('defaults to * when env.SITE_ORIGIN is not set', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
    });
  });
});

describe('Route Handling', () => {
  it('returns 404 for unknown routes', async () => {
    const request = new Request('http://localhost:8787/api/unknown', { method: 'GET' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body.error).toBe('Not Found');
  });
  
  it('404 response includes CORS headers', async () => {
    const request = new Request('http://localhost:8787/api/unknown', { method: 'GET' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
  });
  
  it('404 response has Content-Type: application/json', async () => {
    const request = new Request('http://localhost:8787/api/unknown', { method: 'GET' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.headers.get('Content-Type')).toBe('application/json');
  });
  
  it('rejects POST method on /api/packages', async () => {
    const request = new Request('http://localhost:8787/api/packages', { method: 'POST' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.status).toBe(404);
  });
  
  it('rejects PUT method on /api/packages', async () => {
    const request = new Request('http://localhost:8787/api/packages', { method: 'PUT' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.status).toBe(404);
  });
  
  it('rejects DELETE method on /api/packages', async () => {
    const request = new Request('http://localhost:8787/api/packages', { method: 'DELETE' });
    const response = await worker.fetch(request, {}, {});
    
    expect(response.status).toBe(404);
  });
});
