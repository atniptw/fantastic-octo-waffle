/**
 * Pagination component - navigation between pages
 */

import type { FunctionalComponent } from 'preact';
import './Pagination.css';

export interface PaginationProps {
  /** Current page number (1-indexed) */
  currentPage: number;
  /** Total number of pages */
  totalPages: number;
  /** Whether previous page button is enabled */
  hasPrevious: boolean;
  /** Whether next page button is enabled */
  hasNext: boolean;
  /** Callback when previous button is clicked */
  onPrevious: () => void;
  /** Callback when next button is clicked */
  onNext: () => void;
}

/**
 * Displays pagination controls with previous/next buttons and page indicator
 */
export const Pagination: FunctionalComponent<PaginationProps> = ({
  currentPage,
  totalPages,
  hasPrevious,
  hasNext,
  onPrevious,
  onNext,
}) => {
  return (
    <div class="pagination">
      <button
        class="pagination-button"
        onClick={onPrevious}
        disabled={!hasPrevious}
        aria-label="Previous page"
      >
        ← Previous
      </button>

      <span class="pagination-info" aria-live="polite">
        Page {currentPage} of {totalPages || 1}
      </span>

      <button class="pagination-button" onClick={onNext} disabled={!hasNext} aria-label="Next page">
        Next →
      </button>
    </div>
  );
};
