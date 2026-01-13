/**
 * Mod list and version endpoints
 */

import {
  THUNDERSTORE_API_BASE,
  USER_AGENT,
  CACHE_DURATIONS,
  FETCH_TIMEOUT_MS,
} from '../constants';
import { jsonError, jsonResponse } from '../utils/responses';
import { checkRateLimit, getClientId } from '../utils/rate-limit';

/**
 * List mods from Thunderstore
 * GET /api/mods?community=repo&query=...&page=1&sort=downloads
 */
export async function handleModsList(url: URL, request: Request): Promise<Response> {
  // Check rate limit
  const clientId = getClientId(request);
  if (!checkRateLimit(clientId, 'API')) {
    return jsonError('rate_limit_exceeded', 'Too many requests. Please try again later.', 429, {
      'Retry-After': '60',
    });
  }

  const community = url.searchParams.get('community') || 'repo';
  const query = url.searchParams.get('query') || '';
  const page = url.searchParams.get('page') || '1';
  const sort = url.searchParams.get('sort') || '';

  // Build Thunderstore API URL
  const thunderstoreUrl = new URL(`${THUNDERSTORE_API_BASE}/frontend/c/${community}/`);

  if (query) {
    thunderstoreUrl.searchParams.set('q', query);
  }

  if (page) {
    thunderstoreUrl.searchParams.set('page', page);
  }

  // Map our sort params to Thunderstore ordering
  if (sort === 'downloads') {
    thunderstoreUrl.searchParams.set('ordering', '-downloads');
  } else if (sort === 'newest') {
    thunderstoreUrl.searchParams.set('ordering', '-date_created');
  } else if (sort === 'rating') {
    thunderstoreUrl.searchParams.set('ordering', '-rating');
  }

  try {
    // Set up timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch(thunderstoreUrl.toString(), {
        headers: {
          'User-Agent': USER_AGENT,
        },
        signal: controller.signal,
      });

      if (!response.ok) {
        if (response.status === 404) {
          return jsonError('invalid_community', `Community '${community}' not found`, 404);
        }
        throw new Error(`Thunderstore API error: ${response.status}`);
      }

      const data = await response.json();

      return jsonResponse(data, 200, {
        'Cache-Control': CACHE_DURATIONS.API,
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
    return jsonError('upstream_error', 'Failed to process Thunderstore API response', 502);
  }
}

/**
 * Get mod version details
 * GET /api/mod/:namespace/:name/versions
 */
export async function handleModVersions(url: URL, request: Request): Promise<Response> {
  // Check rate limit
  const clientId = getClientId(request);
  if (!checkRateLimit(clientId, 'API')) {
    return jsonError('rate_limit_exceeded', 'Too many requests. Please try again later.', 429, {
      'Retry-After': '60',
    });
  }

  const pathParts = url.pathname.split('/').filter(Boolean);

  // Expected: ['api', 'mod', namespace, name, 'versions']
  if (pathParts.length !== 5 || pathParts[4] !== 'versions') {
    return jsonError('invalid_path', 'Expected /api/mod/:namespace/:name/versions', 400);
  }

  const namespace = pathParts[2];
  const name = pathParts[3];
  const community = url.searchParams.get('community') || 'repo';

  // Build Thunderstore API URL
  const thunderstoreUrl = `${THUNDERSTORE_API_BASE}/frontend/c/${community}/p/${namespace}/${name}/`;

  try {
    // Set up timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch(thunderstoreUrl, {
        headers: {
          'User-Agent': USER_AGENT,
        },
        signal: controller.signal,
      });

      if (!response.ok) {
        if (response.status === 404) {
          return jsonError('mod_not_found', `Mod ${namespace}/${name} not found`, 404);
        }
        throw new Error(`Thunderstore API error: ${response.status}`);
      }

      const data = await response.json();

      return jsonResponse(data, 200, {
        'Cache-Control': CACHE_DURATIONS.API,
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
    return jsonError('upstream_error', 'Failed to process mod versions response from Thunderstore', 502);
  }
}
