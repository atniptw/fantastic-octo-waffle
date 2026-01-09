/**
 * Runtime configuration for the app
 * Load from environment variables or detect environment automatically
 */

// Detect if we're in GitHub Codespaces
const isCodespaces = typeof window !== 'undefined' && window.location.hostname.includes('github.dev');

// Extract Codespace name from hostname (e.g., "symmetrical-umbrella-695v7954-5173.app.github.dev")
let codespaceName: string | undefined;
if (isCodespaces) {
  const match = window.location.hostname.match(/^([a-z0-9-]+)-\d+\.app\.github\.dev$/);
  if (match) {
    codespaceName = match[1];
  }
}

// Build proxy URL for Codespaces (port 8787 is the Cloudflare Worker)
const autoProxyUrl = codespaceName ? `https://${codespaceName}-8787.app.github.dev` : undefined;

export const config = {
  // Thunderstore API base URL for API calls
  thunderstoreBaseUrl: import.meta.env.VITE_THUNDERSTORE_BASE_URL || 'https://thunderstore.io',
  // Thunderstore proxy URL for ZIP downloads
  // Priority: env var > auto-detected Codespaces proxy > undefined
  thunderstoreProxyUrl: import.meta.env.VITE_THUNDERSTORE_PROXY_URL || autoProxyUrl,
  // Thunderstore community (e.g., 'repo' for R.E.P.O.)
  thunderstoreCommunity: import.meta.env.VITE_THUNDERSTORE_COMMUNITY || 'repo',
  // Debug logging flag (set VITE_DEBUG_LOG=true to enable verbose logs)
  debugLogging: import.meta.env.VITE_DEBUG_LOG === 'true' || import.meta.env.DEV,
};
