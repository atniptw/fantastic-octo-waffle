/**
 * Mod list component - container for mod cards with grid layout
 */

import type { FunctionalComponent } from 'preact';
import type { ThunderstorePackageVersion } from '@fantastic-octo-waffle/utils';
import { ModCard } from './ModCard';
import './ModList.css';

// ts-prune-ignore-next
export interface ModListProps {
  /** Array of mods to display */
  mods: ThunderstorePackageVersion[];
  /** Callback when a mod card is clicked */
  onModClick: (mod: ThunderstorePackageVersion) => void;
}

/**
 * Grid container displaying multiple mod cards
 */
export const ModList: FunctionalComponent<ModListProps> = ({ mods, onModClick }) => {
  if (mods.length === 0) {
    return (
      <div class="mod-list-empty">
        <p>No mods found</p>
      </div>
    );
  }

  return (
    <div class="mod-list">
      {mods.map((mod) => (
        <ModCard key={mod.full_name} mod={mod} onClick={onModClick} />
      ))}
    </div>
  );
};
