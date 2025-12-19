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
      // Forward only safe headers; never forward Host/Origin/Referer to Thunderstore
      const forwardHeaders = new Headers();
      forwardHeaders.set('User-Agent', 'thunderstore-proxy/1.0 (Cloudflare Worker)');
      forwardHeaders.set('Accept', request.headers.get('Accept') ?? '*/*');
      const contentType = request.headers.get('Content-Type');
      if (contentType) forwardHeaders.set('Content-Type', contentType);
      const auth = request.headers.get('Authorization');
      if (auth) forwardHeaders.set('Authorization', auth);

      const response = await fetch(targetUrl, {
        method: request.method,
        headers: forwardHeaders,
        redirect: 'follow',
        body: request.method !== 'GET' && request.method !== 'HEAD' ? await request.text() : undefined,
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
