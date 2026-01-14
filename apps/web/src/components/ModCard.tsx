/**
 * Mod card component - displays a single mod's metadata
 */

import type { FunctionalComponent } from 'preact';
import type { ThunderstorePackageListing } from '@fantastic-octo-waffle/utils';
import {
  formatDownloads,
  formatRating,
  getLatestVersion,
  getTotalDownloads,
} from '@fantastic-octo-waffle/utils';
import './ModCard.css';

export interface ModCardProps {
  /** Mod data to display */
  mod: ThunderstorePackageListing;
  /** Callback when card is clicked */
  onClick: (mod: ThunderstorePackageListing) => void;
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

  // Get latest version for display
  const latestVersion = getLatestVersion(mod);
  const totalDownloads = getTotalDownloads(mod);

  return (
    <div
      class="mod-card"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
      aria-label={`View details for ${mod.full_name}`}
    >
      {latestVersion?.icon && (
        <div class="mod-card-icon">
          <img src={latestVersion.icon} alt={`${mod.name} icon`} />
        </div>
      )}

      <div class="mod-card-content">
        <h3 class="mod-card-title">{mod.name}</h3>
        <p class="mod-card-author">by {mod.namespace}</p>

        <p class="mod-card-description">
          {latestVersion?.description || 'No description available'}
        </p>

        <div class="mod-card-stats">
          <span class="mod-stat" title="Total Downloads">
            📥 {formatDownloads(totalDownloads)}
          </span>
          <span class="mod-stat" title="Rating">
            ⭐ {formatRating(mod.rating_score)}
          </span>
          {latestVersion && (
            <span class="mod-stat" title="Latest Version">
              v{latestVersion.version_number}
            </span>
          )}
        </div>
      </div>
    </div>
  );
};
