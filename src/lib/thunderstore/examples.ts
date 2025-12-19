/**
 * Example usage of the Thunderstore API client
 */

import { createThunderstoreClient } from './index';
import type { PackageIndexEntry } from './types';

/**
 * Example: Find all R.E.P.O. cosmetic mods
 */
export async function findREPOCosmetics() {
  const client = createThunderstoreClient();

  console.log('Fetching package index...');
  const allPackages = await client.getPackageIndex();

  console.log(`Found ${allPackages.length} total packages`);

  // Filter for R.E.P.O. related packages
  const repoMods = allPackages.filter(
    (pkg) =>
      pkg.dependencies.toLowerCase().includes('repo') ||
      pkg.namespace.toLowerCase() === 'repo' ||
      pkg.name.toLowerCase().includes('decoration') ||
      pkg.name.toLowerCase().includes('cosmetic')
  );

  console.log(`Found ${repoMods.length} potential R.E.P.O. mods`);

  // Get detailed information for each mod
  const details = await Promise.all(
    repoMods.slice(0, 10).map(async (pkg) => {
      try {
        const details = await client.getPackage(pkg.namespace, pkg.name);
        return {
          fullName: details.full_name,
          description: details.latest.description,
          downloads: details.total_downloads,
          url: client.getPackageUrl(pkg.namespace, pkg.name),
        };
      } catch (error) {
        console.error(`Failed to fetch ${pkg.namespace}/${pkg.name}:`, error);
        return null;
      }
    })
  );

  return details.filter((d) => d !== null);
}

/**
 * Example: Download a mod ZIP file
 */
export async function downloadMod(namespace: string, name: string, version?: string) {
  const client = createThunderstoreClient();

  // Get package info
  const pkg = await client.getPackage(namespace, name);
  const versionToDownload = version || pkg.latest.version_number;

  // Build download URL
  const downloadUrl = client.getPackageDownloadUrl(namespace, name, versionToDownload);

  console.log(`Downloading from: ${downloadUrl}`);

  // Download the ZIP
  const response = await fetch(downloadUrl);
  if (!response.ok) {
    throw new Error(`Download failed: ${response.status} ${response.statusText}`);
  }

  const blob = await response.blob();
  return blob;
}

/**
 * Example: Get mod statistics
 */
export async function getModStatistics(namespace: string, name: string) {
  const client = createThunderstoreClient();

  // Get package info
  const pkg = await client.getPackage(namespace, name);

  // Get metrics
  const metrics = await client.getPackageMetrics(namespace, name);

  return {
    name: pkg.full_name,
    description: pkg.latest.description,
    author: pkg.owner,
    latestVersion: pkg.latest.version_number,
    totalDownloads: metrics.downloads,
    ratingScore: metrics.rating_score,
    isDeprecated: pkg.is_deprecated,
    isPinned: pkg.is_pinned,
    dateCreated: pkg.date_created,
    dateUpdated: pkg.date_updated,
    communities: pkg.community_listings.length,
  };
}

/**
 * Example: Search for mods by keyword
 */
export async function searchModsByKeyword(keyword: string): Promise<PackageIndexEntry[]> {
  const client = createThunderstoreClient();

  const allPackages = await client.getPackageIndex();

  const searchTerm = keyword.toLowerCase();
  return allPackages.filter(
    (pkg) =>
      pkg.name.toLowerCase().includes(searchTerm) ||
      pkg.namespace.toLowerCase().includes(searchTerm)
  );
}

/**
 * Example: Build a local cache of all mods
 */
export async function buildModCache() {
  const client = createThunderstoreClient();

  console.log('Fetching all packages...');
  const packages = await client.getPackageIndex();

  // Group by namespace
  const byNamespace = new Map<string, PackageIndexEntry[]>();
  for (const pkg of packages) {
    const existing = byNamespace.get(pkg.namespace) || [];
    existing.push(pkg);
    byNamespace.set(pkg.namespace, existing);
  }

  console.log(`Found ${byNamespace.size} unique namespaces`);

  // Statistics
  const totalSize = packages.reduce((sum, pkg) => sum + pkg.file_size, 0);
  const totalSizeGB = (totalSize / 1024 / 1024 / 1024).toFixed(2);

  return {
    totalPackages: packages.length,
    totalNamespaces: byNamespace.size,
    totalSizeGB,
    byNamespace: Object.fromEntries(byNamespace),
    largestPackages: packages
      .sort((a, b) => b.file_size - a.file_size)
      .slice(0, 10)
      .map((pkg) => ({
        name: `${pkg.namespace}/${pkg.name}`,
        version: pkg.version_number,
        sizeMB: (pkg.file_size / 1024 / 1024).toFixed(2),
      })),
  };
}

/**
 * Example: Get mod documentation
 */
export async function getModDocumentation(namespace: string, name: string) {
  const client = createThunderstoreClient();

  const pkg = await client.getPackage(namespace, name);
  const latestVersion = pkg.latest.version_number;

  // Get README and changelog
  const [readme, changelog] = await Promise.all([
    client.getPackageVersionReadme(namespace, name, latestVersion),
    client.getPackageVersionChangelog(namespace, name, latestVersion),
  ]);

  // Try to get wiki if available
  let wiki = null;
  try {
    wiki = await client.getPackageWiki(namespace, name);
  } catch (error) {
    console.log('No wiki available for this package');
  }

  return {
    packageName: pkg.full_name,
    version: latestVersion,
    readme: readme.markdown,
    changelog: changelog.markdown,
    wiki: wiki,
  };
}
