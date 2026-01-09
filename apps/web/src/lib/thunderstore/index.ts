/**
 * Thunderstore API Library
 *
 * A TypeScript client library for the Thunderstore API
 * Fully browser-compatible with no Node.js dependencies
 *
 * API Documentation: https://thunderstore.io/api/docs/
 *
 * @example
 * ```typescript
 * import { createThunderstoreClient } from './lib/thunderstore';
 *
 * const client = createThunderstoreClient();
 *
 * // Get all packages
 * const packages = await client.getPackageIndex();
 *
 * // Get a specific package
 * const package = await client.getPackage('BepInEx', 'BepInExPack');
 *
 * // Get package metrics
 * const metrics = await client.getPackageMetrics('BepInEx', 'BepInExPack');
 * ```
 */

export * from './types';
export * from './client';
export { ThunderstoreClient, createThunderstoreClient } from './client';
