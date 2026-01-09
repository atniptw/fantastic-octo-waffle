/**
 * Utility to identify and filter R.E.P.O. cosmetic mods
 */

import type { PackageIndexEntry } from './types';

/**
 * Check if a package is likely a R.E.P.O. cosmetic mod based on:
 * - Dependency on R.E.P.O.
 * - Name contains cosmetic/decoration/hat/skin keywords
 * - Should have .hhh files in Decorations folder (can't verify in index)
 */
export function isREPOCosmeticMod(pkg: PackageIndexEntry): boolean {
  const lowerDeps = pkg.dependencies.toLowerCase();
  const lowerName = pkg.name.toLowerCase();
  const lowerNamespace = pkg.namespace.toLowerCase();

  // Must depend on R.E.P.O.
  const hasREPODep =
    lowerDeps.includes('repo') || lowerDeps.includes('r.e.p.o') || lowerDeps.includes('bepinex');

  if (!hasREPODep) {
    return false;
  }

  // Should be a cosmetic mod based on name
  const isCosmeticByName =
    lowerName.includes('cosmetic') ||
    lowerName.includes('decoration') ||
    lowerName.includes('hat') ||
    lowerName.includes('skin') ||
    lowerName.includes('outfit') ||
    lowerName.includes('accessory') ||
    lowerName.includes('armor') ||
    lowerName.includes('helmet') ||
    lowerName.includes('item') ||
    lowerNamespace.includes('cosmetic') ||
    lowerNamespace.includes('decoration');

  return isCosmeticByName;
}

/**
 * Filter a list of packages to only R.E.P.O. cosmetic mods
 */
export function filterREPOCosmetics(packages: PackageIndexEntry[]): PackageIndexEntry[] {
  return packages.filter(isREPOCosmeticMod);
}
