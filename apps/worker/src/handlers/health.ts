/**
 * Health check endpoint
 */

import { jsonResponse } from '../utils/responses';
import { getCorsHeaders } from '../utils/cors';

export function handleHealth(): Response {
  return jsonResponse({ status: 'ok' }, 200);
}

export function handleCors(): Response {
  return new Response(null, {
    status: 204,
    headers: getCorsHeaders(),
  });
}
