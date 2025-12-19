/**
 * Cloudflare Worker: Thunderstore API Proxy
 * 
 * Routes all Thunderstore API requests through this worker to:
 * - Bypass CORS restrictions
 * - Support experimental endpoints
 * - Cache responses for performance
 * 
 * Usage:
 * GET https://thunderstore-proxy.workers.dev/api/v1/package/
 * GET https://thunderstore-proxy.workers.dev/api/experimental/package/{namespace}/{name}/
 * GET https://thunderstore-proxy.workers.dev/package/download/{namespace}/{name}/
 */

const THUNDERSTORE_BASE = 'https://thunderstore.io';

export default {
  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    
    // Extract the path after the domain
    const path = url.pathname;
    
    // Construct the target URL
    const targetUrl = `${THUNDERSTORE_BASE}${path}${url.search}`;
    
    try {
      const response = await fetch(targetUrl, {
        method: request.method,
        headers: {
          // Remove host header to avoid conflicts
          'User-Agent': 'thunderstore-proxy/1.0 (Cloudflare Worker)',
          ...Object.fromEntries(request.headers),
        },
        body: request.method !== 'GET' ? await request.text() : undefined,
      });
      
      // Clone the response so we can modify headers
      const newResponse = new Response(response.body, response);
      
      // Add CORS headers
      newResponse.headers.set('Access-Control-Allow-Origin', '*');
      newResponse.headers.set('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
      newResponse.headers.set('Access-Control-Allow-Headers', 'Content-Type, Authorization');
      
      // Cache successful responses for 1 hour
      if (response.ok && request.method === 'GET') {
        newResponse.headers.set('Cache-Control', 'public, max-age=3600');
      }
      
      return newResponse;
    } catch (error) {
      return new Response(
        JSON.stringify({
          error: 'Failed to proxy request',
          message: error instanceof Error ? error.message : 'Unknown error',
        }),
        {
          status: 500,
          headers: {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
          },
        }
      );
    }
  },
};
