/**
 * Global state management using Preact Signals
 */

import { signal, computed } from '@preact/signals';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';

/**
 * Current page number (1-indexed)
 */
export const currentPage = signal<number>(1);

/**
 * Search query
 */
export const searchQuery = signal<string>('');

/**
 * Sort order
 */
export const sortOrder = signal<'downloads' | 'newest' | 'rating'>('downloads');

/**
 * List of mods on current page
 */
export const mods = signal<ThunderstorePackageVersion[]>([]);

/**
 * Total count of mods (for pagination)
 */
export const totalCount = signal<number>(0);

/**
 * Loading state
 */
export const isLoading = signal<boolean>(false);

/**
 * Error message (null if no error)
 */
export const error = signal<string | null>(null);

/**
 * Selected mod for detail view (null if none selected)
 */
export const selectedMod = signal<ThunderstorePackageVersion | null>(null);

/**
 * Computed: Total number of pages
 */
export const totalPages = computed(() => {
  // Thunderstore API returns 20 mods per page
  const MODS_PER_PAGE = 20;
  return Math.ceil(totalCount.value / MODS_PER_PAGE);
});

/**
 * Computed: Whether there's a previous page
 */
export const hasPreviousPage = computed(() => currentPage.value > 1);

/**
 * Computed: Whether there's a next page
 */
export const hasNextPage = computed(() => currentPage.value < totalPages.value);
