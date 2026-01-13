// Shared utilities and types

/**
 * Thunderstore API response types
 */

/**
 * A package version from Thunderstore API
 */
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
 */
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
 */
export interface ApiError {
  error: string;
  message: string;
  status: number;
}
