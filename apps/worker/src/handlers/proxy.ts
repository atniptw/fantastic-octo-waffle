/**
 * Download proxy endpoint
 */

import {
  ALLOWED_DOWNLOAD_HOSTS,
  MAX_FILE_SIZE_BYTES,
  MAX_REDIRECT_DEPTH,
  FETCH_TIMEOUT_MS,
  USER_AGENT,
  CACHE_DURATIONS,
  FORWARDED_HEADERS,
} from '../constants';
import { jsonError } from '../utils/responses';
import { getCorsHeaders } from '../utils/cors';
import { checkRateLimit, getClientId } from '../utils/rate-limit';

/**
 * Proxy download requests with allowlist validation
 * GET /proxy?url=https://cdn.thunderstore.io/file/...
 */
export async function handleProxy(url: URL, request: Request): Promise<Response> {
  // Check rate limit (stricter for downloads)
  const clientId = getClientId(request);
  if (!checkRateLimit(clientId, 'PROXY')) {
    return jsonError(
      'rate_limit_exceeded',
      'Too many download requests. Please try again later.',
      429,
      {
        'Retry-After': '60',
      }
    );
  }

  const targetUrl = url.searchParams.get('url');

  if (!targetUrl) {
    return jsonError('missing_url', 'Missing required parameter: url', 400);
  }

  // Validate URL
  let parsedUrl: URL;
  try {
    parsedUrl = new URL(targetUrl);
  } catch {
    return jsonError('invalid_url', 'Invalid URL format', 400);
  }

  // Must be HTTPS
  if (parsedUrl.protocol !== 'https:') {
    return jsonError('invalid_protocol', 'Only HTTPS URLs are allowed', 400);
  }

  // Check allowlist
  const isAllowed = ALLOWED_DOWNLOAD_HOSTS.some(
    (host) => parsedUrl.hostname === host || parsedUrl.hostname.endsWith(`.${host}`)
  );

  if (!isAllowed) {
    return jsonError('host_not_allowed', `Host ${parsedUrl.hostname} is not on the allowlist`, 403);
  }

  try {
    // Set up timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    try {
      // Forward Range header if present
      const headers: HeadersInit = {
        'User-Agent': USER_AGENT,
      };

      const rangeHeader = request.headers.get('Range');
      if (rangeHeader) {
        headers['Range'] = rangeHeader;
      }

      // Follow redirects manually to validate each hop and enforce depth limit
      let currentUrl = parsedUrl.toString();
      let redirectCount = 0;
      let response: Response;

      while (true) {
        response = await fetch(currentUrl, {
          headers,
          redirect: 'manual',
          signal: controller.signal,
        });

        // Check for redirects
        if (response.status >= 300 && response.status < 400) {
          redirectCount++;
          if (redirectCount > MAX_REDIRECT_DEPTH) {
            return jsonError(
              'too_many_redirects',
              `Exceeded maximum redirect depth of ${MAX_REDIRECT_DEPTH}`,
              502
            );
          }

          const location = response.headers.get('Location');
          if (!location) {
            return jsonError('invalid_redirect', 'Redirect without Location header', 502);
          }

          const redirectUrl = new URL(location, currentUrl);

          // Validate redirect URL protocol
          if (redirectUrl.protocol !== 'https:') {
            return jsonError('invalid_protocol', 'Only HTTPS URLs are allowed', 400);
          }

          const redirectAllowed = ALLOWED_DOWNLOAD_HOSTS.some(
            (host) => redirectUrl.hostname === host || redirectUrl.hostname.endsWith(`.${host}`)
          );

          if (!redirectAllowed) {
            return jsonError('redirect_not_allowed', 'Redirect target is not on allowlist', 403);
          }

          currentUrl = redirectUrl.toString();
          continue;
        }

        // Not a redirect, proceed with response
        break;
      }

      if (!response.ok) {
        if (response.status === 404) {
          return jsonError('file_not_found', 'File not found', 404);
        }
        throw new Error(`CDN error: ${response.status}`);
      }

      // Check content length
      const contentLength = response.headers.get('Content-Length');
      if (contentLength && parseInt(contentLength, 10) > MAX_FILE_SIZE_BYTES) {
        return jsonError(
          'file_too_large',
          `File exceeds maximum size of ${MAX_FILE_SIZE_BYTES / 1024 / 1024}MB`,
          413
        );
      }

      // Build response headers
      const responseHeaders = new Headers();

      // Copy relevant headers
      for (const header of FORWARDED_HEADERS) {
        const value = response.headers.get(header);
        if (value) {
          responseHeaders.set(header, value);
        }
      }

      // Add CORS and security headers
      const corsHeaders = getCorsHeaders();
      for (const [key, value] of Object.entries(corsHeaders)) {
        responseHeaders.set(key, value);
      }

      // Cache for 1 year (immutable versioned files)
      responseHeaders.set('Cache-Control', CACHE_DURATIONS.DOWNLOAD);

      return new Response(response.body, {
        status: response.status,
        headers: responseHeaders,
      });
    } finally {
      clearTimeout(timeoutId);
    }
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      return jsonError(
        'timeout',
        `Request timed out after ${FETCH_TIMEOUT_MS / 1000} seconds`,
        504
      );
    }
    return jsonError('proxy_error', 'Failed to proxy download request', 502);
  }
}
