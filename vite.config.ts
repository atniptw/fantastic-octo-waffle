import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  base: mode === 'production' ? '/fantastic-octo-waffle/' : '/',
  root: './src',
  build: {
    outDir: '../dist',
    emptyOutDir: true,
    sourcemap: mode === 'production' ? 'hidden' : true, // Use hidden source maps in production
    minify: 'esbuild', // Use esbuild for fast minification
    rollupOptions: {
      output: {
        manualChunks: (id) => {
          // Split vendor chunks for better caching
          if (id.includes('node_modules')) {
            // Match three.js and related packages
            if (id.includes('/node_modules/three/')) {
              return 'three';
            }
            // Match jszip package
            if (id.includes('/node_modules/jszip/')) {
              return 'jszip';
            }
            // Match React packages
            if (id.includes('/node_modules/react/') || id.includes('/node_modules/react-dom/')) {
              return 'react-vendor';
            }
            // Match utility packages
            if (id.includes('/node_modules/idb/') || id.includes('/node_modules/gif.js/')) {
              return 'utils';
            }
            // Other node_modules go into vendor chunk
            return 'vendor';
          }
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
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  optimizeDeps: {
    include: ['three', 'jszip', 'idb'],
  },
}));
