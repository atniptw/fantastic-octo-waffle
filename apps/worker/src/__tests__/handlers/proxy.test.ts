import { describe, it, expect, vi, beforeEach } from 'vitest';
import { handleProxy } from '../../handlers/proxy';

describe('Proxy handler', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('URL validation', () => {
    it('should return 400 when URL is missing', async () => {
      const url = new URL('http://localhost:8787/proxy');
      const request = new Request('http://localhost:8787/proxy');
      const response = await handleProxy(url, request);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'missing_url',
        status: 400,
      });
    });

    it('should return 400 for invalid URL format', async () => {
      const url = new URL('http://localhost:8787/proxy?url=not-a-valid-url');
      const request = new Request('http://localhost:8787/proxy?url=not-a-valid-url');
      const response = await handleProxy(url, request);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_url',
        status: 400,
      });
    });

    it('should reject non-HTTPS URLs', async () => {
      const url = new URL(
        'http://localhost:8787/proxy?url=http://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=http://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(400);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_protocol',
        status: 400,
      });
    });

    it('should reject URLs not on allowlist', async () => {
      const url = new URL('http://localhost:8787/proxy?url=https://evil.com/malware.zip');
      const request = new Request('http://localhost:8787/proxy?url=https://evil.com/malware.zip');
      const response = await handleProxy(url, request);

      expect(response.status).toBe(403);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'host_not_allowed',
        status: 403,
      });
    });
  });

  describe('Allowlisted hosts', () => {
    beforeEach(() => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': '1000',
        }),
      });
    });

    it('should accept cdn.thunderstore.io', async () => {
      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
    });

    it('should accept thunderstore.io', async () => {
      const url = new URL(
        'http://localhost:8787/proxy?url=https://thunderstore.io/package/download/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://thunderstore.io/package/download/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
    });

    it('should accept gcdn.thunderstore.io', async () => {
      const url = new URL(
        'http://localhost:8787/proxy?url=https://gcdn.thunderstore.io/live/repository/packages/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://gcdn.thunderstore.io/live/repository/packages/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
    });

    it('should accept subdomains of allowed hosts', async () => {
      const url = new URL(
        'http://localhost:8787/proxy?url=https://sub.cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://sub.cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
    });
  });

  describe('Download proxying', () => {
    it('should proxy allowed download URL', async () => {
      const mockBody = new ReadableStream();
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: mockBody,
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': '1000000',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toBe('application/zip');
      expect(response.headers.get('Cache-Control')).toContain('immutable');
      expect(response.body).toBe(mockBody);
    });

    it('should forward Range header', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 206,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Range': 'bytes 0-999/1000000',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip',
        {
          headers: { Range: 'bytes=0-999' },
        }
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(206);
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            Range: 'bytes=0-999',
          }),
        })
      );
    });

    it('should return 404 for non-existent files', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        headers: new Headers(),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/missing.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/missing.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(404);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'file_not_found',
        status: 404,
      });
    });
  });

  describe('File size limits', () => {
    it('should reject files exceeding size limit', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': (300 * 1024 * 1024).toString(), // 300MB
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/huge.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/huge.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(413);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'file_too_large',
        status: 413,
      });
    });

    it('should allow files within size limit', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': (100 * 1024 * 1024).toString(), // 100MB
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/normal.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/normal.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
    });
  });

  describe('Redirect handling', () => {
    it('should reject redirects to non-allowlisted hosts', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 302,
        headers: new Headers({
          Location: 'https://evil.com/redirect.zip',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(403);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'redirect_not_allowed',
        status: 403,
      });
    });

    it('should follow redirects to allowlisted hosts', async () => {
      globalThis.fetch = vi
        .fn()
        .mockResolvedValueOnce({
          ok: false,
          status: 302,
          headers: new Headers({
            Location: 'https://gcdn.thunderstore.io/file/test.zip',
          }),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          body: new ReadableStream(),
          headers: new Headers({
            'Content-Type': 'application/zip',
            'Content-Length': '1000',
          }),
        });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalledTimes(2);
    });

    it('should enforce maximum redirect depth', async () => {
      // Mock 6 redirects (exceeds MAX_REDIRECT_DEPTH of 5)
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 302,
        headers: new Headers({
          Location: 'https://cdn.thunderstore.io/redirect',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'too_many_redirects',
        status: 502,
      });
    });

    it('should reject redirects without Location header', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 302,
        headers: new Headers({}),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(502);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'invalid_redirect',
        status: 502,
      });
    });

    it('should handle multi-hop redirect chains', async () => {
      globalThis.fetch = vi
        .fn()
        .mockResolvedValueOnce({
          ok: false,
          status: 301,
          headers: new Headers({
            Location: 'https://gcdn.thunderstore.io/redirect1',
          }),
        })
        .mockResolvedValueOnce({
          ok: false,
          status: 302,
          headers: new Headers({
            Location: 'https://cdn.thunderstore.io/redirect2',
          }),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          body: new ReadableStream(),
          headers: new Headers({
            'Content-Type': 'application/zip',
          }),
        });

      const url = new URL('http://localhost:8787/proxy?url=https://thunderstore.io/file/test.zip');
      const request = new Request(
        'http://localhost:8787/proxy?url=https://thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
      expect(globalThis.fetch).toHaveBeenCalledTimes(3);
    });
  });

  describe('Response headers', () => {
    it('should include CORS headers', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('X-Content-Type-Options')).toBe('nosniff');
    });

    it('should expose headers for client access via CORS', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Range': 'bytes 0-999/1000000',
          'Accept-Ranges': 'bytes',
          ETag: '"abc123"',
          'Last-Modified': 'Wed, 12 Jan 2026 00:00:00 GMT',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      const exposeHeaders = response.headers.get('Access-Control-Expose-Headers');
      expect(exposeHeaders).toContain('Content-Range');
      expect(exposeHeaders).toContain('Accept-Ranges');
      expect(exposeHeaders).toContain('ETag');
      expect(exposeHeaders).toContain('Last-Modified');
      expect(exposeHeaders).toContain('Content-Disposition');
    });

    it('should include immutable cache header', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.headers.get('Cache-Control')).toContain('immutable');
      expect(response.headers.get('Cache-Control')).toContain('max-age=31536000');
    });

    it('should forward relevant headers from CDN', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': '1000',
          ETag: '"abc123"',
          'Last-Modified': 'Wed, 12 Jan 2026 00:00:00 GMT',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.headers.get('Content-Type')).toBe('application/zip');
      expect(response.headers.get('Content-Length')).toBe('1000');
      expect(response.headers.get('ETag')).toBe('"abc123"');
      expect(response.headers.get('Last-Modified')).toBe('Wed, 12 Jan 2026 00:00:00 GMT');
    });
  });

  describe('HEAD requests', () => {
    it('should support HEAD requests for size checks', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: null,
        headers: new Headers({
          'Content-Type': 'application/zip',
          'Content-Length': '5000000',
          'Accept-Ranges': 'bytes',
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip',
        { method: 'HEAD' }
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Length')).toBe('5000000');
      expect(response.body).toBeNull();
    });
  });

  describe('Missing Content-Length handling', () => {
    it('should handle responses without Content-Length', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        body: new ReadableStream(),
        headers: new Headers({
          'Content-Type': 'application/zip',
          // No Content-Length header
        }),
      });

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(200);
      // Should proceed without size check when Content-Length is missing
    });
  });

  describe('Timeout handling', () => {
    it('should timeout on slow requests', async () => {
      globalThis.fetch = vi.fn().mockImplementation(
        () =>
          new Promise((_, reject) => {
            setTimeout(() => {
              const error = new Error('Aborted');
              error.name = 'AbortError';
              reject(error);
            }, 100);
          })
      );

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(504);

      const data = await response.json();
      expect(data).toMatchObject({
        error: 'timeout',
        status: 504,
      });
    });

    it('should handle aborted requests gracefully', async () => {
      const abortError = new Error('The operation was aborted');
      abortError.name = 'AbortError';
      globalThis.fetch = vi.fn().mockRejectedValue(abortError);

      const url = new URL(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const request = new Request(
        'http://localhost:8787/proxy?url=https://cdn.thunderstore.io/file/test.zip'
      );
      const response = await handleProxy(url, request);

      expect(response.status).toBe(504);

      const data = (await response.json()) as { error: string };
      expect(data.error).toBe('timeout');
    });
  });
});
