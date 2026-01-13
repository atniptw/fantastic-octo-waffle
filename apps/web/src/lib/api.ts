/**
 * API client for Thunderstore mods
 */

import type { ThunderstoreApiResponse, ApiError } from '@fantastic-octo-waffle/utils';

const API_BASE = import.meta.env.DEV ? 'http://localhost:8787' : '';

/**
 * Fetch mods from the Worker proxy
 */
export async function fetchMods(
  page: number = 1,
  query: string = '',
  sort: 'downloads' | 'newest' | 'rating' = 'downloads'
): Promise<ThunderstoreApiResponse> {
  const url = new URL(`${API_BASE}/api/mods`);
  url.searchParams.set('community', 'repo');
  url.searchParams.set('page', page.toString());

  if (query) {
    url.searchParams.set('query', query);
  }

  url.searchParams.set('sort', sort);

  const response = await fetch(url.toString(), {
    headers: {
      Accept: 'application/json',
    },
  });

  if (!response.ok) {
    let errorData: ApiError;
    try {
      errorData = await response.json();
    } catch {
      errorData = {
        error: 'network_error',
        message: `Request failed with status ${response.status}`,
        status: response.status,
      };
    }
    throw new Error(errorData.message || 'Failed to fetch mods');
  }

  return response.json();
}
