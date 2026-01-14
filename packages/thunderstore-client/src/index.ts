/**
 * Thunderstore API Client for new community listing API
 */

// ============================================================================
// Types - New Thunderstore API Response Shapes
// ============================================================================

/**
 * Community metadata from /api/cyberstorm/community/{community}/
 */
export interface CommunityMetadata {
  name: string;
  identifier: string;
  discord_url?: string;
  wiki_url?: string;
  require_package_listing_approval: boolean;
}

/**
 * Filter option for a category
 */
export interface FilterOption {
  slug: string;
  label: string;
  count?: number;
}

/**
 * Available filters from /community/{community}/filters/
 */
export interface CommunityFilters {
  package_categories: FilterOption[];
  sections: FilterOption[];
}

/**
 * Package listing item from /listing/{community}/
 */
export interface PackageListing {
  namespace: string;
  name: string;
  full_name: string;
  owner: string;
  package_url: string;
  donation_link?: string;
  date_created: string;
  date_updated: string;
  uuid4: string;
  rating_score: number;
  is_pinned: boolean;
  is_deprecated: boolean;
  has_nsfw_content: boolean;
  categories: string[];
  versions: PackageVersion[];
}

/**
 * Package version within a listing
 */
export interface PackageVersion {
  name: string;
  full_name: string;
  description: string;
  icon: string;
  version_number: string;
  dependencies: string[];
  download_url: string;
  downloads: number;
  date_created: string;
  website_url?: string;
  is_active: boolean;
  uuid4: string;
  file_size: number;
}

/**
 * Paginated listing response
 */
export interface ListingResponse {
  count: number;
  next: string | null;
  previous: string | null;
  results: PackageListing[];
}

/**
 * Package detail response (for versions endpoint)
 */
export interface PackageDetail {
  namespace: string;
  name: string;
  full_name: string;
  owner: string;
  package_url: string;
  date_created: string;
  date_updated: string;
  rating_score: number;
  is_pinned: boolean;
  is_deprecated: boolean;
  categories: string[];
  versions: PackageVersion[];
}

// ============================================================================
// Client Configuration
// ============================================================================

export interface ThunderstoreClientConfig {
  baseUrl?: string;
  userAgent?: string;
  timeout?: number;
}

const DEFAULT_CONFIG: Required<ThunderstoreClientConfig> = {
  baseUrl: 'https://thunderstore.io',
  userAgent: 'fantastic-octo-waffle-thunderstore-client/0.1.0',
  timeout: 30000,
};

// ============================================================================
// Client Functions
// ============================================================================

/**
 * Create a configured fetch function with timeout and headers
 */
function createFetch(config: Required<ThunderstoreClientConfig>) {
  return async (url: string, options?: RequestInit): Promise<Response> => {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), config.timeout);

    try {
      const response = await fetch(url, {
        ...options,
        headers: {
          'User-Agent': config.userAgent,
          ...options?.headers,
        },
        signal: controller.signal,
      });

      return response;
    } finally {
      clearTimeout(timeoutId);
    }
  };
}

/**
 * Get community metadata
 */
export async function getCommunityMetadata(
  community: string = 'repo',
  config: ThunderstoreClientConfig = {}
): Promise<CommunityMetadata> {
  const mergedConfig = { ...DEFAULT_CONFIG, ...config };
  const fetchWithConfig = createFetch(mergedConfig);
  
  const url = `${mergedConfig.baseUrl}/api/cyberstorm/community/${community}/`;
  const response = await fetchWithConfig(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch community metadata: ${response.status}`);
  }

  return response.json();
}

/**
 * Get available filters for a community
 */
export async function getCommunityFilters(
  community: string = 'repo',
  config: ThunderstoreClientConfig = {}
): Promise<CommunityFilters> {
  const mergedConfig = { ...DEFAULT_CONFIG, ...config };
  const fetchWithConfig = createFetch(mergedConfig);
  
  const url = `${mergedConfig.baseUrl}/community/${community}/filters/`;
  const response = await fetchWithConfig(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch community filters: ${response.status}`);
  }

  return response.json();
}

/**
 * Listing query parameters
 */
export interface ListingParams {
  /** Community identifier (default: 'repo') */
  community?: string;
  /** Page number (1-indexed) */
  page?: number;
  /** Search query */
  q?: string;
  /** Filter by categories (array of category slugs) */
  included_categories?: string[];
  /** Filter by sections (array of section slugs) */
  section?: string;
  /** Ordering field (e.g., '-downloads', '-date_created', '-rating_score') */
  ordering?: string;
  /** Items per page */
  page_size?: number;
}

/**
 * Get package listing with filters
 */
export async function getPackageListing(
  params: ListingParams = {},
  config: ThunderstoreClientConfig = {}
): Promise<ListingResponse> {
  const mergedConfig = { ...DEFAULT_CONFIG, ...config };
  const fetchWithConfig = createFetch(mergedConfig);
  
  const community = params.community || 'repo';
  const url = new URL(`${mergedConfig.baseUrl}/listing/${community}/`);

  // Add query parameters
  if (params.page) {
    url.searchParams.set('page', params.page.toString());
  }
  
  if (params.q) {
    url.searchParams.set('q', params.q);
  }
  
  if (params.included_categories && params.included_categories.length > 0) {
    url.searchParams.set('included_categories', params.included_categories.join(','));
  }
  
  if (params.section) {
    url.searchParams.set('section', params.section);
  }
  
  if (params.ordering) {
    url.searchParams.set('ordering', params.ordering);
  }
  
  if (params.page_size) {
    url.searchParams.set('page_size', params.page_size.toString());
  }

  const response = await fetchWithConfig(url.toString());

  if (!response.ok) {
    throw new Error(`Failed to fetch package listing: ${response.status}`);
  }

  return response.json();
}

/**
 * Get package detail and versions
 */
export async function getPackageDetail(
  namespace: string,
  name: string,
  community: string = 'repo',
  config: ThunderstoreClientConfig = {}
): Promise<PackageDetail> {
  const mergedConfig = { ...DEFAULT_CONFIG, ...config };
  const fetchWithConfig = createFetch(mergedConfig);
  
  const url = `${mergedConfig.baseUrl}/api/cyberstorm/community/${community}/package/${namespace}/${name}/`;
  const response = await fetchWithConfig(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch package detail: ${response.status}`);
  }

  return response.json();
}
