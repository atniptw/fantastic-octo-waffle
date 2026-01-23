/**
 * Unit tests for Core CORS & Error Utilities
 * Uses Vitest with standard Node.js/Web API environment
 */
import { describe, it, expect } from 'vitest';
import {
  corsHeaders,
  handlePreflight,
  jsonError,
  validateUpstreamUrl,
  isValidParam,
  parseFilename
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

describe('isValidParam()', () => {
  describe('Valid Parameters', () => {
    it('accepts alphanumeric characters', () => {
      expect(isValidParam('abc123')).toBe(true);
      expect(isValidParam('ABC123')).toBe(true);
      expect(isValidParam('Test123')).toBe(true);
    });

    it('accepts underscores', () => {
      expect(isValidParam('YMC_MHZ')).toBe(true);
      expect(isValidParam('My_Mod')).toBe(true);
      expect(isValidParam('test_123_mod')).toBe(true);
    });

    it('accepts hyphens', () => {
      expect(isValidParam('More-Head')).toBe(true);
      expect(isValidParam('my-mod')).toBe(true);
      expect(isValidParam('test-123-mod')).toBe(true);
    });

    it('accepts mixed alphanumeric, underscore, hyphen', () => {
      expect(isValidParam('My_Mod-v2')).toBe(true);
      expect(isValidParam('Test-Author_123')).toBe(true);
      expect(isValidParam('a1-b2_c3')).toBe(true);
    });

    it('accepts pure numeric strings', () => {
      expect(isValidParam('1234')).toBe(true);
      expect(isValidParam('100')).toBe(true);
    });

    it('accepts single character', () => {
      expect(isValidParam('a')).toBe(true);
      expect(isValidParam('1')).toBe(true);
      expect(isValidParam('_')).toBe(true);
      expect(isValidParam('-')).toBe(true);
      expect(isValidParam('.')).toBe(true);
    });
    
    it('accepts version numbers with dots', () => {
      expect(isValidParam('1.0.0')).toBe(true);
      expect(isValidParam('1.2.3')).toBe(true);
      expect(isValidParam('2.5.10')).toBe(true);
    });

    it('accepts 256 character strings (max length)', () => {
      const param = 'a'.repeat(256);
      expect(isValidParam(param)).toBe(true);
    });
  });

  describe('Invalid Parameters', () => {
    it('rejects path traversal attempts (normalized by URL parser)', () => {
      // URL parser normalizes .. so these won't match the route pattern
      // They would become /api/evil/name/1.0.0, /api/download/evil/1.0.0, etc.
      // But we still test that our validation works for the literal strings
      expect(isValidParam('..')).toBe(true); // Just dots are allowed (e.g., '...')
      expect(isValidParam('../')).toBe(false); // Contains slash
      expect(isValidParam('..\\evil')).toBe(false); // Contains backslash
      expect(isValidParam('../evil')).toBe(false); // Contains slash
    });

    it('rejects null byte injection', () => {
      expect(isValidParam('mod%00name')).toBe(false);
      expect(isValidParam('test\x00name')).toBe(false);
    });

    it('rejects spaces', () => {
      expect(isValidParam('mod name')).toBe(false);
      expect(isValidParam('test mod')).toBe(false);
      expect(isValidParam(' ')).toBe(false);
    });

    it('rejects special characters', () => {
      expect(isValidParam('mod@name')).toBe(false);
      expect(isValidParam('mod#name')).toBe(false);
      expect(isValidParam('mod$name')).toBe(false);
      expect(isValidParam('mod%name')).toBe(false);
      expect(isValidParam('mod&name')).toBe(false);
      expect(isValidParam('mod*name')).toBe(false);
      expect(isValidParam('mod(name)')).toBe(false);
      expect(isValidParam('mod[name]')).toBe(false);
      expect(isValidParam('mod{name}')).toBe(false);
      expect(isValidParam('mod|name')).toBe(false);
      expect(isValidParam('mod\\name')).toBe(false);
      expect(isValidParam('mod/name')).toBe(false);
      expect(isValidParam('mod:name')).toBe(false);
      expect(isValidParam('mod;name')).toBe(false);
      expect(isValidParam('mod<name>')).toBe(false);
      expect(isValidParam('mod>name')).toBe(false);
      expect(isValidParam('mod?name')).toBe(false);
      expect(isValidParam('mod=name')).toBe(false);
      expect(isValidParam('mod+name')).toBe(false);
      expect(isValidParam('mod!name')).toBe(false);
      expect(isValidParam('mod~name')).toBe(false);
      expect(isValidParam('mod`name')).toBe(false);
      expect(isValidParam('mod\'name')).toBe(false);
      expect(isValidParam('mod"name')).toBe(false);
      expect(isValidParam('mod,name')).toBe(false);
    });

    it('rejects empty string', () => {
      expect(isValidParam('')).toBe(false);
    });

    it('rejects null', () => {
      expect(isValidParam(null)).toBe(false);
    });

    it('rejects undefined', () => {
      expect(isValidParam(undefined)).toBe(false);
    });

    it('rejects non-string types (number)', () => {
      expect(isValidParam(123)).toBe(false);
    });

    it('rejects non-string types (object)', () => {
      expect(isValidParam({ name: 'test' })).toBe(false);
    });

    it('rejects non-string types (array)', () => {
      expect(isValidParam(['test'])).toBe(false);
    });

    it('rejects strings over 256 characters', () => {
      const param = 'a'.repeat(257);
      expect(isValidParam(param)).toBe(false);
    });
  });
});

describe('parseFilename()', () => {
  describe('Standard Format (filename="value")', () => {
    it('extracts filename with double quotes', () => {
      const result = parseFilename('attachment; filename="mod.zip"', 'default.zip');
      expect(result).toBe('mod.zip');
    });

    it('extracts filename without quotes', () => {
      const result = parseFilename('attachment; filename=mod.zip', 'default.zip');
      expect(result).toBe('mod.zip');
    });

    it('extracts complex filename with dashes and underscores', () => {
      const result = parseFilename('attachment; filename="YMC_MHZ-MoreHead-1.4.3.zip"', 'default.zip');
      expect(result).toBe('YMC_MHZ-MoreHead-1.4.3.zip');
    });

    it('handles filename with spaces', () => {
      const result = parseFilename('attachment; filename="my mod.zip"', 'default.zip');
      expect(result).toBe('my mod.zip');
    });

    it('handles filename with special characters', () => {
      const result = parseFilename('attachment; filename="mod (v1.0).zip"', 'default.zip');
      expect(result).toBe('mod (v1.0).zip');
    });

    it('extracts first filename when multiple present', () => {
      const result = parseFilename('attachment; filename="first.zip"; filename="second.zip"', 'default.zip');
      expect(result).toBe('first.zip');
    });
  });

  describe('RFC 5987 Format (filename*=charset\'\'value)', () => {
    it('extracts URL-encoded filename', () => {
      const result = parseFilename('attachment; filename*=UTF-8\'\'mod%20name.zip', 'default.zip');
      expect(result).toBe('mod name.zip');
    });

    it('extracts filename with no charset specified', () => {
      const result = parseFilename('attachment; filename*=\'\'mod.zip', 'default.zip');
      expect(result).toBe('mod.zip');
    });

    it('handles complex URL encoding', () => {
      const result = parseFilename('attachment; filename*=UTF-8\'\'%E6%B5%8B%E8%AF%95.zip', 'default.zip');
      expect(result).toBe('测试.zip');
    });

    it('handles filename* without charset prefix', () => {
      const result = parseFilename('attachment; filename*=test%20file.zip', 'default.zip');
      expect(result).toBe('test file.zip');
    });

    it('returns encoded version if decoding fails', () => {
      const result = parseFilename('attachment; filename*=UTF-8\'\'%invalid.zip', 'default.zip');
      expect(result).toBe('%invalid.zip');
    });
  });

  describe('Fallback Behavior', () => {
    it('returns fallback when disposition is null', () => {
      const result = parseFilename(null, 'default.zip');
      expect(result).toBe('default.zip');
    });

    it('returns fallback when disposition is undefined', () => {
      const result = parseFilename(undefined, 'default.zip');
      expect(result).toBe('default.zip');
    });

    it('returns fallback when disposition is empty string', () => {
      const result = parseFilename('', 'default.zip');
      expect(result).toBe('default.zip');
    });

    it('returns fallback when no filename found in disposition', () => {
      const result = parseFilename('attachment', 'default.zip');
      expect(result).toBe('default.zip');
    });

    it('returns fallback when disposition is malformed', () => {
      const result = parseFilename('completely-invalid-header', 'default.zip');
      expect(result).toBe('default.zip');
    });

    it('returns fallback when filename is empty', () => {
      const result = parseFilename('attachment; filename=""', 'default.zip');
      expect(result).toBe('default.zip');
    });
  });

  describe('Edge Cases', () => {
    it('handles disposition with multiple parameters', () => {
      const result = parseFilename('attachment; name=test; filename="mod.zip"; size=1024', 'default.zip');
      expect(result).toBe('mod.zip');
    });

    it('handles disposition with semicolons in quoted filename', () => {
      const result = parseFilename('attachment; filename="test;file.zip"', 'default.zip');
      expect(result).toBe('test;file.zip');
    });

    it('handles inline disposition type', () => {
      const result = parseFilename('inline; filename="preview.zip"', 'default.zip');
      expect(result).toBe('preview.zip');
    });

    it('handles case-insensitive filename parameter', () => {
      const result = parseFilename('attachment; FILENAME="mod.zip"', 'default.zip');
      // Note: Our regex is case-sensitive, so this should fallback
      expect(result).toBe('default.zip');
    });

    it('preserves whitespace in extracted filename', () => {
      const result = parseFilename('attachment; filename="  spaces  .zip"', 'default.zip');
      expect(result).toBe('  spaces  .zip');
    });

    it('handles single quotes around filename', () => {
      const result = parseFilename("attachment; filename='mod.zip'", 'default.zip');
      // Our regex expects double quotes or no quotes, so this extracts including quotes
      expect(result).toBe("'mod.zip'");
    });
  });
});
