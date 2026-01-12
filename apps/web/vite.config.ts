import { defineConfig } from 'vite';
import preact from '@preact/preset-vite';

export default defineConfig({
  plugins: [preact()],
  server: {
    port: 5173,
  },
  build: {
    target: 'esnext',
    minify: 'esbuild',
    rollupOptions: {
      onwarn(warning) {
        // Requirement: Treat all Rollup warnings as errors in CI to maintain code quality
        // This ensures build warnings don't accumulate and become technical debt
        throw new Error(warning.message);
      },
    },
  },
});
