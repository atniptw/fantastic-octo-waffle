import type { FunctionalComponent } from 'preact';
import { useEffect } from 'preact/hooks';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';
import { fetchMods } from './lib/api';
import {
  mods,
  currentPage,
  totalCount,
  isLoading,
  error,
  selectedMod,
  totalPages,
  hasPreviousPage,
  hasNextPage,
  sortOrder,
} from './lib/state';
import { ModList } from './components/ModList';
import { Pagination } from './components/Pagination';
import { LoadingSpinner } from './components/LoadingSpinner';
import { ErrorToast } from './components/ErrorToast';
import './app.css';

/**
 * Root application component.
 *
 * Implements Phase 1: Mod List UI
 * - Fetches mods from /api/mods on mount
 * - Displays mod cards in a responsive grid
 * - Provides pagination controls
 * - Shows loading and error states
 */
export const App: FunctionalComponent = () => {
  // Load mods when component mounts
  useEffect(() => {
    loadModsForPage(currentPage.value);
  }, []);

  /**
   * Load mods for a specific page
   */
  const loadModsForPage = async (page: number) => {
    isLoading.value = true;
    error.value = null;

    try {
      const response = await fetchMods(page, '', sortOrder.value);
      mods.value = response.results;
      totalCount.value = response.count;
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to load mods';
      mods.value = [];
      totalCount.value = 0;
    } finally {
      isLoading.value = false;
    }
  };

  const handleModClick = (mod: ThunderstorePackageVersion) => {
    selectedMod.value = mod;
    // TODO: Navigate to mod detail or open modal
  };

  const handlePreviousPage = () => {
    if (hasPreviousPage.value) {
      currentPage.value -= 1;
      window.scrollTo({ top: 0, behavior: 'smooth' });
      loadModsForPage(currentPage.value);
    }
  };

  const handleNextPage = () => {
    if (hasNextPage.value) {
      currentPage.value += 1;
      window.scrollTo({ top: 0, behavior: 'smooth' });
      loadModsForPage(currentPage.value);
    }
  };

  const handleDismissError = () => {
    error.value = null;
  };

  return (
    <div class="app">
      {error.value && <ErrorToast message={error.value} onDismiss={handleDismissError} />}

      <header class="app-header">
        <h1>REPO Cosmetic Viewer</h1>
        <p class="app-subtitle">Browse and preview cosmetic mods</p>
      </header>

      <main class="app-main">
        {isLoading.value ? (
          <LoadingSpinner />
        ) : (
          <>
            <ModList mods={mods.value} onModClick={handleModClick} />
            {mods.value.length > 0 && (
              <Pagination
                currentPage={currentPage.value}
                totalPages={totalPages.value}
                hasPrevious={hasPreviousPage.value}
                hasNext={hasNextPage.value}
                onPrevious={handlePreviousPage}
                onNext={handleNextPage}
              />
            )}
          </>
        )}
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
