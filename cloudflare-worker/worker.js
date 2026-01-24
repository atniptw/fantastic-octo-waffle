/**
 * Cloudflare Worker entry point
 * Proxies Thunderstore API with CORS support
 */
import { corsHeaders, handlePreflight, jsonError, validateUpstreamUrl, isValidParam, parseFilename } from './src/utils.js';

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
    
    // Route: HEAD /api/download/:namespace/:name/:version
    if (url.pathname.match(/^\/api\/download\/[^\/]+\/[^\/]+\/[^\/]+$/) && request.method === 'HEAD') {
      return handleDownloadMetadata(env, url);
    }
    
    // Route: GET /api/download/:namespace/:name/:version
    if (url.pathname.match(/^\/api\/download\/[^\/]+\/[^\/]+\/[^\/]+$/) && request.method === 'GET') {
      return handleDownloadRequest(env, url);
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

/**
 * Handles HEAD /api/download/:namespace/:name/:version endpoint
 * Probes ZIP file metadata (size and filename) without downloading body
 * @param {Object} env - Worker environment bindings
 * @param {URL} url - Parsed request URL
 * @returns {Response} Response with metadata headers or error
 */
async function handleDownloadMetadata(env, url) {
  const parts = url.pathname.split('/');
  const namespace = parts[3];
  const name = parts[4];
  const version = parts[5];
  
  // Validate parameters (alphanumeric, underscore, hyphen, and dot)
  if (!isValidParam(namespace) || !isValidParam(name) || !isValidParam(version)) {
    console.warn(`HEAD /api/download: Invalid parameters - ${namespace}/${name}/${version}`);
    return jsonError('Invalid parameters', 400, env);
  }
  
  const upstreamUrl = `https://thunderstore.io/package/download/${namespace}/${name}/${version}/`;
  
  let timeoutId;
  try {
    // 10-second timeout (HEAD should be fast)
    const controller = new AbortController();
    timeoutId = setTimeout(() => controller.abort(), 10000);
    
    const response = await fetch(upstreamUrl, { 
      method: 'HEAD',
      redirect: 'follow', // Follow redirects
      signal: controller.signal,
      headers: {
        'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)'
      }
    });
    
    // Handle upstream errors
    if (!response.ok) {
      const statusMsg = response.status === 404 ? 'Mod not found' : 'Download service unavailable';
      const returnCode = response.status === 404 ? 404 : 502;
      console.error(`HEAD /api/download upstream error: ${response.status}`);
      return jsonError(statusMsg, returnCode, env);
    }
    
    // Extract metadata from headers
    const sizeBytes = response.headers.get('Content-Length');
    const disposition = response.headers.get('Content-Disposition');
    const filename = parseFilename(disposition, `${name}.zip`);
    
    // Validate Content-Length (if missing, return 502)
    if (!sizeBytes) {
      console.warn(`HEAD /api/download: Missing Content-Length from upstream`);
      return jsonError('Cannot determine mod size from service', 502, env);
    }
    
    // Log success
    console.log(`HEAD /api/download/${namespace}/${name}/${version} → 200 ${sizeBytes} bytes`);
    
    // Return metadata in headers (no body for HEAD)
    return new Response(null, {
      status: 200,
      headers: {
        'X-Size-Bytes': sizeBytes,
        'X-Filename': filename,
        'Cache-Control': 'public, max-age=86400',
        'ETag': `"${namespace}-${name}-${version}"`,
        'Content-Type': 'application/json',
        ...corsHeaders(env)
      }
    });
    
  } catch (error) {
    console.error('HEAD /api/download error:', error.message);
    
    if (error.name === 'AbortError') {
      console.warn(`HEAD /api/download: Timeout after 10s for ${namespace}/${name}/${version}`);
      return jsonError('Download service timeout', 504, env);
    }
    
    return jsonError('Download service unavailable', 502, env);
  } finally {
    // Always clear the timeout to prevent memory leaks
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
    }
  }
}

/**
 * Handles GET /api/download/:namespace/:name/:version endpoint
 * Streams ZIP file download from Thunderstore without buffering entire file
 * @param {Object} env - Worker environment bindings
 * @param {URL} url - Parsed request URL
 * @returns {Response} Streaming response with ZIP body or error
 */
async function handleDownloadRequest(env, url) {
  const parts = url.pathname.split('/');
  const namespace = parts[3];
  const name = parts[4];
  const version = parts[5];
  
  // Validate parameters (same as HEAD endpoint)
  if (!isValidParam(namespace) || !isValidParam(name) || !isValidParam(version)) {
    console.warn(`GET /api/download: Invalid parameters - ${namespace}/${name}/${version}`);
    return jsonError('Invalid parameters', 400, env);
  }
  
  const upstreamUrl = `https://thunderstore.io/package/download/${namespace}/${name}/${version}/`;
  
  let timeoutId;
  try {
    // 30-second timeout for large file downloads
    const controller = new AbortController();
    timeoutId = setTimeout(() => controller.abort(), 30000);
    
    const response = await fetch(upstreamUrl, { 
      signal: controller.signal,
      headers: {
        'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)'
      }
    });
    
    // Handle upstream errors
    if (!response.ok) {
      const statusMsg = response.status === 404 ? 'Mod not found' : 'Download service unavailable';
      const returnCode = response.status === 404 ? 404 : 502;
      console.error(`GET /api/download upstream error: ${response.status}`);
      return jsonError(statusMsg, returnCode, env);
    }
    
    // Log success
    console.log(`GET /api/download/${namespace}/${name}/${version} → 200 streaming`);
    
    // Stream response directly (no buffering)
    return new Response(response.body, {
      status: 200,
      headers: {
        'Content-Type': 'application/zip',
        'Content-Disposition': response.headers.get('Content-Disposition') || `attachment; filename="${name}.zip"`,
        'Content-Length': response.headers.get('Content-Length') || '',
        ...corsHeaders(env)
      }
    });
    
  } catch (error) {
    console.error('GET /api/download error:', error.message);
    
    if (error.name === 'AbortError') {
      console.warn(`GET /api/download: Timeout after 30s for ${namespace}/${name}/${version}`);
      return jsonError('Download service timeout', 504, env);
    }
    
    return jsonError('Download service unavailable', 502, env);
  } finally {
    // Always clear the timeout to prevent memory leaks
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
    }
  }
}
