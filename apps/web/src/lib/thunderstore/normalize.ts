import { PackageExperimental, PackageListing, PackageIndexEntry } from './types';

/**
 * Normalized package interface for internal use
 * Eliminates need for type unions and defensive checks
 */
export interface ModPackage {
  id: string; // full_name or namespace/name
  name: string;
  namespace: string;
  owner: string;
  description: string;
  categories: string[];
  iconUrl?: string;
  websiteUrl?: string;
  downloadUrl?: string;
  version?: string;
  dateCreated?: string;
  dateUpdated?: string;
  rating?: number;
  downloadCount?: number;
}

/**
 * Normalize any Thunderstore package type to internal ModPackage
 */
export function normalizePackage(
  pkg: PackageExperimental | PackageListing | PackageIndexEntry
): ModPackage {
  // Determine ID
  const id = 'full_name' in pkg ? pkg.full_name : `${pkg.namespace}/${pkg.name}`;
  
  // Determine owner/namespace
  const owner = 'owner' in pkg ? pkg.owner : pkg.namespace;
  const namespace = 'namespace' in pkg ? pkg.namespace : pkg.owner || '';
  
  // Extract description
  let description = '';
  if ('latest' in pkg && pkg.latest?.description) {
    description = pkg.latest.description;
  } else if ('description' in pkg && typeof pkg.description === 'string') {
    description = pkg.description;
  }
  
  // Normalize categories to array
  let categories: string[] = [];
  if ('categories' in pkg && pkg.categories) {
    categories = typeof pkg.categories === 'string' ? [pkg.categories] : [];
  } else if ('community_listings' in pkg && pkg.community_listings) {
    categories = pkg.community_listings.map(listing => listing.categories);
  }
  
  // Extract version info
  const version = 'latest' in pkg ? pkg.latest?.version_number : undefined;
  
  // Extract URLs
  const iconUrl =
    'latest' in pkg
      ? pkg.latest?.icon
      : 'icon_url' in pkg
        ? pkg.icon_url
        : undefined;
  
  const websiteUrl =
    'latest' in pkg
      ? pkg.latest?.website_url
      : 'package_url' in pkg
        ? pkg.package_url
        : undefined;
  
  const downloadUrl = 'latest' in pkg ? pkg.latest?.download_url : undefined;
  
  // Extract dates
  const dateCreated = 'date_created' in pkg ? pkg.date_created : undefined;
  const dateUpdated = 'date_updated' in pkg ? pkg.date_updated : undefined;
  
  // Extract stats
  const rating = 'rating_score' in pkg ? parseFloat(pkg.rating_score) || undefined : undefined;
  const downloadCount =
    'latest' in pkg 
      ? pkg.latest?.downloads 
      : 'download_count' in pkg 
        ? typeof pkg.download_count === 'number' ? pkg.download_count : undefined
        : undefined;
  
  return {
    id,
    name: pkg.name,
    namespace,
    owner,
    description,
    categories,
    iconUrl: iconUrl as string | undefined,
    websiteUrl,
    downloadUrl,
    version,
    dateCreated,
    dateUpdated,
    rating,
    downloadCount,
  };
}
