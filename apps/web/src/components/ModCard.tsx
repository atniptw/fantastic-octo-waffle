/**
 * Mod card component - displays a single mod's metadata
 */

import type { FunctionalComponent } from 'preact';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';
import './ModCard.css';

export interface ModCardProps {
  /** Mod data to display */
  mod: ThunderstorePackageVersion;
  /** Callback when card is clicked */
  onClick: (mod: ThunderstorePackageVersion) => void;
}

/**
 * Displays a clickable card with mod information
 */
export const ModCard: FunctionalComponent<ModCardProps> = ({ mod, onClick }) => {
  const handleClick = () => {
    onClick(mod);
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick(mod);
    }
  };

  // Format download count (e.g., 1234 -> "1.2K")
  const formatDownloads = (count: number): string => {
    if (count >= 1000000) {
      return `${(count / 1000000).toFixed(1)}M`;
    }
    if (count >= 1000) {
      return `${(count / 1000).toFixed(1)}K`;
    }
    return count.toString();
  };

  // Format rating (0-100 scale to 0-5 stars)
  const formatRating = (score: number | undefined): string => {
    if (score === undefined || score === null) {
      return 'N/A';
    }
    // Convert 0-100 to 0-5
    const stars = (score / 100) * 5;
    return `${stars.toFixed(1)} ★`;
  };

  return (
    <div
      class="mod-card"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
      aria-label={`View details for ${mod.full_name}`}
    >
      {mod.icon_url && (
        <div class="mod-card-icon">
          <img src={mod.icon_url} alt={`${mod.name} icon`} />
        </div>
      )}
      
      <div class="mod-card-content">
        <h3 class="mod-card-title">{mod.name}</h3>
        <p class="mod-card-author">by {mod.namespace}</p>
        
        <p class="mod-card-description">
          {mod.description || 'No description available'}
        </p>

        <div class="mod-card-stats">
          <span class="mod-stat" title="Downloads">
            📥 {formatDownloads(mod.downloads)}
          </span>
          <span class="mod-stat" title="Rating">
            ⭐ {formatRating(mod.rating_score)}
          </span>
          <span class="mod-stat" title="Version">
            v{mod.version_number}
          </span>
        </div>
      </div>
    </div>
  );
};
