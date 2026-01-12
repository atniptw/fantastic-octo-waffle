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
      onwarn(warning, defaultHandler) {
        // Requirement: Treat all Rollup warnings as errors in CI to maintain code quality
        // This ensures build warnings don't accumulate and become technical debt
        const isCi = process.env.CI === 'true' || process.env.CI === '1';

        if (isCi) {
          // Include warning details for better debugging in CI logs
          const details = [
            `Code: ${warning.code || 'unknown'}`,
            `Message: ${warning.message}`,
            warning.loc
              ? `Location: ${warning.loc.file}:${warning.loc.line}:${warning.loc.column}`
              : '',
          ]
            .filter(Boolean)
            .join('\n');
          throw new Error(`Build failed due to warning:\n${details}`);
        }

        // In non-CI environments, use default handler to avoid disrupting local development
        defaultHandler(warning);
      },
    },
  },
});
