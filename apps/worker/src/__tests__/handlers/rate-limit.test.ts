import { describe, it, expect, beforeEach, vi } from 'vitest';
import { handleModsList } from '../../handlers/mods';
import { handleProxy } from '../../handlers/proxy';

// Mock the thunderstore-client module
vi.mock('@fantastic-octo-waffle/thunderstore-client', () => ({
  getPackageListing: vi.fn(),
  getPackageDetail: vi.fn(),
}));

import { getPackageListing } from '@fantastic-octo-waffle/thunderstore-client';

// Helper to create mock requests with specific IP
function createMockRequest(url: string, ip: string, options?: RequestInit): Request {
  return new Request(url, {
    ...options,
    headers: {
      ...options?.headers,
      'CF-Connecting-IP': ip,
    },
  });
}

describe('Rate Limiting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Mock thunderstore-client for successful responses
    (getPackageListing as any).mockResolvedValue({
      count: 0,
      next: null,
      previous: null,
      results: [],
    });
    // Mock fetch for proxy requests
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ count: 0, results: [] }),
      headers: new Headers({ 'Content-Type': 'application/json' }),
      body: new ReadableStream(),
    });
  });

  describe('API endpoints rate limiting', () => {
    it('should allow requests within rate limit (100/min)', async () => {
      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const ip = '192.168.1.100';

      // Make multiple requests - should all succeed initially
      for (let i = 0; i < 10; i++) {
        const request = createMockRequest(url.toString(), ip);
        const response = await handleModsList(url, request);
        expect(response.status).toBe(200);
      }
    });

    it('should return 429 when API rate limit exceeded', async () => {
      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const ip = '192.168.1.101'; // Use unique IP to isolate test

      // Exhaust the rate limit (100 requests)
      for (let i = 0; i < 100; i++) {
        const request = createMockRequest(url.toString(), ip);
        await handleModsList(url, request);
      }

      // Next request should be rate limited
      const request = createMockRequest(url.toString(), ip);
      const response = await handleModsList(url, request);

      expect(response.status).toBe(429);
      const data = await response.json();
      expect(data).toMatchObject({
        error: 'rate_limit_exceeded',
        status: 429,
      });
      expect(response.headers.get('Retry-After')).toBe('60');
    });

    it('should track rate limits separately per IP', async () => {
      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const ip1 = '192.168.1.102';
      const ip2 = '192.168.1.103';

      // Exhaust rate limit for ip1
      for (let i = 0; i < 100; i++) {
        const request = createMockRequest(url.toString(), ip1);
        await handleModsList(url, request);
      }

      // ip1 should be rate limited
      const request1 = createMockRequest(url.toString(), ip1);
      const response1 = await handleModsList(url, request1);
      expect(response1.status).toBe(429);

      // ip2 should still work
      const request2 = createMockRequest(url.toString(), ip2);
      const response2 = await handleModsList(url, request2);
      expect(response2.status).toBe(200);
    });
  });

  describe('Proxy endpoint rate limiting', () => {
    it('should allow requests within rate limit (20/min)', async () => {
      const targetUrl = 'https://cdn.thunderstore.io/file/test.zip';
      const url = new URL(`http://localhost:8787/proxy?url=${encodeURIComponent(targetUrl)}`);
      const ip = '192.168.1.104';

      // Make multiple requests - should all succeed initially
      for (let i = 0; i < 10; i++) {
        const request = createMockRequest(url.toString(), ip);
        const response = await handleProxy(url, request);
        expect(response.status).toBe(200);
      }
    });

    it('should return 429 when proxy rate limit exceeded', async () => {
      const targetUrl = 'https://cdn.thunderstore.io/file/test.zip';
      const url = new URL(`http://localhost:8787/proxy?url=${encodeURIComponent(targetUrl)}`);
      const ip = '192.168.1.105'; // Use unique IP to isolate test

      // Exhaust the rate limit (20 requests for proxy)
      for (let i = 0; i < 20; i++) {
        const request = createMockRequest(url.toString(), ip);
        await handleProxy(url, request);
      }

      // Next request should be rate limited
      const request = createMockRequest(url.toString(), ip);
      const response = await handleProxy(url, request);

      expect(response.status).toBe(429);
      const data = await response.json();
      expect(data).toMatchObject({
        error: 'rate_limit_exceeded',
        status: 429,
      });
      expect(response.headers.get('Retry-After')).toBe('60');
    });

    it('should have stricter rate limit for proxy than API', async () => {
      const apiUrl = new URL('http://localhost:8787/api/mods?community=repo');
      const proxyUrl = new URL(
        `http://localhost:8787/proxy?url=${encodeURIComponent('https://cdn.thunderstore.io/file/test.zip')}`
      );
      const ip = '192.168.1.106';

      // Make 20 proxy requests (should exhaust proxy limit)
      for (let i = 0; i < 20; i++) {
        const request = createMockRequest(proxyUrl.toString(), ip);
        await handleProxy(proxyUrl, request);
      }

      // Proxy should be rate limited
      const proxyRequest = createMockRequest(proxyUrl.toString(), ip);
      const proxyResponse = await handleProxy(proxyUrl, proxyRequest);
      expect(proxyResponse.status).toBe(429);

      // But API should still work (has higher limit of 100)
      const apiRequest = createMockRequest(apiUrl.toString(), ip);
      const apiResponse = await handleModsList(apiUrl, apiRequest);
      expect(apiResponse.status).toBe(200);
    });
  });

  describe('Client ID detection', () => {
    it('should use CF-Connecting-IP header', async () => {
      const url = new URL('http://localhost:8787/api/mods?community=repo');
      const ip = '203.0.113.42';

      const request = createMockRequest(url.toString(), ip);
      const response = await handleModsList(url, request);

      expect(response.status).toBe(200);
    });

    it('should fallback to X-Forwarded-For if CF-Connecting-IP missing', async () => {
      const url = new URL('http://localhost:8787/api/mods?community=repo');

      const request = new Request(url.toString(), {
        headers: {
          'X-Forwarded-For': '198.51.100.42, 192.0.2.1',
        },
      });

      const response = await handleModsList(url, request);
      expect(response.status).toBe(200);
    });
  });
});
