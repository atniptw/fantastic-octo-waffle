import { describe, it, expect } from 'vitest';
import { handleHealth, handleCors } from '../../handlers/health';

describe('Health handler', () => {
  describe('handleHealth', () => {
    it('should return 200 OK with status', () => {
      const response = handleHealth();

      expect(response.status).toBe(200);
      
      const text = response.clone().text();
      expect(text).toBeDefined();
    });

    it('should return JSON response', async () => {
      const response = handleHealth();
      const data = await response.json();
      
      expect(data).toEqual({ status: 'ok' });
    });

    it('should include CORS headers', () => {
      const response = handleHealth();

      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('X-Content-Type-Options')).toBe('nosniff');
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });
  });

  describe('handleCors', () => {
    it('should return 204 No Content', () => {
      const response = handleCors();

      expect(response.status).toBe(204);
    });

    it('should include CORS headers', () => {
      const response = handleCors();

      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toBe('GET, HEAD, OPTIONS');
      expect(response.headers.get('Access-Control-Allow-Headers')).toContain('Content-Type');
      expect(response.headers.get('Access-Control-Max-Age')).toBe('3600');
    });

    it('should include security headers', () => {
      const response = handleCors();

      expect(response.headers.get('X-Content-Type-Options')).toBe('nosniff');
      expect(response.headers.get('X-Frame-Options')).toBe('DENY');
      expect(response.headers.get('Referrer-Policy')).toBe('no-referrer');
    });
  });
});
