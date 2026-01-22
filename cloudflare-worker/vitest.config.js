import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json-summary'],
      include: ['src/**/*.js'],
      exclude: ['test/**'],
      thresholds: {
        lines: 90,
        functions: 100,
        branches: 85,
        statements: 90
      }
    }
  },
});
