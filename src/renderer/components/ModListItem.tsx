import { PackageExperimental, PackageListing, PackageIndexEntry } from '@/lib/thunderstore/types';

interface ModListItemProps {
  mod: PackageExperimental | PackageListing | PackageIndexEntry;
  isSelected: boolean;
  onClick: () => void;
}

export default function ModListItem({ mod, isSelected, onClick }: ModListItemProps) {
  const downloadCount = 'total_downloads' in mod ? parseInt(mod.total_downloads, 10) || 0 : 0;
  const formattedDownloads =
    downloadCount >= 1000 ? `${(downloadCount / 1000).toFixed(1)}k` : downloadCount.toString();

  return (
    <div
      className={`mod-list-item ${isSelected ? 'selected' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      }}
    >
      <div className="mod-item-content">
        <h3 className="mod-item-name">{mod.name}</h3>
        <p className="mod-item-author">by {'owner' in mod ? mod.owner : mod.namespace}</p>
        <p className="mod-item-description">
          {'latest' in mod && mod.latest?.description ? mod.latest.description : 'No description'}
        </p>
        <div className="mod-item-meta">
          {downloadCount > 0 && <span className="mod-meta-badge">↓ {formattedDownloads}</span>}
          {'is_pinned' in mod && (mod as PackageExperimental).is_pinned && (
            <span className="mod-meta-badge">📌 Pinned</span>
          )}
          {'is_deprecated' in mod && (mod as PackageExperimental).is_deprecated && (
            <span className="mod-meta-badge deprecated">⚠️ Deprecated</span>
          )}
        </div>
      </div>
    </div>
  );
}
