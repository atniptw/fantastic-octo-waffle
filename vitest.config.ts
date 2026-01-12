import { defineConfig } from 'vitest/config';

/**
 * Root vitest configuration
 * This file provides shared test configuration for the entire monorepo.
 * Individual packages can extend or override these settings in their own vitest.config.ts files.
 */
export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: [
        'node_modules/',
        'dist/',
        'coverage/',
        '**/*.config.{ts,js}',
        '**/.*',
      ],
    },
  },
});
