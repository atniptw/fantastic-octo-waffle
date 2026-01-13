import { defineConfig } from 'vitest/config';

/**
 * Integration test configuration for Cloudflare Worker
 * Integration tests should test the Worker endpoints with realistic scenarios
 * but without making real external HTTP requests.
 */
export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    include: ['src/**/*.test.ts'],
    exclude: ['src/**/*.unit.test.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: ['node_modules/', 'dist/', 'coverage/'],
    },
  },
});
