/**
 * Cloudflare Worker entry point
 * Proxies Thunderstore API with CORS support
 */
import { corsHeaders, handlePreflight, jsonError, validateUpstreamUrl } from './src/utils.js';

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    
    // Handle OPTIONS preflight for all endpoints
    if (request.method === 'OPTIONS') {
      return handlePreflight(env);
    }
    
    // Route: GET/HEAD /api/packages
    if (url.pathname === '/api/packages' && (request.method === 'GET' || request.method === 'HEAD')) {
      return handlePackagesRequest(env, request.method);
    }
    
    // 404 for unknown routes
    return jsonError('Not Found', 404, env);
  }
};

/**
 * Handles GET/HEAD /api/packages endpoint
 * Proxies Thunderstore package list with CORS support
 * @param {Object} env - Worker environment bindings
 * @param {string} method - HTTP method (GET or HEAD)
 * @returns {Response} JSON response with package list or error
 */
async function handlePackagesRequest(env, method = 'GET') {
  const upstreamUrl = 'https://thunderstore.io/c/repo/api/v1/package/';
  
  // Validate URL (defense-in-depth, should never fail)
  const validation = validateUpstreamUrl(upstreamUrl);
  if (!validation.valid) {
    return jsonError(validation.error, 400, env);
  }
  
  let timeoutId;
  try {
    // Fetch from upstream with timeout
    const controller = new AbortController();
    timeoutId = setTimeout(() => controller.abort(), 10000);
    
    const response = await fetch(upstreamUrl, {
      signal: controller.signal,
      headers: {
        'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)',
        'Accept': 'application/json'
      }
    });
    
    // Handle upstream errors
    if (!response.ok) {
      return jsonError('Upstream service unavailable', 502, env);
    }
    
    // Proxy response with CORS
    const body = await response.text();
    
    // For HEAD requests, return headers and status without body
    if (method === 'HEAD') {
      return new Response(null, {
        status: response.status,
        headers: {
          'Content-Type': 'application/json',
          ...corsHeaders(env)
        }
      });
    }
    
    return new Response(body, {
      status: response.status,
      headers: {
        'Content-Type': 'application/json',
        ...corsHeaders(env)
      }
    });
    
  } catch (error) {
    console.error('Packages endpoint error:', error.message);
    
    if (error.name === 'AbortError') {
      return jsonError('Upstream service timeout', 504, env);
    }
    
    return jsonError('Upstream service unavailable', 502, env);
  } finally {
    // Always clear the timeout to prevent memory leaks
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
    }
  }
}
