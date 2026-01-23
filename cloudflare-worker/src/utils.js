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

/**
 * Validates a Thunderstore parameter (namespace, name, or version)
 * 
 * Rules:
 * - Length: 1-256 characters (Thunderstore limit)
 * - Pattern: ^[a-zA-Z0-9_.-]+$ (alphanumeric, underscore, hyphen, dot)
 * - No special chars, no spaces, no null bytes, no path traversal
 * - Check decoded value (handle URL encoding edge cases)
 * 
 * @param {string} param - Parameter to validate
 * @returns {boolean} True if valid, false otherwise
 * 
 * Valid examples:
 * - "YMC_MHZ" (underscore)
 * - "More-Head" (hyphen)
 * - "My_Mod-v2" (mixed)
 * - "1.2.3" (version with dots)
 * - "1234" (numeric)
 * 
 * Invalid examples:
 * - "../evil" (path traversal)
 * - "mod%00name" (null byte)
 * - "mod name" (space)
 * - "mod@name" (special char)
 * - "" (empty)
 */
export function isValidParam(param) {
  if (!param || typeof param !== 'string') return false;
  if (param.length < 1 || param.length > 256) return false;
  // Alphanumeric, underscore, hyphen, dot only
  return /^[a-zA-Z0-9_.-]+$/.test(param);
}

/**
 * Parses filename from Content-Disposition header
 * Handles multiple RFC formats and edge cases
 * 
 * Supported formats:
 * 1. Standard: attachment; filename="my-file.zip"
 * 2. RFC 5987: attachment; filename*=UTF-8''my-file.zip (URL-encoded)
 * 3. Fallback: Uses provided fallback if parsing fails
 * 
 * @param {string} disposition - Content-Disposition header value
 * @param {string} fallback - Fallback filename (e.g., "{name}.zip")
 * @returns {string} Extracted or fallback filename
 * 
 * Examples:
 * - parseFilename('attachment; filename="mod.zip"', "default.zip") → "mod.zip"
 * - parseFilename('attachment; filename*=UTF-8''%20name%20.zip', "default.zip") → " name .zip"
 * - parseFilename(null, "default.zip") → "default.zip"
 */
export function parseFilename(disposition, fallback) {
  if (!disposition) return fallback;
  
  try {
    // Try standard format: filename="value" or filename=value
    const standardMatch = disposition.match(/filename="?([^";]+)"?/);
    if (standardMatch && standardMatch[1]) {
      return standardMatch[1];
    }
    
    // Try RFC 5987 format: filename*=charset'lang'value or filename*=''value
    const rfc5987Match = disposition.match(/filename\*=(?:[a-zA-Z0-9_-]*'')?([^;]+)/);
    if (rfc5987Match && rfc5987Match[1]) {
      try {
        return decodeURIComponent(rfc5987Match[1]);
      } catch {
        // If decoding fails, return encoded version
        return rfc5987Match[1];
      }
    }
  } catch (e) {
    console.warn('Error parsing filename from Content-Disposition:', e.message);
  }
  
  return fallback;
}
