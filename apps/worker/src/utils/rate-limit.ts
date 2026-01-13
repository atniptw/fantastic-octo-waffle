/**
 * Rate limiting implementation using in-memory token bucket
 *
 * Note: This is a simple in-memory implementation suitable for single-instance workers.
 * For production at scale, consider using Cloudflare Durable Objects or KV store.
 */

interface TokenBucket {
  tokens: number;
  lastRefill: number;
}

// In-memory store for rate limiting (per-instance)
const rateLimitStore = new Map<string, TokenBucket>();

// Rate limit configurations
export const RATE_LIMITS = {
  API: {
    maxTokens: 100, // 100 requests
    refillRate: 100 / 60, // per second (100 per minute)
  },
  PROXY: {
    maxTokens: 20, // 20 requests
    refillRate: 20 / 60, // per second (20 per minute)
  },
} as const;

export type RateLimitType = keyof typeof RATE_LIMITS;

/**
 * Check if request is within rate limit
 * Returns true if allowed, false if rate limit exceeded
 */
export function checkRateLimit(clientId: string, type: RateLimitType): boolean {
  // Lazy cleanup of old entries
  maybeCleanup();

  const config = RATE_LIMITS[type];
  const now = Date.now() / 1000; // Convert to seconds

  // Create unique key for client + type combination
  const bucketKey = `${clientId}:${type}`;

  // Get or create bucket for this client+type
  let bucket = rateLimitStore.get(bucketKey);
  if (!bucket) {
    bucket = {
      tokens: config.maxTokens,
      lastRefill: now,
    };
    rateLimitStore.set(bucketKey, bucket);
  }

  // Calculate tokens to add based on time elapsed
  const timePassed = now - bucket.lastRefill;
  const tokensToAdd = timePassed * config.refillRate;

  // Refill tokens (up to max)
  bucket.tokens = Math.min(config.maxTokens, bucket.tokens + tokensToAdd);
  bucket.lastRefill = now;

  // Check if we have tokens available
  if (bucket.tokens >= 1) {
    bucket.tokens -= 1;
    return true;
  }

  return false;
}

/**
 * Get client identifier from request
 * Uses CF-Connecting-IP header (Cloudflare provided) or falls back to x-forwarded-for
 */
export function getClientId(request: Request): string {
  // Cloudflare provides CF-Connecting-IP with the real client IP
  const cfIp = request.headers.get('CF-Connecting-IP');
  if (cfIp) {
    return cfIp;
  }

  // Fallback to x-forwarded-for (less reliable)
  const forwardedFor = request.headers.get('X-Forwarded-For');
  if (forwardedFor) {
    const ip = forwardedFor.split(',')[0];
    if (ip) {
      return ip.trim();
    }
  }

  // Last resort: use a placeholder (won't work well for rate limiting)
  return 'unknown';
}

/**
 * Clean up old entries from rate limit store
 * Should be called periodically to prevent memory leaks
 * 
 * Note: This is called lazily during rate limit checks rather than on a timer
 * because Cloudflare Workers don't support setInterval at global scope
 */
export function cleanupRateLimitStore(): void {
  const now = Date.now() / 1000;
  const maxAge = 300; // 5 minutes

  for (const [clientId, bucket] of rateLimitStore.entries()) {
    if (now - bucket.lastRefill > maxAge) {
      rateLimitStore.delete(clientId);
    }
  }
}

// Track last cleanup time
let lastCleanup = Date.now() / 1000;
const cleanupInterval = 60; // Clean up every minute

/**
 * Perform lazy cleanup if enough time has passed
 */
function maybeCleanup(): void {
  const now = Date.now() / 1000;
  if (now - lastCleanup > cleanupInterval) {
    cleanupRateLimitStore();
    lastCleanup = now;
  }
}
