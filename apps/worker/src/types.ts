/**
 * Cloudflare Worker type definitions
 */

export interface WorkerEnv {
  // Future: Add KV namespace for rate limiting
  // RATE_LIMIT_KV?: KVNamespace;
}

export interface ErrorResponse {
  error: string;
  message: string;
  status: number;
}
