/**
 * Core CORS & Error Utilities for Cloudflare Worker
 * Pure functions for middleware - no side effects, fully testable
 */

/**
 * Returns CORS headers for Worker responses
 * @param {Object} env - Worker environment bindings
 * @param {string} [env.SITE_ORIGIN] - Production origin (e.g., 'https://atniptw.github.io')
 * @returns {Object} Headers object for CORS
 */
export function corsHeaders(env) {
  const origin = env?.SITE_ORIGIN || '*';
  
  return {
    'Access-Control-Allow-Origin': origin,
    'Access-Control-Allow-Methods': 'GET, HEAD, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type'
  };
}

/**
 * Handles OPTIONS preflight requests
 * @param {Object} env - Worker environment bindings
 * @returns {Response} 204 Response with CORS headers
 */
export function handlePreflight(env) {
  return new Response(null, {
    status: 204,
    headers: corsHeaders(env)
  });
}

/**
 * Creates standardized JSON error responses
 * @param {string} message - Error message
 * @param {number} status - HTTP status code (400-599)
 * @param {Object} [env] - Optional environment for CORS headers
 * @returns {Response} JSON error response with CORS headers
 */
export function jsonError(message, status, env = {}) {
  const body = JSON.stringify({ error: message });
  const headers = {
    'Content-Type': 'application/json',
    ...corsHeaders(env)
  };
  
  return new Response(body, {
    status,
    headers
  });
}

/**
 * Validates that a URL belongs to the Thunderstore allowlist
 * @param {string} urlString - URL to validate
 * @returns {{ valid: boolean, error?: string }} Validation result
 */
export function validateUpstreamUrl(urlString) {
  // Allowlist of permitted hosts
  const allowedHosts = ['thunderstore.io', 'gcdn.thunderstore.io'];
  
  // Handle null, undefined, or empty string
  if (!urlString || typeof urlString !== 'string') {
    return {
      valid: false,
      error: 'Invalid URL format'
    };
  }
  
  // Try to parse the URL
  let url;
  try {
    url = new URL(urlString);
  } catch (e) {
    return {
      valid: false,
      error: 'Invalid URL format'
    };
  }
  
  // Check protocol
  if (url.protocol !== 'https:') {
    return {
      valid: false,
      error: 'Only HTTPS protocol allowed'
    };
  }
  
  // Check hostname against allowlist (case-insensitive)
  const hostname = url.hostname.toLowerCase();
  if (!allowedHosts.includes(hostname)) {
    return {
      valid: false,
      error: 'Invalid upstream host: only thunderstore.io domains allowed'
    };
  }
  
  return { valid: true };
}
