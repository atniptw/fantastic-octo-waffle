/**
 * Unit tests for Core CORS & Error Utilities
 * Uses Vitest with Cloudflare Workers pool (Miniflare runtime)
 */
import { describe, it, expect } from 'vitest';
import {
  corsHeaders,
  handlePreflight,
  jsonError,
  validateUpstreamUrl
} from '../src/utils.js';

describe('corsHeaders()', () => {
  it('returns * when env.SITE_ORIGIN is undefined', () => {
    const headers = corsHeaders({ OTHER_VAR: 'value' });
    expect(headers['Access-Control-Allow-Origin']).toBe('*');
  });

  it('returns * when env is empty object', () => {
    const headers = corsHeaders({});
    expect(headers['Access-Control-Allow-Origin']).toBe('*');
  });

  it('returns specific origin when env.SITE_ORIGIN is set', () => {
    const headers = corsHeaders({ SITE_ORIGIN: 'https://atniptw.github.io' });
    expect(headers['Access-Control-Allow-Origin']).toBe('https://atniptw.github.io');
  });

  it('includes all three required header keys', () => {
    const headers = corsHeaders({});
    expect(headers).toHaveProperty('Access-Control-Allow-Origin');
    expect(headers).toHaveProperty('Access-Control-Allow-Methods');
    expect(headers).toHaveProperty('Access-Control-Allow-Headers');
  });

  it('methods header includes GET, HEAD, OPTIONS', () => {
    const headers = corsHeaders({});
    expect(headers['Access-Control-Allow-Methods']).toBe('GET, HEAD, OPTIONS');
  });

  it('headers include Content-Type', () => {
    const headers = corsHeaders({});
    expect(headers['Access-Control-Allow-Headers']).toBe('Content-Type');
  });
});

describe('handlePreflight()', () => {
  it('returns Response object with status 204', () => {
    const response = handlePreflight({});
    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(204);
  });

  it('response body is null/empty', async () => {
    const response = handlePreflight({});
    const text = await response.text();
    expect(text).toBe('');
  });

  it('response includes CORS headers matching corsHeaders(env)', () => {
    const env = { SITE_ORIGIN: 'https://test.example.com' };
    const response = handlePreflight(env);
    const expectedHeaders = corsHeaders(env);
    
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe(expectedHeaders['Access-Control-Allow-Origin']);
    expect(response.headers.get('Access-Control-Allow-Methods')).toBe(expectedHeaders['Access-Control-Allow-Methods']);
    expect(response.headers.get('Access-Control-Allow-Headers')).toBe(expectedHeaders['Access-Control-Allow-Headers']);
  });

  it('works with dev env (no SITE_ORIGIN)', () => {
    const response = handlePreflight({});
    expect(response.status).toBe(204);
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
  });

  it('works with prod env (with SITE_ORIGIN)', () => {
    const response = handlePreflight({ SITE_ORIGIN: 'https://atniptw.github.io' });
    expect(response.status).toBe(204);
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('https://atniptw.github.io');
  });
});

describe('jsonError()', () => {
  it('returns Response with correct HTTP status code', () => {
    const response = jsonError('Not Found', 404);
    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(404);
  });

  it('body is valid JSON matching { error: "message" } format', async () => {
    const response = jsonError('Test error', 400);
    const body = await response.json();
    expect(body).toEqual({ error: 'Test error' });
  });

  it('body does not include status field', async () => {
    const response = jsonError('Test error', 400);
    const body = await response.json();
    expect(body).not.toHaveProperty('status');
  });

  it('Content-Type header is application/json', () => {
    const response = jsonError('Error', 500);
    expect(response.headers.get('Content-Type')).toBe('application/json');
  });

  it('includes CORS headers', () => {
    const response = jsonError('Error', 500);
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
    expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
    expect(response.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
  });

  it('handles different status codes (400)', async () => {
    const response = jsonError('Bad Request', 400);
    expect(response.status).toBe(400);
    const body = await response.json();
    expect(body.error).toBe('Bad Request');
  });

  it('handles different status codes (404)', async () => {
    const response = jsonError('Not Found', 404);
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body.error).toBe('Not Found');
  });

  it('handles different status codes (500)', async () => {
    const response = jsonError('Internal Server Error', 500);
    expect(response.status).toBe(500);
    const body = await response.json();
    expect(body.error).toBe('Internal Server Error');
  });

  it('handles different status codes (502)', async () => {
    const response = jsonError('Bad Gateway', 502);
    expect(response.status).toBe(502);
    const body = await response.json();
    expect(body.error).toBe('Bad Gateway');
  });

  it('escapes special characters in error messages', async () => {
    const message = 'Error with "quotes" and \'apostrophes\' and <html>';
    const response = jsonError(message, 400);
    const body = await response.json();
    expect(body.error).toBe(message);
  });

  it('respects env.SITE_ORIGIN when provided', () => {
    const response = jsonError('Error', 500, { SITE_ORIGIN: 'https://test.com' });
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('https://test.com');
  });

  it('defaults to * when env is not provided', () => {
    const response = jsonError('Error', 500);
    expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
  });
});

describe('validateUpstreamUrl()', () => {
  it('accepts https://thunderstore.io/* URLs', () => {
    const result = validateUpstreamUrl('https://thunderstore.io/api/v1/package/');
    expect(result.valid).toBe(true);
    expect(result.error).toBeUndefined();
  });

  it('accepts https://gcdn.thunderstore.io/* URLs', () => {
    const result = validateUpstreamUrl('https://gcdn.thunderstore.io/icons/icon.png');
    expect(result.valid).toBe(true);
    expect(result.error).toBeUndefined();
  });

  it('accepts URLs with query parameters', () => {
    const result = validateUpstreamUrl('https://thunderstore.io/api/v1/package/?page=2&sort=name');
    expect(result.valid).toBe(true);
  });

  it('accepts URLs with fragments', () => {
    const result = validateUpstreamUrl('https://thunderstore.io/packages/namespace/name#readme');
    expect(result.valid).toBe(true);
  });

  it('accepts URLs with paths', () => {
    const result = validateUpstreamUrl('https://thunderstore.io/c/repo/api/v1/package/');
    expect(result.valid).toBe(true);
  });

  it('rejects non-allowlist hosts (example.com)', () => {
    const result = validateUpstreamUrl('https://example.com/api');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid upstream host: only thunderstore.io domains allowed');
  });

  it('rejects non-allowlist hosts (evil.com)', () => {
    const result = validateUpstreamUrl('https://evil.com/api');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid upstream host: only thunderstore.io domains allowed');
  });

  it('rejects subdomains not in allowlist (cdn.thunderstore.io)', () => {
    const result = validateUpstreamUrl('https://cdn.thunderstore.io/files/');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid upstream host: only thunderstore.io domains allowed');
  });

  it('rejects malformed URLs', () => {
    const result = validateUpstreamUrl('not-a-url');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });

  it('rejects HTTP URLs (non-HTTPS)', () => {
    const result = validateUpstreamUrl('http://thunderstore.io/api');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Only HTTPS protocol allowed');
  });

  it('rejects empty strings', () => {
    const result = validateUpstreamUrl('');
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });

  it('rejects null input', () => {
    const result = validateUpstreamUrl(null);
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });

  it('rejects undefined input', () => {
    const result = validateUpstreamUrl(undefined);
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });

  it('case-insensitive hostname matching (THUNDERSTORE.IO)', () => {
    const result = validateUpstreamUrl('https://THUNDERSTORE.IO/api');
    expect(result.valid).toBe(true);
  });

  it('case-insensitive hostname matching (Gcdn.ThunderStore.Io)', () => {
    const result = validateUpstreamUrl('https://Gcdn.ThunderStore.Io/icons/test.png');
    expect(result.valid).toBe(true);
  });

  it('error messages are descriptive for invalid host', () => {
    const result = validateUpstreamUrl('https://malicious.com/steal-data');
    expect(result.error).toContain('thunderstore.io');
  });

  it('error messages are descriptive for invalid protocol', () => {
    const result = validateUpstreamUrl('ftp://thunderstore.io/files');
    expect(result.error).toContain('HTTPS');
  });

  it('error messages are descriptive for malformed URLs', () => {
    const result = validateUpstreamUrl('://broken');
    expect(result.error).toContain('format');
  });

  it('rejects numeric input', () => {
    const result = validateUpstreamUrl(12345);
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });

  it('rejects object input', () => {
    const result = validateUpstreamUrl({ url: 'https://thunderstore.io' });
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Invalid URL format');
  });
});
