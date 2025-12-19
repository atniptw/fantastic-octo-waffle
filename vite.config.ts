import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

// ESM-compatible __dirname replacement
const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

export default defineConfig(({ mode }) => {
  // Make base path configurable via environment variable
  const isGitHubPages = process.env.GITHUB_PAGES === 'true';
  const basePath = isGitHubPages || mode === 'production' ? '/fantastic-octo-waffle/' : '/';

  return {
    plugins: [react()],
    base: basePath,
    root: './src',
    resolve: {
      conditions: ['browser', 'default'],
      alias: {
        '@': resolve(__dirname, 'src'),
        'sevenzip-wasm': 'sevenzip-wasm/sevenzip-wasm.js'
      }
    },
    build: {
      outDir: '../dist',
      emptyOutDir: true,
      sourcemap: mode === 'production' ? 'hidden' : true, // Use hidden source maps in production
      minify: 'esbuild', // Use esbuild for fast minification
      rollupOptions: {
        output: {
          manualChunks: (id): string | undefined => {
            // Normalize path separators for cross-platform compatibility
            const normalizedId = id.replace(/\\/g, '/');
            
            // Split vendor chunks for better caching
            if (normalizedId.includes('node_modules')) {
              // Match three.js and related packages
              if (normalizedId.includes('/node_modules/three/')) {
                return 'three';
              }
              // Match jszip package
              if (normalizedId.includes('/node_modules/jszip/')) {
                return 'jszip';
              }
              // Match React packages
              if (normalizedId.includes('/node_modules/react/') || normalizedId.includes('/node_modules/react-dom/')) {
                return 'react-vendor';
              }
              // Match utility packages
              if (normalizedId.includes('/node_modules/idb/') || normalizedId.includes('/node_modules/gif.js/')) {
                return 'utils';
              }
              // Other node_modules go into vendor chunk
              return 'vendor';
            }
            // Return undefined for non-vendor modules (handled by default chunking)
            return undefined;
          },
          // Optimize chunk file names
          chunkFileNames: 'assets/[name]-[hash].js',
          entryFileNames: 'assets/[name]-[hash].js',
          assetFileNames: 'assets/[name]-[hash].[ext]',
        },
      },
      // Chunk size warnings
      chunkSizeWarningLimit: 500,
    },
    optimizeDeps: {
      include: ['three', 'jszip', 'idb'],
    },
  };
});
