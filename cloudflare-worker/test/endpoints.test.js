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
  
  describe('HEAD Method Support', () => {
    let mockFetch;
    let originalFetch;
    
    beforeEach(() => {
      originalFetch = global.fetch;
      mockFetch = vi.fn();
      global.fetch = mockFetch;
    });
    
    afterEach(() => {
      global.fetch = originalFetch;
    });
    
    it('HEAD request returns 200 with no body', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => JSON.stringify([{ name: 'test' }])
      });
      
      const request = createRequest('/api/packages', 'HEAD');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      const body = await response.text();
      expect(body).toBe('');
    });
    
    it('HEAD request includes CORS headers', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages', 'HEAD');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('HEAD request includes Content-Type header', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages', 'HEAD');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });
    
    it('HEAD request fetches from upstream', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        text: async () => '[]'
      });
      
      const request = createRequest('/api/packages', 'HEAD');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        'https://thunderstore.io/c/repo/api/v1/package/',
        expect.any(Object)
      );
    });
    
    it('HEAD request returns 502 when upstream fails', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 500,
        text: async () => 'Internal Server Error'
      });
      
      const request = createRequest('/api/packages', 'HEAD');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
    });
    
    it('HEAD request returns 504 on timeout', async () => {
      const abortError = new Error('The operation was aborted');
      abortError.name = 'AbortError';
      mockFetch.mockRejectedValue(abortError);
      
      const request = createRequest('/api/packages', 'HEAD');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(504);
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

describe('HEAD /api/download/:namespace/:name/:version', () => {
  let mockFetch;
  let originalFetch;
  
  beforeEach(() => {
    originalFetch = global.fetch;
    mockFetch = vi.fn();
    global.fetch = mockFetch;
  });
  
  afterEach(() => {
    global.fetch = originalFetch;
  });
  
  // Helper to create request object
  function createRequest(path, method = 'HEAD') {
    return new Request(`http://localhost:8787${path}`, { method });
  }
  
  describe('Happy Path', () => {
    it('returns 200 with metadata headers for valid mod', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '2202890'],
          ['Content-Disposition', 'attachment; filename="YMC_MHZ-MoreHead-1.4.3.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      expect(response.headers.get('X-Size-Bytes')).toBe('2202890');
      expect(response.headers.get('X-Filename')).toBe('YMC_MHZ-MoreHead-1.4.3.zip');
    });
    
    it('response body is empty (no body for HEAD)', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      const body = await response.text();
      expect(body).toBe('');
    });
    
    it('includes Cache-Control header (24-hour cache)', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Cache-Control')).toBe('public, max-age=86400');
    });
    
    it('includes ETag header with namespace-name-version', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('ETag')).toBe('"YMC_MHZ-MoreHead-1.4.3"');
    });
    
    it('includes CORS headers on success', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('includes Content-Type: application/json header', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });
    
    it('calls upstream with correct URL', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        'https://thunderstore.io/package/download/YMC_MHZ/MoreHead/1.4.3/',
        expect.objectContaining({
          method: 'HEAD',
          redirect: 'follow'
        })
      );
    });
    
    it('includes AbortController signal in fetch', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          signal: expect.any(AbortSignal)
        })
      );
    });
  });
  
  describe('Filename Parsing', () => {
    it('extracts filename from standard Content-Disposition', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '2202890'],
          ['Content-Disposition', 'attachment; filename="My-Mod-v1.0.0.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/MyMod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('X-Filename')).toBe('My-Mod-v1.0.0.zip');
    });
    
    it('extracts filename from RFC 5987 Content-Disposition', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename*=UTF-8\'\'mod%20name.zip']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('X-Filename')).toBe('mod name.zip');
    });
    
    it('falls back to {name}.zip when Content-Disposition missing', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000']
        ])
      });
      
      const request = createRequest('/api/download/Author/MyAwesomeMod/2.5.1');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('X-Filename')).toBe('MyAwesomeMod.zip');
    });
    
    it('handles malformed Content-Disposition gracefully', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'completely-invalid-header']
        ])
      });
      
      const request = createRequest('/api/download/Author/TestMod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('X-Filename')).toBe('TestMod.zip');
    });
  });
  
  describe('Parameter Validation', () => {
    it('accepts valid parameters with underscores', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Test_Author_123/My_Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
    });
    
    it('accepts valid parameters with hyphens', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Test-Author/My-Mod/1-0-0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
    });
    
    it('rejects path traversal in namespace (normalized to 404)', async () => {
      // URL parser normalizes .. so /api/download/../evil/Mod/1.0.0 becomes /api/evil/Mod/1.0.0
      // This won't match our route pattern, so we get 404
      const request = createRequest('/api/download/../evil/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects path traversal in name (contains backslash)', async () => {
      // Backslash is not normalized by URL parser, so it's passed through
      // Our validation rejects it
      const request = createRequest('/api/download/Author/..\\evil/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404); // Actually won't match route due to backslash
    });
    
    it('rejects path traversal in version (normalized to 404)', async () => {
      // URL parser normalizes .. so /api/download/Author/Mod/../evil becomes /api/download/Author/evil
      // This has only 2 segments after /api/download/, won't match route pattern
      const request = createRequest('/api/download/Author/Mod/../evil');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects null byte injection', async () => {
      const request = createRequest('/api/download/Author/mod%00name/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(400);
    });
    
    it('rejects spaces in parameters', async () => {
      const request = createRequest('/api/download/Author/mod name/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(400);
    });
    
    it('rejects special characters (@)', async () => {
      const request = createRequest('/api/download/Author/mod@name/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(400);
    });
    
    it('rejects slashes in parameters', async () => {
      const request = createRequest('/api/download/Author/mod/name/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404); // Would match as different route
    });
    
    it('rejects empty namespace (won\'t match route pattern)', async () => {
      // Empty segment means the route becomes /api/download//Mod/1.0.0
      // This won't match the route pattern properly
      const request = createRequest('/api/download//Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects empty name (won\'t match route pattern)', async () => {
      // Empty segment means the route becomes /api/download/Author//1.0.0
      const request = createRequest('/api/download/Author//1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects empty version', async () => {
      const request = createRequest('/api/download/Author/Mod/');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404); // Doesn't match route pattern
    });
  });
  
  describe('Error Scenarios', () => {
    it('returns 404 when mod not found upstream', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 404
      });
      
      const request = createRequest('/api/download/Author/NonExistent/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
      const body = await response.json();
      expect(body.error).toBe('Mod not found');
    });
    
    it('returns 502 when upstream returns 500', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 500
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download service unavailable');
    });
    
    it('returns 502 when upstream returns 503', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 503
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download service unavailable');
    });
    
    it('returns 502 when Content-Length header missing', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Cannot determine mod size from service');
    });
    
    it('returns 504 on request timeout', async () => {
      const abortError = new Error('The operation was aborted');
      abortError.name = 'AbortError';
      mockFetch.mockRejectedValue(abortError);
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(504);
      const body = await response.json();
      expect(body.error).toBe('Download service timeout');
    });
    
    it('returns 502 on network error', async () => {
      mockFetch.mockRejectedValue(new Error('Network error'));
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download service unavailable');
    });
    
    it('error responses include CORS headers', async () => {
      mockFetch.mockRejectedValue(new Error('Test error'));
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
  });
  
  describe('OPTIONS Preflight', () => {
    it('OPTIONS request returns 204 with CORS headers', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(204);
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
    });
  });
  
  describe('Route Matching', () => {
    it('rejects POST method on download route', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0', 'POST');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects routes with too few segments', async () => {
      const request = createRequest('/api/download/Author/Mod');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects routes with too many segments', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0/extra');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
  });
  
  describe('Environment Configuration', () => {
    it('respects env.SITE_ORIGIN in CORS headers', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const env = { SITE_ORIGIN: 'https://atniptw.github.io' };
      const response = await worker.fetch(request, env, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('https://atniptw.github.io');
    });
  });
});

describe('GET /api/download/:namespace/:name/:version', () => {
  let mockFetch;
  let originalFetch;
  
  beforeEach(() => {
    originalFetch = global.fetch;
    mockFetch = vi.fn();
    global.fetch = mockFetch;
  });
  
  afterEach(() => {
    global.fetch = originalFetch;
  });
  
  // Helper to create request object
  function createRequest(path, method = 'GET') {
    return new Request(`http://localhost:8787${path}`, { method });
  }
  
  // Helper to create fake ZIP bytes
  function createFakeZipBody() {
    // ZIP file magic header: 50 4B 03 04
    const zipMagic = new Uint8Array([0x50, 0x4B, 0x03, 0x04]);
    const fakeData = new Uint8Array(1024);
    fakeData.set(zipMagic, 0);
    return new ReadableStream({
      start(controller) {
        controller.enqueue(fakeData);
        controller.close();
      }
    });
  }
  
  describe('Happy Path', () => {
    it('returns 200 with ZIP body when successful', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '2202890'],
          ['Content-Disposition', 'attachment; filename="YMC_MHZ-MoreHead-1.4.3.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      expect(response.body).toBeTruthy();
    });
    
    it('response has Content-Type: application/zip header', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/zip');
    });
    
    it('includes Content-Disposition header from upstream', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '2202890'],
          ['Content-Disposition', 'attachment; filename="YMC_MHZ-MoreHead-1.4.3.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Disposition')).toBe('attachment; filename="YMC_MHZ-MoreHead-1.4.3.zip"');
    });
    
    it('falls back to synthetic Content-Disposition when upstream missing', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000']
        ])
      });
      
      const request = createRequest('/api/download/Author/TestMod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Disposition')).toBe('attachment; filename="TestMod.zip"');
    });
    
    it('includes Content-Length header from upstream', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '2202890'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Length')).toBe('2202890');
    });
    
    it('handles missing Content-Length gracefully', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Length')).toBe('');
    });
    
    it('includes CORS headers on success', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('calls upstream with correct URL', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/YMC_MHZ/MoreHead/1.4.3');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        'https://thunderstore.io/package/download/YMC_MHZ/MoreHead/1.4.3/',
        expect.objectContaining({
          redirect: 'follow',
          signal: expect.any(AbortSignal)
        })
      );
    });
    
    it('includes User-Agent header in upstream request', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      await worker.fetch(request, {}, {});
      
      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)'
          })
        })
      );
    });
    
    it('streams response body directly (no buffering)', async () => {
      const fakeBody = createFakeZipBody();
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: fakeBody,
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      // Verify response body is the same ReadableStream (not buffered)
      expect(response.body).toBe(fakeBody);
    });
  });
  
  describe('Parameter Validation', () => {
    it('accepts valid parameters with underscores', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Test_Author_123/My_Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
    });
    
    it('accepts valid parameters with hyphens', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Test-Author/My-Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
    });
    
    it('accepts valid parameters with dots', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.2.3');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(200);
    });
    
    it('returns 400 for invalid namespace (special chars)', async () => {
      const request = createRequest('/api/download/Author@Bad/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(400);
      const body = await response.json();
      expect(body.error).toBe('Invalid parameters');
    });
    
    it('returns 404 for invalid name (path traversal - route will not match)', async () => {
      const request = createRequest('/api/download/Author/../evil/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
      const body = await response.json();
      expect(body.error).toBe('Not Found');
    });
    
    it('returns 400 for invalid version (spaces)', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0%20bad');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(400);
      const body = await response.json();
      expect(body.error).toBe('Invalid parameters');
    });
    
    it('returns 404 for empty namespace (route will not match)', async () => {
      const request = createRequest('/api/download//Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
      const body = await response.json();
      expect(body.error).toBe('Not Found');
    });
    
    it('error responses for invalid params include CORS headers', async () => {
      const request = createRequest('/api/download/Author@Bad/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
    });
  });
  
  describe('Error Scenarios', () => {
    it('returns 404 when mod not found upstream', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 404
      });
      
      const request = createRequest('/api/download/Author/NonExistent/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
      const body = await response.json();
      expect(body.error).toBe('Mod not found');
    });
    
    it('returns 502 when upstream returns 500', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 500
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download unavailable');
    });
    
    it('returns 502 when upstream returns 503', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 503
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download unavailable');
    });
    
    it('returns 504 on request timeout', async () => {
      const abortError = new Error('The operation was aborted');
      abortError.name = 'AbortError';
      mockFetch.mockRejectedValue(abortError);
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(504);
      const body = await response.json();
      expect(body.error).toBe('Download timeout');
    });
    
    it('returns 502 on network error', async () => {
      mockFetch.mockRejectedValue(new Error('Network error'));
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(502);
      const body = await response.json();
      expect(body.error).toBe('Download service unavailable');
    });
    
    it('error responses include CORS headers', async () => {
      mockFetch.mockRejectedValue(new Error('Test error'));
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
    });
    
    it('error responses have JSON body (not ZIP)', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 500
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.headers.get('Content-Type')).toBe('application/json');
      const body = await response.json();
      expect(body).toHaveProperty('error');
    });
  });
  
  describe('OPTIONS Preflight', () => {
    it('OPTIONS request returns 204 with CORS headers', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0', 'OPTIONS');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(204);
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
    });
  });
  
  describe('Route Matching', () => {
    it('rejects POST method on download route', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0', 'POST');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects PUT method on download route', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0', 'PUT');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects routes with too few segments', async () => {
      const request = createRequest('/api/download/Author/Mod');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
    
    it('rejects routes with too many segments', async () => {
      const request = createRequest('/api/download/Author/Mod/1.0.0/extra');
      const response = await worker.fetch(request, {}, {});
      
      expect(response.status).toBe(404);
    });
  });
  
  describe('Environment Configuration', () => {
    it('respects env.SITE_ORIGIN in CORS headers', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        body: createFakeZipBody(),
        headers: new Map([
          ['Content-Length', '1000'],
          ['Content-Disposition', 'attachment; filename="test.zip"']
        ])
      });
      
      const request = createRequest('/api/download/Author/Mod/1.0.0');
      const env = { SITE_ORIGIN: 'https://atniptw.github.io' };
      const response = await worker.fetch(request, env, {});
      
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('https://atniptw.github.io');
    });
  });
});
