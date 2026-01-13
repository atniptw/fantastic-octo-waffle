/**
 * Cloudflare Worker Proxy for Thunderstore API
 *
 * Provides CORS-safe proxy for:
 * - Mod listings from Thunderstore
 * - Mod version metadata
 * - Zip file downloads with allowlist validation
 */

import type { WorkerEnv } from './types';
import { handleHealth, handleCors } from './handlers/health';
import { handleModsList, handleModVersions } from './handlers/mods';
import { handleProxy } from './handlers/proxy';
import { jsonError } from './utils/responses';

/**
 * Main request handler
 */
export default {
  async fetch(request: Request, _env: WorkerEnv): Promise<Response> {
    const url = new URL(request.url);

    // Handle CORS preflight
    if (request.method === 'OPTIONS') {
      return handleCors();
    }

    try {
      // Route requests
      if (url.pathname === '/health') {
        return handleHealth();
      }

      if (url.pathname === '/api/mods') {
        return handleModsList(url, request);
      }

      if (url.pathname.startsWith('/api/mod/')) {
        return handleModVersions(url, request);
      }

      if (url.pathname === '/proxy') {
        return handleProxy(url, request);
      }

      // 404 for unknown routes
      return jsonError('not_found', 'Endpoint not found', 404);
    } catch (error) {
      console.error('Worker error:', error);
      return jsonError(
        'internal_error',
        error instanceof Error ? error.message : 'Internal server error',
        500
      );
    }
  },
};
