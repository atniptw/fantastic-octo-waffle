/**
 * Loading spinner component
 */

import type { FunctionalComponent } from 'preact';
import './LoadingSpinner.css';

/**
 * Simple loading spinner displayed while fetching data
 */
export const LoadingSpinner: FunctionalComponent = () => {
  return (
    <div class="loading-spinner-container">
      <div class="loading-spinner" role="status" aria-label="Loading">
        <div class="spinner"></div>
      </div>
      <p class="loading-text">Loading mods...</p>
    </div>
  );
};
