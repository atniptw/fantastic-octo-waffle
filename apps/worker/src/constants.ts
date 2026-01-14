/**
 * Cloudflare Worker constants
 */

// Thunderstore API base URL (new community API)
export const THUNDERSTORE_API_BASE = 'https://thunderstore.io';

// User agent for upstream requests
export const USER_AGENT = 'fantastic-octo-waffle-worker/0.1.0';

// Allowed hosts for /proxy endpoint (download URLs)
export const ALLOWED_DOWNLOAD_HOSTS = [
  'cdn.thunderstore.io',
  'thunderstore.io',
  'gcdn.thunderstore.io',
];

// Max file size for downloads (200MB in bytes)
export const MAX_FILE_SIZE_BYTES = 200 * 1024 * 1024;

// Max redirect depth for proxy (prevent loops)
export const MAX_REDIRECT_DEPTH = 5;

// Fetch timeout in milliseconds (30 seconds)
export const FETCH_TIMEOUT_MS = 30000;

// Cache durations
export const CACHE_DURATIONS = {
  API: 'public, max-age=300, stale-while-revalidate=600',
  DOWNLOAD: 'public, immutable, max-age=31536000',
} as const;

// HTTP headers to forward from CDN responses
export const FORWARDED_HEADERS = [
  'Content-Type',
  'Content-Length',
  'Content-Range',
  'Accept-Ranges',
  'Content-Disposition',
  'ETag',
  'Last-Modified',
] as const;
