// Shared utilities and types

/**
 * Thunderstore API response types
 */

/**
 * A package version from Thunderstore API
 * @public - Used across web app components
 */
// ts-prune-ignore-next
export interface ThunderstorePackageVersion {
  /** Unique namespace of the mod author/team */
  namespace: string;
  /** Package name */
  name: string;
  /** Full package identifier (namespace/name) */
  full_name: string;
  /** Mod description */
  description: string;
  /** Version string (e.g., "1.0.0") */
  version_number: string;
  /** Array of dependency strings (e.g., ["Author-Mod-1.0.0"]) */
  dependencies: string[];
  /** Download URL for the package zip */
  download_url: string;
  /** Number of downloads */
  downloads: number;
  /** Release date in ISO format */
  date_created: string;
  /** Icon URL */
  icon_url?: string;
  /** Website URL */
  website_url?: string;
  /** Whether this package is deprecated */
  is_deprecated: boolean;
  /** Whether this package is pinned */
  is_pinned: boolean;
  /** Package categories */
  categories?: string[];
  /** Average rating (if available) */
  rating_score?: number;
}

/**
 * Thunderstore API paginated response
 * @public - Used by API client
 */
// ts-prune-ignore-next
export interface ThunderstoreApiResponse {
  /** Total number of results */
  count: number;
  /** URL for next page (null if last page) */
  next: string | null;
  /** URL for previous page (null if first page) */
  previous: string | null;
  /** Array of package results */
  results: ThunderstorePackageVersion[];
}

/**
 * Error response from API
 * @public - Used by API client error handling
 */
// ts-prune-ignore-next
export interface ApiError {
  error: string;
  message: string;
  status: number;
}

/**
 * Format download count (e.g., 1234 -> "1.2K")
 */
export function formatDownloads(count: number): string {
  if (count >= 1000000) {
    return `${(count / 1000000).toFixed(1)}M`;
  }
  if (count >= 1000) {
    return `${(count / 1000).toFixed(1)}K`;
  }
  return count.toString();
}

/**
 * Format rating score from 0-100 scale to 0-5 stars
 */
export function formatRating(score: number | undefined): string {
  if (score === null || score === undefined) {
    return 'N/A';
  }
  // Convert 0-100 to 0-5
  const stars = (score / 100) * 5;
  return `${stars.toFixed(1)} ★`;
}
