// Shared utilities and types

/**
 * Thunderstore API response types (new community listing API)
 */

/**
 * Package version within a listing (from new API)
 * @public - Used across web app components
 */
export interface ThunderstorePackageVersion {
  /** Version name (e.g., "Author-ModName") */
  name: string;
  /** Full version name with version number (e.g., "Author-ModName-1.0.0") */
  full_name: string;
  /** Mod description */
  description: string;
  /** Icon URL */
  icon: string;
  /** Version string (e.g., "1.0.0") */
  version_number: string;
  /** Array of dependency strings (e.g., ["Author-Mod-1.0.0"]) */
  dependencies: string[];
  /** Download URL for the package zip */
  download_url: string;
  /** Number of downloads for this specific version */
  downloads: number;
  /** Release date in ISO format */
  date_created: string;
  /** Website URL */
  website_url?: string;
  /** Whether this version is active */
  is_active: boolean;
  /** UUID of the version */
  uuid4: string;
  /** File size in bytes */
  file_size: number;
}

/**
 * Package listing item (from new API)
 * @public - Used by web app for mod list display
 */
export interface ThunderstorePackageListing {
  /** Unique namespace of the mod author/team */
  namespace: string;
  /** Package name */
  name: string;
  /** Full package identifier (namespace-name) */
  full_name: string;
  /** Package owner */
  owner: string;
  /** Package URL on Thunderstore */
  package_url: string;
  /** Donation link */
  donation_link?: string;
  /** Creation date in ISO format */
  date_created: string;
  /** Last update date in ISO format */
  date_updated: string;
  /** UUID of the package */
  uuid4: string;
  /** Average rating score (0-100) */
  rating_score: number;
  /** Whether this package is pinned */
  is_pinned: boolean;
  /** Whether this package is deprecated */
  is_deprecated: boolean;
  /** Whether package has NSFW content */
  has_nsfw_content: boolean;
  /** Package categories */
  categories: string[];
  /** Array of package versions */
  versions: ThunderstorePackageVersion[];
}

/**
 * Thunderstore API paginated response
 * @public - Used by API client
 */
export interface ThunderstoreApiResponse {
  /** Total number of results */
  count: number;
  /** URL for next page (null if last page) */
  next: string | null;
  /** URL for previous page (null if first page) */
  previous: string | null;
  /** Array of package results */
  results: ThunderstorePackageListing[];
}

/**
 * Error response from API
 * @public - Used by API client error handling
 */
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

/**
 * Get the latest version from a package listing
 */
export function getLatestVersion(
  pkg: ThunderstorePackageListing
): ThunderstorePackageVersion | undefined {
  // Versions are typically ordered with newest first
  return pkg.versions[0];
}

/**
 * Get total downloads for a package (sum of all version downloads)
 */
export function getTotalDownloads(pkg: ThunderstorePackageListing): number {
  return pkg.versions.reduce((sum, version) => sum + version.downloads, 0);
}
