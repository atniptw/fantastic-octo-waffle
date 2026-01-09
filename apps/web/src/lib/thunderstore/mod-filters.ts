import { PackageExperimental, PackageListing, PackageIndexEntry } from './types';
import { ModPackage } from './normalize';

/**
 * Type guard to check if a package is cosmetic-related
 */
export function isCosmeticMod(
  pkg: PackageExperimental | PackageListing | PackageIndexEntry
): boolean {
  const name = pkg.name.toLowerCase();
  
  // categories can be a string or array; safely convert to string
  let catsStr = '';
  if ('categories' in pkg && pkg.categories) {
    catsStr = typeof pkg.categories === 'string' ? pkg.categories.toLowerCase() : '';
  } else if ('community_listings' in pkg && pkg.community_listings) {
    catsStr = pkg.community_listings
      .map(listing => listing.categories)
      .join(',')
      .toLowerCase();
  }
  
  const cosmeticKeywords = ['decoration', 'cosmetic', 'hat', 'skin'];
  
  const nameMatch = cosmeticKeywords.some(keyword => name.includes(keyword));
  const categoryMatch = cosmeticKeywords.some(keyword => catsStr.includes(keyword));
  
  return nameMatch || categoryMatch;
}

/**
 * Filter mods by search query (name, owner/namespace, description)
 */
export function searchMods(
  mods: ModPackage[],
  query: string
): ModPackage[] {
  if (!query.trim()) {
    return mods;
  }

  const lowerQuery = query.toLowerCase();
  
  return mods.filter((mod) => {
    const nameMatch = mod.name.toLowerCase().includes(lowerQuery);
    const ownerMatch = mod.owner.toLowerCase().includes(lowerQuery);
    const descMatch = mod.description.toLowerCase().includes(lowerQuery);
    
    return nameMatch || ownerMatch || descMatch;
  });
}
