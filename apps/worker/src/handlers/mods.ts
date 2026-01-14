/**
 * Mod list and version endpoints using new Thunderstore community API
 */

import {
  getPackageListing,
  getPackageDetail,
  type ListingParams,
} from '@fantastic-octo-waffle/thunderstore-client';
import { THUNDERSTORE_API_BASE, USER_AGENT, CACHE_DURATIONS } from '../constants';
import { jsonError, jsonResponse } from '../utils/responses';
import { checkRateLimit, getClientId } from '../utils/rate-limit';

/**
 * List mods from Thunderstore using new community listing API
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

  // Build params for new API
  const params: ListingParams = {
    community,
    page: parseInt(page, 10),
  };

  if (query) {
    params.q = query;
  }

  // Map our sort params to Thunderstore ordering
  if (sort === 'downloads') {
    params.ordering = '-downloads';
  } else if (sort === 'newest') {
    params.ordering = '-date_created';
  } else if (sort === 'rating') {
    params.ordering = '-rating_score';
  }

  try {
    const data = await getPackageListing(params, {
      baseUrl: THUNDERSTORE_API_BASE,
      userAgent: USER_AGENT,
    });

    return jsonResponse(data, 200, {
      'Cache-Control': CACHE_DURATIONS.API,
    });
  } catch (error) {
    if (error instanceof Error) {
      // Check if it's a 404 error
      if (error.message.includes('404')) {
        return jsonError('invalid_community', `Community '${community}' not found`, 404);
      }
    }
    return jsonError('upstream_error', 'Failed to process Thunderstore API response', 502);
  }
}

/**
 * Get mod version details using new package detail API
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

  try {
    const data = await getPackageDetail(namespace, name, community, {
      baseUrl: THUNDERSTORE_API_BASE,
      userAgent: USER_AGENT,
    });

    return jsonResponse(data, 200, {
      'Cache-Control': CACHE_DURATIONS.API,
    });
  } catch (error) {
    if (error instanceof Error) {
      // Check if it's a 404 error
      if (error.message.includes('404')) {
        return jsonError('mod_not_found', `Mod ${namespace}/${name} not found`, 404);
      }
    }
    return jsonError(
      'upstream_error',
      'Failed to process mod versions response from Thunderstore',
      502
    );
  }
}
