import { PackageExperimental, PackageListing, PackageIndexEntry } from '@/lib/thunderstore/types';
import ModListItem from './ModListItem';

interface ModListProps {
  mods: Array<PackageExperimental | PackageListing | PackageIndexEntry>;
  selectedModId: string | null;
  onSelectMod: (mod: PackageExperimental | PackageListing | PackageIndexEntry) => void;
  isLoading?: boolean;
  error?: string;
  onRetry?: () => void;
}

export default function ModList({
  mods,
  selectedModId,
  onSelectMod,
  isLoading,
  error,
  onRetry,
}: ModListProps) {
  if (isLoading) {
    return (
      <div className="mod-list">
        <div className="mod-list-loading">Loading mods...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="mod-list">
        <div className="mod-list-error">
          <div className="mod-list-error-title">Unable to load mods</div>
          <div className="mod-list-error-message">{error}</div>
          {onRetry && (
            <button className="mod-list-retry-button" onClick={onRetry}>
              Retry
            </button>
          )}
        </div>
      </div>
    );
  }

  if (mods.length === 0) {
    return (
      <div className="mod-list">
        <div className="mod-list-empty">No mods found</div>
      </div>
    );
  }

  return (
    <div className="mod-list">
      {mods.map((mod) => (
        <ModListItem
          key={'full_name' in mod ? mod.full_name : `${mod.namespace}/${mod.name}`}
          mod={mod}
          isSelected={
            selectedModId === ('full_name' in mod ? mod.full_name : `${mod.namespace}/${mod.name}`)
          }
          onClick={() => onSelectMod(mod)}
        />
      ))}
    </div>
  );
}
