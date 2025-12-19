/**
 * Runtime configuration for the app
 * Load from environment variables or use defaults
 */

export const config = {
  // Thunderstore API base URL
  // In development: use local proxy or cors-anywhere fallback
  // In production: use your Cloudflare Worker URL
  thunderstoreBaseUrl: import.meta.env.VITE_THUNDERSTORE_PROXY_URL,
  // Thunderstore community (e.g., 'repo' for R.E.P.O.)
  thunderstoreCommunity: import.meta.env.VITE_THUNDERSTORE_COMMUNITY,
};
