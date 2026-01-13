import type { FunctionalComponent } from 'preact';
import './app.css';

/**
 * Root application component.
 *
 * Renders the top-level application layout, consisting of:
 * - A header with the application title and subtitle.
 * - A main section with placeholder content describing the REPO cosmetic viewer
 *   and current development phase.
 * - A footer with a link to the Thunderstore REPO category.
 *
 * This is a Phase 0 stub UI used to validate the Vite + Preact setup.
 * Future phases will replace the placeholder content with a mod browser,
 * search, pagination, and 3D previews.
 *
 * @returns {JSX.Element} Root application container with header, main content, and footer.
 */
export const App: FunctionalComponent = () => {
  return (
    <div class="app">
      <header class="app-header">
        <h1>REPO Cosmetic Viewer</h1>
        <p class="app-subtitle">Browse and preview cosmetic mods</p>
      </header>

      <main class="app-main">
        <div class="placeholder-content">
          <h2>Welcome!</h2>
          <p>This is a browser-based viewer for REPO game cosmetic mods.</p>
          <p class="status">
            <strong>Status:</strong> Phase 0 - Setup Complete ✓
          </p>
          <p class="coming-soon">Coming soon: Mod browser, 3D previews, and more!</p>
        </div>
      </main>

      <footer class="app-footer">
        <p>
          Powered by{' '}
          <a href="https://thunderstore.io/c/repo/" target="_blank" rel="noopener noreferrer">
            Thunderstore
          </a>
        </p>
      </footer>
    </div>
  );
};
