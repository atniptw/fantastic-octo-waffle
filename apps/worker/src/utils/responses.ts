/**
 * Response helpers
 */

import { getCorsHeaders } from './cors';
import type { ErrorResponse } from '../types';

export function jsonResponse(
  data: unknown,
  status: number,
  additionalHeaders: Record<string, string> = {}
): Response {
  const headers = {
    'Content-Type': 'application/json',
    ...getCorsHeaders(),
    ...additionalHeaders,
  };

  return new Response(JSON.stringify(data), {
    status,
    headers,
  });
}

export function jsonError(error: string, message: string, status: number): Response {
  const errorResponse: ErrorResponse = {
    error,
    message,
    status,
  };

  return jsonResponse(errorResponse, status);
}
